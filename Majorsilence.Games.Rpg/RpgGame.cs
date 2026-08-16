using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Levels;
using Majorsilence.Games.Core.Physics;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using Majorsilence.Games.Core.Tilemaps;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// The exploration half of the game: whichever map is loaded, the hero walking
/// around it, the townsfolk standing on it, and the doorways between maps.
///
/// Maps are ordinary engine levels (LevelLoader JSON) declaring
/// perspective "topdown", rendered through FlatTilemap - the same orthogonal
/// renderer the side-scrolling rooms use. What differs is the body attached to
/// the walker: TopDownBody, which walks in both axes with no gravity.
///
/// A map is rebuilt from scratch on every transition rather than mutated, so
/// there's no partly-torn-down state to reason about - the same approach the
/// Titanic game takes with its rooms.
/// </summary>
public class RpgGame : IDisposable
{
    private const int TileSize = 16;
    private const float WalkSpeed = 64f;
    private const float DoorCooldownSeconds = 0.4f;

    private const string BattleMusicPath = "assets/audio/rpg/battle.wav";

    /// <summary>Where a defeat puts you back: the town square you started from.</summary>
    private const string DefeatMap = "assets/levels/ashholt.json";
    private const string DefeatSpawn = "";

    /// <summary>
    /// How many tiles ahead a "talk" reaches. Two, not one, so the hero can lean
    /// over a shop counter to the keeper behind it - but the second tile only
    /// counts when the first is solid, so this never becomes shouting across an
    /// empty room.
    /// </summary>
    private const int TalkReachTiles = 2;

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly Dictionary<string, SpriteSheet> _sheets = new();

    private FlatTilemap _tilemap = null!;
    private LevelMap _level = null!;
    private string[,] _tileTypes = new string[0, 0];
    private readonly List<Door> _doors = new();
    private readonly List<Folk> _folk = new();
    private readonly Dictionary<string, (int Column, int Row)> _spawns = new();
    private readonly HashSet<(int Column, int Row)> _occupied = new();
    private readonly Sound? _confirmSound;
    private readonly Sound? _cancelSound;
    private readonly Sound? _doorSound;
    private readonly Sound? _victorySound;
    private readonly MonsterBook _monsterBook;
    private readonly Random _random;
    private float _doorCooldown;
    private Folk? _talkingTo;

    /// <summary>What this map can throw at you, and how often - both from the level file.</summary>
    private List<string> _encounterTable = new();
    private float _encounterRate;
    private string _mapMusic = "";

    /// <summary>
    /// The tile the last encounter check ran on. Encounters are rolled per tile
    /// entered rather than per frame, so standing still is safe and walking is
    /// what carries the risk.
    /// </summary>
    private (int Column, int Row) _lastEncounterTile = (-1, -1);
    private bool _victoryPlayed;

    public RpgGame(Renderer renderer, string fontPath, AudioDevice? audio = null, int? seed = null)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        // Seeded on request so a scripted run fights a reproducible battle;
        // otherwise every run rolls its own.
        _random = seed is { } value ? new Random(value) : new Random();
        Camera = new Camera { Axis = ScrollAxis.Free };
        Dialogue = new DialogueBox(renderer, fontPath);
        Hero = new Walker(GetSheet("assets/artwork/rpg/hero.png")) { Speed = WalkSpeed, ZIndex = 1 };
        Music = new MusicDirector(audio);

        _monsterBook = MonsterBook.Load("assets/monsters.json");
        BattleScreen = new BattleScreen(renderer, fontPath,
            new SpriteSheet(Texture.CreateImageTexture(renderer, "assets/artwork/rpg/monsters.png"), 32, 32));

        // Starting numbers for the one character there is. Levels, MP and a
        // party are the next slice; these are what a first fight is balanced
        // against.
        Champion = new Combatant
        {
            Name = "Wren",
            MaxHealth = 28,
            Health = 28,
            Attack = 9,
            Defense = 4,
            Agility = 8
        };

