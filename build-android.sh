#!/usr/bin/env bash
# Builds the Android APK for the Titanic game (Majorsilence.Games.Learning.Android).
#
# Usage:
#   ./build-android.sh                 # Debug APK (default)
#   ./build-android.sh Release         # Release APK (still debug-signed - fine for local testing)
#   ./build-android.sh --install       # build, then adb install -r to the connected device/emulator
#   ./build-android.sh Release --install
#
# The Android project is deliberately not in the .sln (it needs the android
# workload + Android SDK), which is why it gets its own build entry point.
set -euo pipefail

cd "$(dirname "$0")"

CONFIG=Debug
INSTALL=false
for arg in "$@"; do
    case "$arg" in
        Debug|Release) CONFIG="$arg" ;;
        --install) INSTALL=true ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

if ! dotnet workload list 2>/dev/null | grep -q '^android'; then
    echo "The .NET android workload is not installed. Run: dotnet workload install android" >&2
    exit 1
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
