# Android head for the Titanic game

Runs the same game code as the desktop `Majorsilence.Games.Learning` project
(sources are linked in; only the console bootstrap `Program.cs` is excluded) on
top of SDL3's official Android port. Boots straight into the Titanic voyage in
single-player.

## Prerequisites

- .NET SDK with the `android` workload (`dotnet workload install android`)
- Android SDK (default probe: `~/Android/Sdk`; override with the
  `AndroidSdkDirectory` MSBuild property or `ANDROID_HOME`)
- JDK 17+

## Build the APK

This project is deliberately **not** in the solution so desktop builds don't
require the Android toolchain. Build it directly:

```bash
dotnet build Majorsilence.Games.Learning.Android
```

Output: `bin/Debug/net10.0-android/com.majorsilence.games.titanic-Signed.apk`
(debug-signed, self-contained — assemblies are embedded, so plain sideloading
works without an IDE).

## Install and run

```bash
adb install -r bin/Debug/net10.0-android/com.majorsilence.games.titanic-Signed.apk
adb shell monkey -p com.majorsilence.games.titanic 1   # or tap the "Titanic" icon
```

Works on emulators (x86_64) and devices (arm64-v8a). Useful log filter while
testing: `adb logcat -s SDL:V monodroid:E AndroidRuntime:E`.

## How it fits together

- `MainActivity` subclasses SDL's own `SDLActivity` (bound from
  `android/SDL3-classes.jar`, extracted from the official `SDL3-3.4.10.aar`).
  It overrides `GetLibraries()` (which native libs to load) and `Main()` (the
  SDL-thread entry point), extracts the APK assets to the app's files dir
  (the game loads assets via ordinary relative file paths), then runs the
  shared game loop.
- `android/libs/<abi>/*.so` are the official SDL3 / SDL3_image / SDL3_ttf /
  SDL3_mixer Android builds, versions matching the SDL3-CS 3.4.10.x binding
  packages the desktop build uses. They're vendored because .NET for Android
  doesn't consume the prefab layout inside SDL's `.aar` releases.
- `Transforms/Metadata.xml` removes a few binding members that reference
  package-private Java helper classes.

## Third-party credits

The vendored SDL binaries are zlib-licensed; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for versions, origins, and
bundled-library licenses.

Portions of this software are copyright © The FreeType Project
(https://www.freetype.org). All rights reserved.

## Known limitations

- **No touch controls yet** — the game is keyboard-driven (arrows/Space/Enter),
  so on a device you'll need a bluetooth/USB keyboard for now. An on-screen
  touch input source is the natural next step.
- The camera viewport uses the full native resolution, so the view is far more
  zoomed out than the 640x480 desktop window.
