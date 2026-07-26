using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Games.Shared;

namespace Majorsilence.Games.Learning;

/// <summary>Device identity + server address, persisted separately from CampaignSave (surviving even a "New Campaign" wipe, which only deletes campaign.json).</summary>
public class CloudSaveConfig
{
    public string ServerUrl { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Token { get; set; } = "";
}

/// <summary>
/// Talks to Majorsilence.Games.Server for anonymous device-account cloud
/// saves. Every public method is failure-tolerant by design: a missing or
/// unreachable server, a fresh install, or a player who's simply offline must
/// never block or crash the game - the local CampaignSave file remains fully
/// authoritative and playable with this doing nothing at all.
/// </summary>
public class CloudSaveClient
{
    // Aspirational default; not yet a real deployed service. Point at a local
    // dev instance (or any other server) with TITANIC_SERVER_URL.
    private const string DefaultServerUrl = "https://titanic-save.majorsilence.com";

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "majorsilence-titanic", "cloud.json");

    private readonly HttpClient _http;
    private readonly CloudSaveConfig _config;
    private readonly SemaphoreSlim _uploadLock = new(1, 1);
    private CampaignSave? _pendingUpload;

    /// <summary>Raised with a short human-readable status after a sync attempt actually does something worth telling the player about (a successful upload). Game subscribes to surface it via ShowMessage.</summary>
    public event Action<string>? Notified;

    public string LinkSiteUrl => $"{_config.ServerUrl}/link.html";

    public CloudSaveClient()
    {
        var serverUrl = Environment.GetEnvironmentVariable("TITANIC_SERVER_URL") ?? DefaultServerUrl;
        _config = LoadConfig() ?? new CloudSaveConfig();
        if (string.IsNullOrEmpty(_config.ServerUrl)) _config.ServerUrl = serverUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    }

    private static CloudSaveConfig? LoadConfig()
    {
        try
        {
            return File.Exists(ConfigPath) ? JsonSerializer.Deserialize<CloudSaveConfig>(File.ReadAllText(ConfigPath)) : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Offline-safe: the device token just won't persist, so the next
            // launch registers a new device instead - annoying, not fatal.
        }
    }

    private bool IsRegistered => !string.IsNullOrEmpty(_config.Token);

    private async Task EnsureRegisteredAsync()
    {
        if (IsRegistered) return;

        var response = await _http.PostAsJsonAsync($"{_config.ServerUrl}/api/v1/devices/register", new RegisterRequest(RuntimePlatform()));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        if (body is null) throw new InvalidOperationException("empty register response");

        _config.DeviceId = body.DeviceId;
        _config.Token = body.Token;
        SaveConfig();
    }

    private static string RuntimePlatform() => OperatingSystem.IsAndroid() ? "android"
        : OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos"
        : "linux";

    private void Authorize() => _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);

    /// <summary>
    /// Called once at startup, before the title screen: registers this device
    /// if needed, fetches the cloud save, and returns it only if it's strictly
    /// newer than the local copy (null otherwise - offline, no server, no
    /// cloud save yet, or the local copy already wins). Bounded by the
    /// HttpClient's 3s timeout, so a dead/unreachable server can't hang launch.
    /// </summary>
    public async Task<CampaignSave?> StartupSyncAsync(CampaignSave local)
    {
        try
        {
            await EnsureRegisteredAsync();
            Authorize();

            var response = await _http.GetAsync($"{_config.ServerUrl}/api/v1/save");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<SaveEnvelope>();
            if (envelope is null || envelope.UpdatedUtc <= local.UpdatedUtc) return null;

            var cloudSave = JsonSerializer.Deserialize<CampaignSave>(envelope.PayloadJson);
            if (cloudSave is null) return null;
            cloudSave.UpdatedUtc = envelope.UpdatedUtc;
            return cloudSave;
        }
        catch
        {
            // No reachable server, first launch, offline - the local save wins by default.
            return null;
        }
    }

    /// <summary>
    /// Fire-and-forget upload of a freshly-written CampaignSave. Never awaited
    /// by the caller - the frame loop must not stall on network I/O. Multiple
    /// calls collapse into "upload whatever the latest queued save is" rather
    /// than queuing every one individually, and a failed attempt is simply
    /// retried whenever the next save happens (which always carries newer or
    /// equal state anyway).
    /// </summary>
    public void QueueUpload(CampaignSave save)
    {
        _pendingUpload = save;
        _ = UploadLoopAsync();
    }

    private async Task UploadLoopAsync()
    {
        if (!await _uploadLock.WaitAsync(0)) return; // an upload is already draining the queue; it will pick up this update itself
        try
        {
            while (_pendingUpload is not null)
            {
                var save = _pendingUpload;
                _pendingUpload = null;
                await TryUploadOnceAsync(save!);
            }
        }
        finally
        {
            _uploadLock.Release();
        }
    }

    private async Task TryUploadOnceAsync(CampaignSave save)
    {
        var payload = JsonSerializer.Serialize(save);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await EnsureRegisteredAsync();
                Authorize();
                var response = await _http.PutAsJsonAsync($"{_config.ServerUrl}/api/v1/save", new SavePutRequest(payload, save.UpdatedUtc));
                if (response.IsSuccessStatusCode)
                {
                    Notified?.Invoke("Progress synced to the cloud.");
                    return;
                }
                if (response.StatusCode == HttpStatusCode.Conflict) return; // the server's copy is newer - nothing to retry, it's already right
            }
            catch
            {
                // offline or unreachable - fall through to the single retry, then give up quietly
            }
            if (attempt == 0) await Task.Delay(1000);
        }
    }

    /// <summary>Mints a short-lived link code for this device to display, for the player to enter (alongside a second device's code) at LinkSiteUrl. Null on any failure.</summary>
    public async Task<string?> RequestLinkCodeAsync()
    {
        try
        {
            await EnsureRegisteredAsync();
            Authorize();
            var response = await _http.PostAsync($"{_config.ServerUrl}/api/v1/link/code", null);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadFromJsonAsync<LinkCodeResponse>();
            return body?.Code;
        }
        catch
        {
            return null;
        }
    }
}
