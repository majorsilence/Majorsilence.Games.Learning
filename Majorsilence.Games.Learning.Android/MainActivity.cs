using Android.App;
using Android.Content.PM;
using Org.Libsdl.App;

namespace Majorsilence.Games.Learning.Android;

/// <summary>
/// The Android entry point: SDL's own SDLActivity handles the surface, input,
/// audio routing and lifecycle, and calls Main() on a dedicated SDL thread -
/// overridden here to run the managed game directly instead of the default
/// native libmain.so lookup. GetLibraries() names the native SDL libraries to
/// load (and deliberately omits the default "main" entry, which doesn't exist
/// in a managed app).
/// </summary>
[Activity(
    Label = "Titanic",
    MainLauncher = true,
    Exported = true,
    HardwareAccelerated = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.Landscape,
    Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
    ConfigurationChanges = ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                           ConfigChanges.Navigation | ConfigChanges.Orientation |
                           ConfigChanges.ScreenLayout | ConfigChanges.ScreenSize |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density |
                           ConfigChanges.UiMode)]
public class MainActivity : SDLActivity
{
    protected override string[] GetLibraries() => new[] { "SDL3", "SDL3_image", "SDL3_ttf", "SDL3_mixer" };

    protected override void Main()
    {
        // The game loads everything through relative file paths ("assets/...").
        // APK assets aren't files, so copy them out to the app's private files
        // dir on launch and make that the working directory. AssetManager paths
        // are relative to the APK's assets/ root (no "assets/" prefix), so each
        // top-level tree is re-rooted under an assets/ directory on disk.
        var root = ApplicationContext!.FilesDir!.AbsolutePath;
        foreach (var top in new[] { "artwork", "audio", "fonts", "levels" })
        {
            ExtractAssets(top, Path.Combine(root, "assets"));
        }
        Directory.SetCurrentDirectory(root);

        AndroidGame.Run();
    }

    /// <summary>Recursively copies an APK asset directory to real files under targetRoot.</summary>
    private void ExtractAssets(string assetPath, string targetRoot)
    {
        var assets = ApplicationContext!.Assets!;
        var children = assets.List(assetPath) ?? Array.Empty<string>();

        if (children.Length == 0)
        {
            // a leaf: an actual asset file
            var destination = Path.Combine(targetRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = assets.Open(assetPath);
            using var target = File.Create(destination);
            source.CopyTo(target);
            return;
        }

        foreach (var child in children)
        {
            ExtractAssets($"{assetPath}/{child}", targetRoot);
        }
    }
}
