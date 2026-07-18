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

Or use the helper script from the repo root, which also checks prerequisites
and can install to a connected device/emulator in one step:

```bash
./build-android.sh                    # Debug APK
./build-android.sh Release            # Release APK (smaller, linked)
./build-android.sh --install          # build + adb install -r
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

## Touch controls

`TouchControls` draws an on-screen overlay — a virtual 8-way d-pad on the
left, JUMP / ACT / TIX buttons on the right — and registers itself as an
extra `IInputSource` with `InputActions`, so the shared game code keeps
reading actions with no touch awareness. ACT is the Confirm action (talk to
NPCs, claim role bonuses, board lifeboats); TIX is Fire (throw tix). Multi-
touch works: you can steer and press buttons at the same time. A physical
keyboard still works alongside it. Button artwork lives in
`assets/artwork/touch/` (generated, semi-transparent, sized from the screen's
shorter edge at runtime).

## Known limitations

- The camera viewport uses the full native resolution, so the view is far more
  zoomed out than the 640x480 desktop window.
