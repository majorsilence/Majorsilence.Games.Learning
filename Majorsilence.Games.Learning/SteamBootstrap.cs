using System;

namespace Majorsilence.Games.Learning;

/// <summary>
/// Optional Steamworks integration, compiled in only when building with
/// -p:EnableSteam=true (see the csproj's conditional PackageReference and
/// STEAM define). With the flag off, every method here is a no-op, so
/// Program.cs can call them unconditionally - the ordinary desktop/Android
/// builds never need to know Steam exists. When compiled in, any
/// initialization failure (no steam_appid.txt in dev, the Steam client not
/// running, wrong platform) is caught and logged rather than crashing the
/// game - Steam features are a bonus, never a requirement to play.
/// </summary>
public static class SteamBootstrap
{
    public static bool IsAvailable { get; private set; }

#if STEAM
    public static void Init()
    {
        try
        {
            IsAvailable = Steamworks.SteamAPI.Init();
            if (!IsAvailable)
                Console.WriteLine("Steam: SteamAPI.Init() returned false (client not running, or no steam_appid.txt for a dev run).");
        }
        catch (Exception e)
        {
            IsAvailable = false;
            Console.WriteLine($"Steam: unavailable ({e.Message}).");
        }
    }

    public static void RunCallbacks()
    {
        if (!IsAvailable) return;
        try
        {
            Steamworks.SteamAPI.RunCallbacks();
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public static void Shutdown()
    {
        if (!IsAvailable) return;
        try
        {
            Steamworks.SteamAPI.Shutdown();
        }
        catch
        {
            // best-effort on the way out
        }
        IsAvailable = false;
    }
#else
    public static void Init()
    {
    }

    public static void RunCallbacks()
    {
    }

    public static void Shutdown()
    {
    }
#endif
}
