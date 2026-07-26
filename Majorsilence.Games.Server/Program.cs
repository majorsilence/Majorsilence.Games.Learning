using Dapper;
using Majorsilence.Games.Server;
using Majorsilence.Games.Shared;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var dataSourcePath = builder.Configuration["DataSourcePath"]
    ?? Environment.GetEnvironmentVariable("TITANIC_DB_PATH")
    ?? "titanic.db";
var db = new Db(dataSourcePath);
db.EnsureSchema();
builder.Services.AddSingleton(db);

// Register/link are the only unauthenticated, abusable endpoints (anyone can
// call them); a fixed-window cap per client IP is enough to keep the account/
// code space from being brute-forced or spammed without needing a full WAF.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

// Resolves the caller's account id from the Authorization: Bearer header, or
// null if missing/unrecognized. Touches last_seen_utc as a side effect so a
// single query both authenticates and records device activity.
async Task<string?> AuthenticateAsync(HttpContext http, Db database)
{
    var header = http.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
    var token = header["Bearer ".Length..].Trim();
    if (token.Length == 0) return null;

    var tokenHash = TokenService.Hash(token);
    using var connection = database.Open();
    var accountId = await connection.QueryFirstOrDefaultAsync<string?>(
        "UPDATE devices SET last_seen_utc = @Now WHERE token_hash = @Hash RETURNING account_id",
        new { Hash = tokenHash, Now = DateTimeOffset.UtcNow.ToString("O") });
    return accountId;
}

var api = app.MapGroup("/api/v1");

api.MapPost("/devices/register", (RegisterRequest req, Db database) =>
{
    using var connection = database.Open();
    var accountId = Guid.NewGuid().ToString();
    var deviceId = Guid.NewGuid().ToString();
    var token = TokenService.GenerateToken();
    var now = DateTimeOffset.UtcNow.ToString("O");

    connection.Execute("INSERT INTO accounts (id, created_utc) VALUES (@Id, @Now)", new { Id = accountId, Now = now });
    connection.Execute(
        "INSERT INTO devices (id, account_id, token_hash, platform, created_utc, last_seen_utc) VALUES (@Id, @AccountId, @Hash, @Platform, @Now, @Now)",
        new { Id = deviceId, AccountId = accountId, Hash = TokenService.Hash(token), Platform = req.Platform, Now = now });

    return Results.Ok(new RegisterResponse(accountId, deviceId, token));
}).RequireRateLimiting("auth");

api.MapGet("/save", async (HttpContext http, Db database) =>
{
    var accountId = await AuthenticateAsync(http, database);
    if (accountId is null) return Results.Unauthorized();

    using var connection = database.Open();
    var row = await connection.QueryFirstOrDefaultAsync<SaveRow>(
        "SELECT payload_json AS PayloadJson, updated_utc AS UpdatedUtc, revision AS Revision FROM saves WHERE account_id = @AccountId",
        new { AccountId = accountId });

    return row is null
        ? Results.NoContent()
        : Results.Ok(new SaveEnvelope(row.PayloadJson, DateTimeOffset.Parse(row.UpdatedUtc), row.Revision));
});

api.MapPut("/save", async (SavePutRequest req, HttpContext http, Db database) =>
{
    var accountId = await AuthenticateAsync(http, database);
    if (accountId is null) return Results.Unauthorized();

    using var connection = database.Open();
    var existing = await connection.QueryFirstOrDefaultAsync<SaveRow>(
        "SELECT payload_json AS PayloadJson, updated_utc AS UpdatedUtc, revision AS Revision FROM saves WHERE account_id = @AccountId",
        new { AccountId = accountId });

    // Last-write-wins: a PUT older than what's already stored is rejected with
    // the server's copy so the client can adopt it instead of clobbering newer
    // progress synced from a second device.
    if (existing is not null && req.UpdatedUtc < DateTimeOffset.Parse(existing.UpdatedUtc))
    {
        var conflict = new SaveConflict(new SaveEnvelope(existing.PayloadJson, DateTimeOffset.Parse(existing.UpdatedUtc), existing.Revision));
        return Results.Json(conflict, statusCode: StatusCodes.Status409Conflict);
    }

    var revision = (existing?.Revision ?? 0) + 1;
    connection.Execute("""
        INSERT INTO saves (account_id, payload_json, updated_utc, revision, device_id)
        VALUES (@AccountId, @PayloadJson, @UpdatedUtc, @Revision, @AccountId)
        ON CONFLICT(account_id) DO UPDATE SET
            payload_json = excluded.payload_json,
            updated_utc = excluded.updated_utc,
            revision = excluded.revision
        """,
        new { AccountId = accountId, req.PayloadJson, UpdatedUtc = req.UpdatedUtc.ToString("O"), Revision = revision });

    return Results.Ok(new SaveEnvelope(req.PayloadJson, req.UpdatedUtc, revision));
});

