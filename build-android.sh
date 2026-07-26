#!/usr/bin/env bash
# Builds the Android APK/AAB for the Titanic game (Majorsilence.Games.Learning.Android).
#
# Usage:
#   ./build-android.sh                 # Debug APK (default)
#   ./build-android.sh Release         # Release APK (debug-signed unless ANDROID_SIGNING_* is set - see the csproj)
#   ./build-android.sh --install       # build, then adb install -r to the connected device/emulator
#   ./build-android.sh Release --install
#   ./build-android.sh --release-bundle  # Release AAB via dotnet publish, for Play Console upload -
#                                         # requires ANDROID_SIGNING_KEY_STORE/ALIAS/STORE_PASS/KEY_PASS
#                                         # (an unsigned AAB can't be uploaded to Play at all)
#
# The Android project is deliberately not in the .sln (it needs the android
# workload + Android SDK), which is why it gets its own build entry point.
set -euo pipefail

cd "$(dirname "$0")"

CONFIG=Debug
INSTALL=false
BUNDLE=false
for arg in "$@"; do
    case "$arg" in
        Debug|Release) CONFIG="$arg" ;;
        --install) INSTALL=true ;;
        --release-bundle) BUNDLE=true; CONFIG=Release ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

if ! dotnet workload list 2>/dev/null | grep -q '^android'; then
    echo "The .NET android workload is not installed. Run: dotnet workload install android" >&2
    exit 1
fi

if $BUNDLE; then
    if [[ -z "${ANDROID_SIGNING_KEY_STORE:-}" ]]; then
        echo "ANDROID_SIGNING_KEY_STORE (and _ALIAS/_STORE_PASS/_KEY_PASS) must be set to build a release bundle - Play won't accept an unsigned AAB." >&2
        exit 1
    fi
    dotnet publish Majorsilence.Games.Learning.Android -c Release -p:AndroidPackageFormat=aab

    AAB="Majorsilence.Games.Learning.Android/bin/Release/net10.0-android/publish/com.majorsilence.games.titanic-Signed.aab"
    if [[ ! -f "$AAB" ]]; then
        echo "Publish reported success but AAB not found at $AAB" >&2
        exit 1
    fi
    echo
    echo "AAB: $AAB ($(du -h "$AAB" | cut -f1)) - upload this to Play Console."
    exit 0
fi

dotnet build Majorsilence.Games.Learning.Android -c "$CONFIG"

APK="Majorsilence.Games.Learning.Android/bin/$CONFIG/net10.0-android/com.majorsilence.games.titanic-Signed.apk"
if [[ ! -f "$APK" ]]; then
    echo "Build reported success but APK not found at $APK" >&2
    exit 1
fi

echo
echo "APK: $APK ($(du -h "$APK" | cut -f1))"

if $INSTALL; then
    ADB="${ANDROID_HOME:-$HOME/Android/Sdk}/platform-tools/adb"
    command -v adb >/dev/null && ADB=adb
    "$ADB" install -r "$APK"
    echo "Installed. Launch with: $ADB shell monkey -p com.majorsilence.games.titanic 1"
else
    echo "Install with: adb install -r $APK"
fi
