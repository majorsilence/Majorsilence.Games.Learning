using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Majorsilence.Games.Learning;

/// <summary>
/// Everything that differs between one voyage and the next: which ship (level
/// files and hull geometry) and how the disaster plays out (timings, rescue
/// window, banking goal). Game holds exactly one of these at a time; hull
/// geometry that used to be hardcoded Titanic constants now comes from here.
/// </summary>
public record VoyageConfig(
    string Id,
    string Name,
    string Briefing,
    string ExteriorPath,
    string SplitPath,
    int HullColumns,
    int HullRows,
    float CollisionMinSeconds,
    float CollisionMaxSeconds,
    float SplitAfterCollisionSeconds,
    float SunkAfterCollisionSeconds,
    float BoardingWindowSeconds,
    int TixGoal,
    int GoalBonusTix)
{
    // Geometry conventions shared by every generated hull (and the original
    // Titanic): center column, bow tip near row 2, stern tip 3 rows from the
    // end, the ship breaking amidships, and the final stern refuge being the
    // last ~15 rows.
    public int CenterColumn => HullColumns / 2 - 1;
    public int SternRow => HullRows - 3;
    public int BowRow => 2;
    public int SplitMidRow => HullRows / 2;
    public int WaterlineRowAtSplit => SplitMidRow - 1;
    public int WaterlineRowAtSunk => HullRows - 15;
}

/// <summary>
/// The four-voyage career: a gentle sea-trials shakedown, two escalating
/// liners, and the Titanic herself as the finale. Difficulty comes from three
/// dials turning together voyage over voyage: the sinking gets faster, the
/// Carpathia waits less, and the banking goal grows - while the banked tix and
/// gear carried between voyages are what make the later ships survivable.
/// </summary>
public static class Campaign
{
    public static readonly IReadOnlyList<VoyageConfig> Voyages = new[]
    {
        new VoyageConfig(
            Id: "dauntless",
            Name: "Voyage 1: SS Dauntless",
            Briefing: "Sea trials. Learn the ship, loot her well, and get everyone home.",
            ExteriorPath: "assets/levels/dauntless/ship.json",
            SplitPath: "assets/levels/dauntless/ship-split.json",
            HullColumns: 16, HullRows: 48,
            CollisionMinSeconds: 90f, CollisionMaxSeconds: 150f,
            SplitAfterCollisionSeconds: 80f, SunkAfterCollisionSeconds: 130f,
            BoardingWindowSeconds: 90f,
            TixGoal: 400, GoalBonusTix: 200),

        new VoyageConfig(
            Id: "meridian",
            Name: "Voyage 2: RMS Meridian",
            Briefing: "A proper liner on the night run. She goes down faster than the Dauntless did.",
            ExteriorPath: "assets/levels/meridian/ship.json",
            SplitPath: "assets/levels/meridian/ship-split.json",
            HullColumns: 20, HullRows: 64,
            CollisionMinSeconds: 120f, CollisionMaxSeconds: 300f,
            SplitAfterCollisionSeconds: 70f, SunkAfterCollisionSeconds: 115f,
            BoardingWindowSeconds: 75f,
            TixGoal: 900, GoalBonusTix: 350),

        new VoyageConfig(
            Id: "colossus",
            Name: "Voyage 3: HMHS Colossus",
            Briefing: "A hospital ship, huge and slow to abandon. The wards are rich and flood early.",
            ExteriorPath: "assets/levels/colossus/ship.json",
            SplitPath: "assets/levels/colossus/ship-split.json",
            HullColumns: 24, HullRows: 96,
            CollisionMinSeconds: 150f, CollisionMaxSeconds: 360f,
            SplitAfterCollisionSeconds: 60f, SunkAfterCollisionSeconds: 100f,
            BoardingWindowSeconds: 60f,
            TixGoal: 1600, GoalBonusTix: 500),

        new VoyageConfig(
            Id: "titanic",
            Name: "Voyage 4: RMS Titanic",
            Briefing: "The unsinkable one. You know how this ends - be ready when it does.",
            ExteriorPath: "assets/levels/titanic.json",
            SplitPath: "assets/levels/titanic-rooms/boat-deck-split.json",
            HullColumns: 20, HullRows: 80,
            CollisionMinSeconds: 60f, CollisionMaxSeconds: 600f,
            SplitAfterCollisionSeconds: 70f, SunkAfterCollisionSeconds: 110f,
            BoardingWindowSeconds: 50f,
            TixGoal: 2500, GoalBonusTix: 1000),
    };

    /// <summary>Free play: the original single-voyage Titanic experience, outside the campaign.</summary>
    public static readonly VoyageConfig FreePlayTitanic = Voyages[^1] with
    {
        Name = "RMS Titanic",
        BoardingWindowSeconds = 75f,
        TixGoal = 0, GoalBonusTix = 0
    };
}

public class InventorySave
{
    public bool HasLifeJacket { get; set; }
    public bool HasDeckBoots { get; set; }
    public bool HasTixLauncher { get; set; }
    public int Blankets { get; set; }
    public List<string> Snacks { get; set; } = new();
    public List<string> Tnt { get; set; } = new();

    public static InventorySave From(PlayerInventory inventory) => new()
    {
        HasLifeJacket = inventory.HasLifeJacket,
        HasDeckBoots = inventory.HasDeckBoots,
        HasTixLauncher = inventory.HasTixLauncher,
        Blankets = inventory.Blankets,
        Snacks = inventory.Snacks.Select(s => s.Kind.ToString()).ToList(),
        Tnt = inventory.TntCharges.Select(t => t.ToString()).ToList(),
    };

    public void ApplyTo(PlayerInventory inventory)
    {
        inventory.HasLifeJacket = HasLifeJacket;
        inventory.HasDeckBoots = HasDeckBoots;
        inventory.HasTixLauncher = HasTixLauncher;
        inventory.Blankets = Blankets;
        inventory.Snacks.Clear();
        foreach (var name in Snacks)
        {
            var item = ShopCatalog.Items.FirstOrDefault(i => i.Snack?.Kind.ToString() == name);
            if (item?.Snack is not null) inventory.Snacks.Enqueue(item.Snack);
        }
        inventory.TntCharges.Clear();
        foreach (var name in Tnt)
        {
            if (Enum.TryParse<TntSize>(name, out var size)) inventory.TntCharges.Enqueue(size);
        }
    }
}

/// <summary>
/// Campaign progress on disk: which voyage is next, the shared bank, and each
/// player's carried gear. Stored under the user's application-data directory
/// so it survives rebuilds of the game itself.
/// </summary>
public class CampaignSave
{
    public int VoyageIndex { get; set; }
    public int Bank { get; set; } = 300;
    public List<InventorySave> Players { get; set; } = new();

    private static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "majorsilence-titanic", "campaign.json");

    public static CampaignSave Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonSerializer.Deserialize<CampaignSave>(File.ReadAllText(SavePath)) ?? new CampaignSave();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not read campaign save ({e.Message}) - starting fresh.");
        }
        return new CampaignSave();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not write campaign save: {e.Message}");
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
        catch
        {
            // a stale save that can't be deleted just gets overwritten on the next Save()
        }
    }
}