        // Sound is optional the same way it is in the Titanic game: no audio
        // device (a test run, a machine with no card) means a silent game, not a
        // crashed one.
        if (audio is not null)
        {
            _confirmSound = new Sound(audio, "assets/audio/rpg/confirm.wav") { Volume = 0.5f };
            _cancelSound = new Sound(audio, "assets/audio/rpg/cancel.wav") { Volume = 0.5f };
            _doorSound = new Sound(audio, "assets/audio/rpg/door.wav") { Volume = 0.6f };
            _victorySound = new Sound(audio, "assets/audio/rpg/victory.wav") { Volume = 0.6f };
        }
    }

    public MusicDirector Music { get; }

    /// <summary>The hero's fighting self - the same character the Walker draws, kept separately because battle cares about numbers and the map cares about pixels.</summary>
    public Combatant Champion { get; }

    public BattleScreen BattleScreen { get; }

    /// <summary>The fight in progress, or null when out on the map. Non-null means walking is suspended.</summary>
    public Battle? Battle => BattleScreen.Battle;

    /// <summary>Banked experience. Nothing spends it yet - levels arrive with the stats slice.</summary>
    public int Experience { get; private set; }

    public Camera Camera { get; }
    public DialogueBox Dialogue { get; }
    public Walker Hero { get; }

    /// <summary>Which map is loaded, and where the hero stands on it - what a scripted run prints to show where it got to.</summary>
    public string MapName { get; private set; } = "";

    public (int Column, int Row) HeroTile =>
        Hero.Body?.CenterTile(Hero.PreciseX, Hero.PreciseY) ?? (0, 0);

    /// <summary>Everything the render loop draws, rebuilt per map (the hero and the dialogue window persist across maps).</summary>
    public List<GameObject> GameObjects { get; } = new();

    private record Door(int Column, int Row, string Target, string Spawn);

    /// <summary>
    /// A townsperson: where they stand, what they say, and how far through it
    /// they are. The line index lives per person, so walking away mid-story and
    /// coming back later picks up where that person left off rather than
    /// wherever the last conversation in town happened to end.
    /// </summary>
    private class Folk
    {
        public required Walker Walker { get; init; }
        public required string Name { get; init; }
        public required string[] Lines { get; init; }
        public required int Column { get; init; }
        public required int Row { get; init; }
        public int NextLine { get; set; }
    }

    public void LoadMap(string path, string spawnName = "")
    {
        _level = LevelLoader.Load(path);
        MapName = Path.GetFileNameWithoutExtension(path);
        if (!_level.Perspective.Equals("topdown", StringComparison.OrdinalIgnoreCase))
            throw new MajorsilenceException($"Map '{path}' is not a topdown level.");

        _doors.Clear();
        _folk.Clear();
        _spawns.Clear();
        _occupied.Clear();
        GameObjects.Clear();
        Dialogue.Close();
        _talkingTo = null;

        var rows = _level.Tiles.Length;
        var columns = rows == 0 ? 0 : _level.Tiles[0].Length;
        _tileTypes = new string[rows, columns];
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            _tileTypes[row, column] = _level.Legend[_level.Tiles[row][column]];

        var tiles = LevelLoader.ResolveTileIndices(_level, _level.TileFrames);
        var tileset = GetSheet(_level.TilesetPath);
        _tilemap = new FlatTilemap(tiles, tileset, _level.TileWidth, _level.TileHeight) { ZIndex = 0 };
        GameObjects.Add(_tilemap);

        BuildEntities();

        Hero.Body = new TopDownBody
        {
            IsSolid = IsSolid,
            TileWidth = _level.TileWidth,
            TileHeight = _level.TileHeight
        };
        GameObjects.Add(Hero);
        GameObjects.Add(Dialogue);
        GameObjects.Add(BattleScreen);

        _encounterRate = _level.EncounterRate;
        _encounterTable = _level.Encounters.Where(_monsterBook.Contains).ToList();

        var start = ResolveStart(spawnName);
        Hero.SnapTo(start.Column * _level.TileWidth, start.Row * _level.TileHeight);

        Camera.Target = Hero;
        Camera.MinX = 0;
        Camera.MaxX = _tilemap.PixelWidth;
        Camera.MinY = 0;
        Camera.MaxY = _tilemap.PixelHeight;
        Camera.Update();

        // After the map is up, so a track named by a level that fails to load
        // never starts playing over a broken screen. A map loaded while a fight
        // is on keeps the battle music - the map is only being staged behind it.
        _mapMusic = _level.MusicPath;
        if (Battle is null) Music.Play(_mapMusic);

        // Arriving somewhere doesn't roll for an encounter; the first step does.
        _lastEncounterTile = HeroTile;
        _doorCooldown = DoorCooldownSeconds;
    }

    private (int Column, int Row) ResolveStart(string spawnName)
    {
        if (spawnName != "" && _spawns.TryGetValue(spawnName, out var spawn)) return spawn;
        var playerStart = _level.Entities.FirstOrDefault(e => e.Type == "playerStart");
        return playerStart is not null ? (playerStart.Column, playerStart.Row) : (1, 1);
    }

    private void BuildEntities()
    {
        foreach (var entity in _level.Entities)
        {
            switch (entity.Type)
            {
                case "door":
                    _doors.Add(new Door(entity.Column, entity.Row,
                        entity.Properties.GetValueOrDefault("target", ""),
                        entity.Properties.GetValueOrDefault("spawn", "")));
                    break;

                case "spawnPoint":
                    var name = entity.Properties.GetValueOrDefault("name", "");
                    if (name != "") _spawns[name] = (entity.Column, entity.Row);
                    break;

                case "npc":
                    var sheet = GetSheet("assets/artwork/rpg/folk.png");
                    // Each townsperson is an 8-frame block in the shared sheet.
                    var look = int.TryParse(entity.Properties.GetValueOrDefault("look", "0"), out var l) ? l : 0;
                    var walker = new Walker(sheet, look * 8) { ZIndex = 1 };
                    walker.SnapTo(entity.Column * _level.TileWidth, entity.Row * _level.TileHeight);
                    walker.FaceTowards(0, int.TryParse(entity.Properties.GetValueOrDefault("facing", "1"), out var f) ? f : 1);
                    _folk.Add(new Folk
                    {
                        Walker = walker,
                        Name = entity.Properties.GetValueOrDefault("name", ""),
                        Lines = entity.Properties.GetValueOrDefault("says", "...").Split('|'),
                        Column = entity.Column,
                        Row = entity.Row
                    });
                    // A person is as solid as a wall to walk into - without this
                    // the hero strolls straight through the person they are
                    // talking to, which looks like a bug even though nothing
                    // breaks.
                    _occupied.Add((entity.Column, entity.Row));
                    GameObjects.Add(walker);
                    break;
            }
        }
    }

    public bool IsSolid(int column, int row)
    {
        var rows = _tileTypes.GetLength(0);
        var columns = rows == 0 ? 0 : _tileTypes.GetLength(1);
        // Off the edge of the map is a wall - a map's own border tiles are the
        // intended boundary, this just guarantees one.
        if (column < 0 || column >= columns || row < 0 || row >= rows) return true;
        if (_occupied.Contains((column, row))) return true;
        return _level.Solid.Contains(_tileTypes[row, column]);
    }

    public void Update(float deltaTime)
    {
        if (_doorCooldown > 0f) _doorCooldown -= deltaTime;

        if (Battle is { } battle)
        {
            UpdateBattle(battle);
            return;
        }

        if (Dialogue.IsOpen)
        {
            // Conversation holds the world still: no walking, no re-triggering
            // the doorway underfoot, just paging through what's being said.
            Hero.DirectionX = 0;
            Hero.DirectionY = 0;
            if (InputActions.IsJustPressed(InputAction.Confirm))
            {
                _confirmSound?.Play();
                if (!Dialogue.Advance()) _talkingTo = null;
            }
            if (InputActions.IsJustPressed(InputAction.Cancel))
            {
                _cancelSound?.Play();
                Dialogue.Close();
                _talkingTo = null;
            }
            return;
        }

        ReadWalkInput();

        if (InputActions.IsJustPressed(InputAction.Confirm)) TryTalk();
    }

    /// <summary>
    /// Battle input. Up/Down move the command cursor, Left/Right pick a target,
    /// Confirm commits whatever is in front of you, Cancel steps back out of
    /// target selection. Both cursor calls are safe to make in either phase -
    /// each ignores input meant for the other.
    /// </summary>
    private void UpdateBattle(Battle battle)
    {
        Hero.DirectionX = 0;
        Hero.DirectionY = 0;

        if (battle.Phase == BattlePhase.Over)
        {
            EndBattle(battle);
            return;
        }

        if (InputActions.IsJustPressed(InputAction.MoveUp))
        {
            battle.MoveCommand(-1);
            battle.MoveTarget(-1);
        }
        if (InputActions.IsJustPressed(InputAction.MoveDown))
        {
            battle.MoveCommand(1);
            battle.MoveTarget(1);
        }
        if (InputActions.IsJustPressed(InputAction.MoveLeft)) battle.MoveTarget(-1);
        if (InputActions.IsJustPressed(InputAction.MoveRight)) battle.MoveTarget(1);

        if (InputActions.IsJustPressed(InputAction.Confirm))
        {
            _confirmSound?.Play();
            battle.Confirm();

            if (battle.Outcome == BattleOutcome.Victory && !_victoryPlayed)
            {
                _victoryPlayed = true;
                Music.Play("");
                _victorySound?.Play();
            }
        }
        else if (InputActions.IsJustPressed(InputAction.Cancel))
        {
            _cancelSound?.Play();
            battle.Cancel();
        }
    }

    /// <summary>
    /// Four-way walking: the axes are mutually exclusive, the way a NES-era RPG
    /// moves. Holding two directions keeps the one pressed most recently, so
    /// turning a corner doesn't stall on the frame both keys overlap.
    /// </summary>
    private void ReadWalkInput()
    {
        var left = InputActions.IsPressed(InputAction.MoveLeft);
        var right = InputActions.IsPressed(InputAction.MoveRight);
        var up = InputActions.IsPressed(InputAction.MoveUp);
        var down = InputActions.IsPressed(InputAction.MoveDown);

        var dirX = left && !right ? -1 : right && !left ? 1 : 0;
        var dirY = up && !down ? -1 : down && !up ? 1 : 0;

        if (dirX != 0 && dirY != 0)
        {
            // Whichever axis was newly pressed this frame wins the tie.
            if (InputActions.IsJustPressed(InputAction.MoveLeft) || InputActions.IsJustPressed(InputAction.MoveRight))
                dirY = 0;
            else
                dirX = 0;
        }

        Hero.DirectionX = dirX;
        Hero.DirectionY = dirY;
    }

    /// <summary>
    /// Talks to whoever the hero is facing. Direction-based rather than
    /// proximity-based: standing between two people and pressing Confirm should
    /// address the one you turned toward, not whichever the list happens to hold
    /// first.
    /// </summary>
    private void TryTalk()
    {
        if (Hero.Body is null) return;

        var (column, row) = Hero.Body.CenterTile(Hero.PreciseX, Hero.PreciseY);
        var (stepX, stepY) = Hero.Facing switch
        {
            Facing.Up => (0, -1),
            Facing.Down => (0, 1),
            Facing.Left => (-1, 0),
            _ => (1, 0)
        };

        for (var step = 1; step <= TalkReachTiles; step++)
        {
            var targetColumn = column + stepX * step;
            var targetRow = row + stepY * step;

            var folk = _folk.FirstOrDefault(f => f.Column == targetColumn && f.Row == targetRow);
            if (folk is not null)
            {
                Hero.FaceTowards(stepX, stepY);
                folk.Walker.FaceTowards(-stepX, -stepY);
                _talkingTo = folk;
                _confirmSound?.Play();
                Dialogue.Show(folk.Name, folk.Lines[folk.NextLine]);
                folk.NextLine = (folk.NextLine + 1) % folk.Lines.Length;
                return;
            }

            // Nobody there - reach further only if what's in the way is
            // something to lean over.
            if (!IsSolid(targetColumn, targetRow)) return;
        }
    }

    /// <summary>
    /// Rolls for a random encounter, once per tile the hero steps onto. Runs
    /// after movement alongside CheckDoors. Safe places declare no encounter
    /// rate, so towns and interiors never call the dice at all.
    /// </summary>
    public bool CheckEncounters()
    {
        if (Battle is not null || Dialogue.IsOpen || Hero.Body is null) return false;
        if (_encounterRate <= 0f || _encounterTable.Count == 0) return false;

        var tile = Hero.Body.CenterTile(Hero.PreciseX, Hero.PreciseY);
        if (tile == _lastEncounterTile) return false;
        _lastEncounterTile = tile;

        if (_random.NextDouble() >= _encounterRate) return false;

        StartBattle(_monsterBook.RollGroup(_encounterTable, _random));
        return true;
    }

    /// <summary>Stages a fight against named monsters from the book - how a scripted run gets into a specific battle without walking the road until one turns up.</summary>
    public void StartBattle(IEnumerable<string> monsterKeys) =>
        StartBattle(monsterKeys.Select(key => _monsterBook[key].Spawn()).ToList());

    /// <summary>Drops the hero into a fight with the given group.</summary>
    public void StartBattle(List<Combatant> monsters)
    {
        if (monsters.Count == 0) return;

        Dialogue.Close();
        _talkingTo = null;
        Hero.DirectionX = 0;
        Hero.DirectionY = 0;

        _victoryPlayed = false;
        BattleScreen.Battle = new Battle(Champion, monsters, _random);
        Music.Play(BattleMusicPath);
    }

    private void EndBattle(Battle battle)
    {
        BattleScreen.Battle = null;

        if (battle.Outcome == BattleOutcome.Victory) Experience += battle.ExperienceEarned;

        if (battle.Outcome == BattleOutcome.Defeat)
        {
            // Losing costs you the road rather than the save: you wake back in
            // Ashholt, whole. A harsher rule can come with saving, when there is
            // something to reload.
            Champion.Health = Champion.MaxHealth;
            LoadMap(DefeatMap, DefeatSpawn);
            return;
        }

        // Back to whatever this map plays. The encounter tile is already the one
        // underfoot, so surviving a fight doesn't immediately start another.
        Music.Play(_mapMusic);
        _lastEncounterTile = HeroTile;
    }

    /// <summary>Walking onto a doorway tile loads the map behind it. Runs after movement, so it sees where the hero actually ended up this frame.</summary>
    public bool CheckDoors()
    {
        if (_doorCooldown > 0f || Dialogue.IsOpen || Hero.Body is null) return false;

        var (column, row) = Hero.Body.CenterTile(Hero.PreciseX, Hero.PreciseY);
        var door = _doors.FirstOrDefault(d => d.Column == column && d.Row == row);
        if (door is null) return false;

        _doorSound?.Play();
        LoadMap(door.Target, door.Spawn);
        return true;
    }

    public void Dispose()
    {
        Music.Dispose();
        _confirmSound?.Dispose();
        _cancelSound?.Dispose();
        _doorSound?.Dispose();
        _victorySound?.Dispose();
    }

    private SpriteSheet GetSheet(string path)
    {
        if (_sheets.TryGetValue(path, out var sheet)) return sheet;
        var texture = Texture.CreateImageTexture(_renderer, path);
        sheet = new SpriteSheet(texture, TileSize, TileSize);
        _sheets[path] = sheet;
        return sheet;
    }
}