api.MapPost("/link/code", async (HttpContext http, Db database) =>
{
    var accountId = await AuthenticateAsync(http, database);
    if (accountId is null) return Results.Unauthorized();

    using var connection = database.Open();
    var code = TokenService.GenerateLinkCode();
    var expires = DateTimeOffset.UtcNow.AddMinutes(10);
    connection.Execute("INSERT INTO link_codes (code, account_id, expires_utc) VALUES (@Code, @AccountId, @Expires)",
        new { Code = code, AccountId = accountId, Expires = expires.ToString("O") });

    return Results.Ok(new LinkCodeResponse(code, expires));
}).RequireRateLimiting("auth");

api.MapPost("/link/merge", (LinkMergeRequest req, Db database) =>
{
    using var connection = database.Open();
    using var transaction = connection.BeginTransaction();
    var now = DateTimeOffset.UtcNow;

    string? ResolveAccount(string code)
    {
        var row = connection.QueryFirstOrDefault<LinkCodeRow>(
            "SELECT account_id AS AccountId, expires_utc AS ExpiresUtc FROM link_codes WHERE code = @Code",
            new { Code = code }, transaction);
        if (row is null || DateTimeOffset.Parse(row.ExpiresUtc) < now) return null;
        return row.AccountId;
    }

    var accountA = ResolveAccount(req.CodeA);
    var accountB = ResolveAccount(req.CodeB);
    if (accountA is null || accountB is null)
    {
        transaction.Rollback();
        return Results.Ok(new LinkMergeResponse(false, "One or both codes are invalid or expired."));
    }
    if (accountA == accountB)
    {
        transaction.Rollback();
        return Results.Ok(new LinkMergeResponse(false, "Both codes already belong to the same account."));
    }

    // Keep whichever account has the newer save (or A, if neither/only A has
    // one); repoint every device of the loser onto the survivor, drop the
    // loser's save and account row. The survivor's own save row already
    // carries the survivor's account id, so nothing else needs updating there.
    var saveA = connection.QueryFirstOrDefault<SaveRow>("SELECT payload_json AS PayloadJson, updated_utc AS UpdatedUtc, revision AS Revision FROM saves WHERE account_id = @Id", new { Id = accountA }, transaction);
    var saveB = connection.QueryFirstOrDefault<SaveRow>("SELECT payload_json AS PayloadJson, updated_utc AS UpdatedUtc, revision AS Revision FROM saves WHERE account_id = @Id", new { Id = accountB }, transaction);
    var bIsNewer = saveB is not null && (saveA is null || DateTimeOffset.Parse(saveB.UpdatedUtc) > DateTimeOffset.Parse(saveA.UpdatedUtc));
    var survivor = bIsNewer ? accountB : accountA;
    var loser = bIsNewer ? accountA : accountB;

    // link_codes must go first: both rows still reference (at least) the loser
    // account, and SQLite enforces the foreign key on DELETE FROM accounts below.
    connection.Execute("DELETE FROM link_codes WHERE code = @CodeA OR code = @CodeB", new { req.CodeA, req.CodeB }, transaction);
    connection.Execute("UPDATE devices SET account_id = @Survivor WHERE account_id = @Loser", new { Survivor = survivor, Loser = loser }, transaction);
    connection.Execute("DELETE FROM saves WHERE account_id = @Loser", new { Loser = loser }, transaction);
    connection.Execute("DELETE FROM accounts WHERE id = @Loser", new { Loser = loser }, transaction);

    transaction.Commit();
    return Results.Ok(new LinkMergeResponse(true, null));
}).RequireRateLimiting("auth");

app.Run();

internal record SaveRow
{
    public string PayloadJson { get; init; } = "";
    public string UpdatedUtc { get; init; } = "";
    public int Revision { get; init; }
}

internal record LinkCodeRow
{
    public string AccountId { get; init; } = "";
    public string ExpiresUtc { get; init; } = "";
}
