using System.Text.Json;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// One character as written to disk. Stats are stored rather than recomputed
/// from party.json and a level, because the roster file is content that will be
/// retuned - and a save that silently restats everyone when the designer nudges
/// a growth number is a save that lied about what it was keeping.
/// </summary>
public class SavedMember
{
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Agility { get; set; }
}

/// <summary>
/// The state of a game in progress: who the party are, what they carry, and
/// where they were standing.
///
/// Written under the user's application-data directory so it survives rebuilds,
/// the same way the Titanic game's campaign save does. Reading is deliberately
/// forgiving - a save that won't parse starts a new game rather than stopping
/// the player getting in.
/// </summary>
public class SaveGame
{
    /// <summary>Map the party was on. Empty means "no save", and the caller should start the game from its usual beginning.</summary>
    public string MapPath { get; set; } = "";

    public int Column { get; set; }
    public int Row { get; set; }

    public int Experience { get; set; }
    public int Coin { get; set; }

    /// <summary>Item key to how many are carried.</summary>
    public Dictionary<string, int> Bag { get; set; } = new();

    public List<SavedMember> Members { get; set; } = new();

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Where the save lives. RPG_SAVE_DIR redirects it, which is what lets a
    /// scripted run exercise saving without touching the player's real game.
    /// </summary>
    public static string SaveDirectory =>
        Environment.GetEnvironmentVariable("RPG_SAVE_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "majorsilence-vale-of-ash");

    public static string SavePath => PathIn(SaveDirectory);

    /// <summary>
    /// The save file inside a given directory. Every operation takes the
    /// directory rather than reading the environment, so a caller - a test, or
    /// a second save slot later - can point somewhere else without disturbing
    /// process-wide state.
    /// </summary>
    public static string PathIn(string directory) => Path.Combine(directory, "save.json");

    public static bool Exists(string? directory = null) => File.Exists(PathIn(directory ?? SaveDirectory));

    /// <summary>Reads the save, or returns null when there isn't one (or it can't be read).</summary>
    public static SaveGame? Load(string? directory = null)
    {
        var path = PathIn(directory ?? SaveDirectory);
        try
        {
            if (!File.Exists(path)) return null;
            var save = JsonSerializer.Deserialize<SaveGame>(File.ReadAllText(path));
            // A save with no map has nowhere to put the player, so it is no save.
            return save is { MapPath.Length: > 0 } ? save : null;
        }
        catch (Exception error)
        {
            Console.WriteLine($"Could not read save ({error.Message}) - starting fresh.");
            return null;
        }
    }

    public void Save(string? directory = null)
    {
        var folder = directory ?? SaveDirectory;
        UpdatedUtc = DateTimeOffset.UtcNow;
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(PathIn(folder),
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            Console.WriteLine($"Could not write save: {error.Message}");
        }
    }

    public static void Delete(string? directory = null)
    {
        try
        {
            var path = PathIn(directory ?? SaveDirectory);
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // a save that can't be deleted just gets overwritten by the next one
        }
    }
}
