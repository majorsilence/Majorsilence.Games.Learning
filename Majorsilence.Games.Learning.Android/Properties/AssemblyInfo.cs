using Android.App;

// Required for CloudSaveClient (cloud saves, link codes) - see
// Majorsilence.Games.Learning/CloudSaveClient.cs. Without these the game still
// runs fine (every network call there is try/catch-wrapped and falls back to
// the local save), it just can never reach the server.
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
