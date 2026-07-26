namespace Majorsilence.Games.Shared;

/// <summary>
/// Wire contracts shared between the game (Majorsilence.Games.Learning's
/// CloudSaveClient) and the server (Majorsilence.Games.Server). Kept as plain
/// records with no engine/ASP.NET dependency so both projects can reference
/// this assembly without pulling in anything heavier.
/// </summary>
public record RegisterRequest(string Platform);

public record RegisterResponse(string AccountId, string DeviceId, string Token);

/// <summary>The campaign save payload itself travels as an opaque JSON string (CampaignSave, serialized game-side) - the server never needs to understand its shape, only its timestamp for conflict resolution.</summary>
public record SaveEnvelope(string PayloadJson, DateTimeOffset UpdatedUtc, int Revision);

public record SavePutRequest(string PayloadJson, DateTimeOffset UpdatedUtc);

public record LinkCodeResponse(string Code, DateTimeOffset ExpiresUtc);

public record LinkMergeRequest(string CodeA, string CodeB);

public record LinkMergeResponse(bool Merged, string? Error);

/// <summary>Returned on a 409 conflict from PUT /save: the server's copy, which the client should adopt instead of overwriting.</summary>
public record SaveConflict(SaveEnvelope ServerCopy);
