using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Physics;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

public enum VoyagePhase
{
    Cruising,
    Warning,
    Collision,
    Sinking,
    Split,
    Sunk
}

/// <summary>
/// Orchestrates the whole Titanic session: the player(s), the currently loaded
/// Room, door-triggered room switching, collision/hazard/death, the tix economy,
/// NPC role takeover, and the scripted sinking timeline. Wired into EventLoop as
/// its beforeUpdate (ground-Z sync) and afterUpdate (everything else) hooks -
/// EventLoop itself stays entirely unaware of any of this.
/// </summary>
public class Game
{
    // The lookout's warning always comes a fixed 30 seconds before impact (so
    // the iceberg's visible approach is equally dramatic however long the
    // cruise lasted). Everything else about the disaster's shape - when the
    // iceberg can strike, how fast she splits and sinks, which ship this even
    // is - lives in the current VoyageConfig.
    private const float WarningBeforeCollisionSeconds = 30f;

    private const float WatcherBonusSeconds = 20f;
    private const float CaptainBonusSeconds = 15f;
    private const float EngineerFloodBonusSeconds = 20f;

    private const float DoorCooldownSeconds = 0.4f;
    private const float DeathFreezeSeconds = 1.6f;
    private const float InteractRadius = 40f;
    private const float PickupRadius = 20f;
    private const int TixPenaltyOnDeath = 50;
    private const int LauncherSellRefund = 300;
    private const int LauncherFireCost = 100;
    private const int LauncherFireCount = 120;
    private const float PocketWatchBonusSeconds = 45f;

    // Item effect tuning. Player.Speed has exactly one writer per frame
    // (UpdatePlayerSpeeds), composing these multipliers - anything else setting
    // Speed directly would silently stack against them.
    private const float BasePlayerSpeed = 120f;
    private const float DeckBootsMultiplier = 1.35f;
    private const float SwimSpeedMultiplier = 0.45f;
    private const float BlanketGraceSeconds = 3f;
    private const float SwimFoamInterval = 0.4f;
    private const float TntIcebergBonusSeconds = 60f;
    private const string TntIconPath = "assets/artwork/titanic-demo/tnt.png";

    // The endgame: once the collision has happened, any player can board a nearby
    // lifeboat (Confirm) and row clear for a modest bonus - or cling to the stern.
    // Either way, actual rescue is the RMS Carpathia: she steams in after the
    // sinking (or early, if someone fires the flare gun), stops off the wreck,
    // and takes aboard everyone who reaches her - lifeboats row to her
    // automatically, swimmers and stern survivors must get within board radius.
    // She waits a fixed boarding window and then departs with whoever made it,
    // which is also what guarantees every session ends.
    private const float LifeboatRowSpeed = 28f;
    private const float LifeboatBoardRadius = 48f;
    private const int LifeboatEscapeBonusTix = 200;
    private const int SternSurvivorBonusTix = 500;
    private const float CarpathiaSummonAfterSunkSeconds = 5f;
    private const float CarpathiaSpawnDistance = 700f;
    private const float CarpathiaSpeed = 45f;
    // She comes alongside off the starboard rail: the stop distance clears the
    // hull's half-width (~180px along the column axis) so she sits in open
    // water, and the board radius reaches back over the starboard side of the
    // stern deck - players must get to the rail, not just exist on the wreck.
    private const float CarpathiaStopDistance = 260f;
    private const float CarpathiaBoardRadius = 110f;
    private const float CarpathiaRowTowardSpeed = 45f;

    // The iceberg starts this many world pixels ahead of the bow (in the ship's
    // direction of travel) once it's sighted, and closes that distance as the
    // voyage clock counts down the rest of the Warning phase - so it's visibly
    // "out there" getting closer, not just an abstract timer.
    private const float IcebergApproachDistance = 550f;

    // (Stern/bow tip tiles - where the wake trail and bow spray spawn from -
    // come from the voyage's hull geometry: _voyage.CenterColumn/SternRow/BowRow.)

    private const float WakeSpawnInterval = 0.5f;
    private const float WakeLifespanSeconds = 5f;
    private const float BowSprayInterval = 0.4f;
    private const float BowSprayLifespanSeconds = 1.2f;
    private const float SmokeSpawnInterval = 0.7f;
    private const float SmokeLifespanSeconds = 2.5f;

    // After striking the iceberg the engines stop and the ship coasts to
    // dead-in-the-water over this many seconds (drift, wake, spray, and smoke all
    // scale down together through the same factor).
    private const float EngineStopSeconds = 4f;

    private const float WaterShimmerInterval = 0.45f;

    // The pre-split upheaval: over the last SplitBulgeSeconds before the ship
    // breaks, the deck rows around the break line visibly hump upward (peaking at
    // SplitBulgeMaxPixels right on the line) as the hull stresses - then the
    // split room load drops both halves back flat, and the waterline eats them.
    private const float SplitBulgeSeconds = 8f;
    private const int SplitBulgeMaxPixels = 26;
    private const int SplitBulgeHalfWidthRows = 9;

    // (Waterline story beats - how far down the hull the sea reaches at the
    // split and at Sunk - are derived per-hull in VoyageConfig.)

    /// <summary>
    /// How many world-pixels the window's shorter edge should show. With 32x16
    /// tiles and a ~64px player this keeps the player around a sixth of the
    /// screen's short side on every display, instead of mapping world pixels 1:1
    /// (which made everything microscopic on HiDPI laptops and phones). Both the
    /// desktop and Android heads feed this to SyncLogicalPresentationToWindow.
    /// </summary>
    public const int TargetViewShortSide = 360;

    /// <summary>
    /// Base depth-sort margin for a standing player (their 32px sprite height),
    /// topped up per-frame in BeforeFrame with however much a raised neighboring
    /// tile "ahead" of them (see PlayerAheadElevationBonus) reaches beyond that -
    /// otherwise a terraced deck's raised edge paints over a character standing at
    /// its foot, since a flat ground tile's own sort key carries no margin for a
    /// dynamic sprite's height the way a raised tile's stacked art visually does.
    /// </summary>
    private const int PlayerBaseSortOffsetY = 32;

    public Camera Camera { get; } = new();
    public List<GameObject> GameObjects { get; } = new();
    public Hud Hud { get; }
    public Room CurrentRoom { get; private set; } = null!;

    private CloudSaveClient? _cloud;

    /// <summary>Optional cloud-save backend (see CloudSaveClient) - null means "cloud sync disabled", and everything that touches it degrades to a no-op. Assigned once by Program.cs/AndroidGame after construction.</summary>
    public CloudSaveClient? Cloud
    {
        get => _cloud;
        set
        {
            if (_cloud is not null) _cloud.Notified -= OnCloudNotified;
            _cloud = value;
            if (_cloud is not null) _cloud.Notified += OnCloudNotified;
        }
    }

    private void OnCloudNotified(string message) => ShowMessage(message, 2.5f);
    public VoyagePhase Phase { get; private set; } = VoyagePhase.Cruising;
    public bool IsGameOver { get; private set; }
    #if DEBUG
    public int TixBalance { get; private set; } = 2100000000;
    #else
    public int TixBalance { get; private set; } = 2300;
    #endif
    public string? CurrentRole { get; private set; }

    // Team-wide shop purchases (per-player ones live in PlayerSession.Inventory).
    public bool FlareGunOwned { get; private set; }
    public bool FlareGunFired { get; private set; }
    private bool _pocketWatchUsed;

    public string DefaultTilesetPath { get; }
    public Dictionary<string, int> DefaultTileFrameIndex { get; }
    public string TixIconPath { get; }
    public string WakeIconPath { get; }
    public string SmokeIconPath { get; }
    public Dictionary<string, (string ImagePath, int Width, int Height)> PropKinds { get; }
    public Dictionary<string, (string ImagePath, int Width, int Height)> NpcKinds { get; }
    public Dictionary<string, string[]> NpcLines { get; }

    private readonly Renderer _renderer;
    private readonly Sound? _doorSound;
    private readonly Sound? _tixSound;
    private readonly Sound? _groanSound;
    private readonly Music? _music;
    private readonly Dictionary<string, Texture> _textureCache = new();
    private readonly Dictionary<(string Path, int Width, int Height), SpriteSheet> _sheetCache = new();
    private readonly List<PlayerSession> _sessions = new();
    private readonly Random _random = new();
    private readonly Dictionary<string, float> _floodDelayBonusByPath = new();

    // Which ship/disaster this session is currently playing, plus campaign
    // progression when running in campaign mode (free play leaves it off).
    private VoyageConfig _voyage = Campaign.FreePlayTitanic;
    private bool _campaignMode;
    private int _voyageIndex;
    private bool _voyageCleared;

    private float _baseCollisionAtSeconds;
    private string? _currentRoleRoomPath;
    private bool _watcherBonusUsed;
    private bool _captainBonusUsed;
    private bool _engineerBonusUsed;
    private bool _hasSplit;
    private float _voyageClock;
    private float _collisionBonusSeconds;
    private float _doorCooldown;
    private string _transientMessage = "";
    private float _transientTimer;

    // Ship drift accumulator - persists across LoadRoom (each visit to the boat
    // deck builds a brand-new Room instance), so the sailed-distance total below
    // is the single source of truth for "how far has the ship gone" this session.
    private float _shipDriftAccumX;
    private float _shipDriftAccumY;
    private int _shipDriftAppliedX;
    private int _shipDriftAppliedY;

    // Iceberg approach - reset to 0 whenever a drifting room is (re)constructed,
    // since a fresh Iceberg sprite starts at its baseline (near-bow) position.
    private int _icebergOffsetAppliedX;
    private int _icebergOffsetAppliedY;

    private readonly List<LaunchedBoat> _launchedBoats = new();
    private string _finalMessage = "";

    // Several interaction checks (NPCs, shop, lifeboats) all key off the same
    // just-pressed Confirm; whichever acts on a session's press first claims it
    // here so one Enter can't e.g. open the shop AND board a lifeboat in the same
    // frame. Cleared at the top of every AfterFrame.
    private readonly HashSet<PlayerSession> _confirmConsumed = new();

    // The purser's shop menu overlay: one shared panel, owned by whichever
    // session opened it (the owner's movement is suspended and their
    // Up/Down/Confirm/Cancel drive the menu until it closes).
    private readonly ShopMenu _shopMenu;
    private PlayerSession? _shopMenuOwner;
    private int _shopMenuIndex;

    // Second HUD line: everyone's carried items, kept off the main status line
    // (single-line Hud, no wrapping - the catalog would run off the screen).
    private readonly Hud _inventoryHud;

    // Placed, still-fizzing TNT charges in the current room. The objects
    // themselves live in the Room (row-anchored, so they drift and can submerge);
    // this list is just Game's fuse watch.
    private readonly List<TntCharge> _tntCharges = new();

    // Set when the flare gun goes up; the rescue-ship logic consumes it to
    // summon the Carpathia ahead of her scheduled post-sinking arrival.
    private bool _flareSummonRequested;

    private readonly RescueShip _rescueShip = new();

    // The stern tip's world position and the ship's heading, cached every frame
    // a drifting room is loaded - the Carpathia's spawn/stop points are derived
    // from these even if she's summoned while everyone is below decks.
    private float _wreckPointX;
    private float _wreckPointY;
    private float _wreckDirX = 1f;
    private float _wreckDirY;

    private readonly List<Particle> _particles = new();
    private float _wakeSpawnTimer;
    private float _bowSprayTimer;
    private float _smokeSpawnTimer;
    private float _shimmerTimer;
    private float _shakeSecondsRemaining;
    private float _shakeAmplitude;

    private class PlayerSession
    {
        public readonly Player Player;
        public readonly IInputSource? InputSource;
        public int LastGoodX;
        public int LastGoodY;
        public (int Column, int Row) EntrySpawnTile;
        public bool IsDying;
        public float DyingTimer;
        public bool Escaped;
        public bool Rescued;
        public readonly PlayerInventory Inventory = new();

        public PlayerSession(Player player, IInputSource? inputSource)
        {
            Player = player;
            InputSource = inputSource;
        }
    }

    /// <summary>
    /// A boarded lifeboat rowing away from the wreck under its own power, carrying
    /// its passenger with it. Detached from the Room (ReleaseFromShip) so ship
    /// drift and row submersion leave it alone; Game moves it each frame instead.
    /// Positions accumulate as floats since GameObject.X/Y are ints and per-frame
    /// movement is fractional.
    /// </summary>
    private class LaunchedBoat
    {
        public readonly Sprite Boat;
        public readonly Player Passenger;
        // Mutable: once the Carpathia is on the water, boats retarget toward her
        // gangway every frame instead of rowing blindly off-map.
        public float VelocityX;
        public float VelocityY;
        public float PreciseX;
        public float PreciseY;

        public LaunchedBoat(Sprite boat, Player passenger, float velocityX, float velocityY)
        {
            Boat = boat;
            Passenger = passenger;
            VelocityX = velocityX;
            VelocityY = velocityY;
            PreciseX = boat.X;
            PreciseY = boat.Y;
        }
    }

    public Game(Renderer renderer, Hud hud, AudioDevice? audio = null)
    {
        _renderer = renderer;
        Hud = hud;
        GameObjects.Add(Hud);

        _inventoryHud = new Hud(renderer, "assets/fonts/Gidole-Regular.ttf", 12,
            new SDL.Color { A = 0, B = 190, G = 185, R = 175 }) { X = 8, Y = 26 };
        GameObjects.Add(_inventoryHud);

        _shopMenu = new ShopMenu(renderer, "assets/fonts/Gidole-Regular.ttf");
        GameObjects.Add(_shopMenu);

        if (audio is not null)
        {
            _doorSound = new Sound(audio, "assets/audio/door-enter.wav");
            _tixSound = new Sound(audio, "assets/audio/tix-pickup.wav") { Volume = 0.6f };
            _groanSound = new Sound(audio, "assets/audio/hull-groan.wav");
            _music = new Music(audio, "assets/audio/titanic-theme.wav") { Volume = 0.45f };
        }

        DefaultTilesetPath = "assets/artwork/isometric-demo/tileset.png";
        DefaultTileFrameIndex = new Dictionary<string, int> { ["grass"] = 0, ["dirt"] = 1, ["water"] = 2, ["stone"] = 3, ["sand"] = 4 };
        TixIconPath = "assets/artwork/titanic-demo/tix-coin.png";
        WakeIconPath = "assets/artwork/titanic-demo/wake-foam.png";
        SmokeIconPath = "assets/artwork/titanic-demo/smoke-puff.png";

        PropKinds = new Dictionary<string, (string, int, int)>
        {
            ["tree"] = ("assets/artwork/isometric-demo/tree.png", 32, 48),
            ["funnel"] = ("assets/artwork/titanic-demo/funnel.png", 32, 88),
            ["iceberg"] = ("assets/artwork/titanic-demo/iceberg.png", 40, 36),
            ["lifeboat"] = ("assets/artwork/titanic-demo/lifeboat.png", 32, 20),
            ["mast"] = ("assets/artwork/titanic-demo/mast.png", 16, 64),
            ["wheel"] = ("assets/artwork/titanic-demo/wheel.png", 24, 32),
            ["boiler"] = ("assets/artwork/titanic-demo/boiler.png", 32, 40),
            ["bed"] = ("assets/artwork/titanic-demo/bed.png", 32, 24),
            ["table"] = ("assets/artwork/titanic-demo/table.png", 32, 24),
            ["crate"] = ("assets/artwork/titanic-demo/crate.png", 24, 24),
            ["shopCounter"] = ("assets/artwork/titanic-demo/shop-counter.png", 32, 28),
            ["hullSide"] = ("assets/artwork/titanic-demo/hull-side.png", 32, 32),
            ["doorway"] = ("assets/artwork/titanic-demo/doorway.png", 24, 36),
            ["carpathia"] = ("assets/artwork/titanic-demo/carpathia.png", 200, 90),
        };

        NpcKinds = new Dictionary<string, (string, int, int)>
        {
            ["captain"] = ("assets/artwork/titanic-demo/captain.png", 16, 32),
            ["engineer"] = ("assets/artwork/titanic-demo/engineer.png", 16, 32),
            ["watcher"] = ("assets/artwork/titanic-demo/watcher.png", 16, 32),
        };

        NpcLines = new Dictionary<string, string[]>
        {
            ["captain"] = new[]
            {
                "\"Steady as she goes. Full speed ahead.\"",
                "\"She's unsinkable, they tell me. We'll see.\"",
                "\"Take the helm if you think you're up to it.\"",
            },
            ["engineer"] = new[]
            {
                "\"Boilers are running hot down here.\"",
                "\"Mind the steam lines - one crack and we're in trouble.\"",
                "\"I can squeeze a little more out of these engines if it comes to that.\"",
            },
            ["watcher"] = new[]
            {
                "\"Cold up here, but it's the best view on the ship.\"",
                "\"Keep your eyes peeled for ice - it's out there somewhere.\"",
                "\"Quietest post on the Titanic, until it isn't.\"",
            },
        };
    }

    public Texture GetTexture(string path)
    {
        if (!_textureCache.TryGetValue(path, out var texture))
        {
            texture = Texture.CreateImageTexture(_renderer, path);
            _textureCache[path] = texture;
        }
        return texture;
    }

    public SpriteSheet GetSheet(string path, int width, int height)
    {
        var key = (path, width, height);
        if (!_sheetCache.TryGetValue(key, out var sheet))
        {
            sheet = new SpriteSheet(GetTexture(path), width, height);
            _sheetCache[key] = sheet;
        }
        return sheet;
    }

    /// <summary>A floating, world-anchored text landmark (see WorldLabel) - Room uses this to mark the shop from across the room.</summary>
    public WorldLabel CreateWorldLabel(string text, int x, int y, SDL.Color color) =>
        new(_renderer, "assets/fonts/Gidole-Regular.ttf", 12, color, text, x, y);

    /// <summary>The rising-floodwater overlay for a platformer room (see WaterOverlay) - Room adds one to its own RoomObjects when built.</summary>
    public WaterOverlay CreateWaterOverlay(Room room) => new(_renderer, room);

    /// <summary>Seconds elapsed since the scripted collision, or -1 before it has happened.</summary>
    public float SecondsSinceCollision() =>
        Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning ? -1f : _voyageClock - CollisionAtSeconds;

    private float CollisionAtSeconds => _baseCollisionAtSeconds + _collisionBonusSeconds;
    private float WarningAtSeconds => CollisionAtSeconds - WarningBeforeCollisionSeconds;

    public float EffectiveFloodDelaySeconds(string path, float baseDelay)
    {
        if (baseDelay < 0f) return -1f;
        return baseDelay + _floodDelayBonusByPath.GetValueOrDefault(path);
    }

    /// <summary>Free play: the classic single Titanic voyage, no persistence. Call once, before running EventLoop.</summary>
    public void Begin(string entryLevelPath, bool coop)
    {
        CreatePlayers(coop);
        ResetVoyage(Campaign.FreePlayTitanic);
        _music?.Play();
    }

    /// <summary>
    /// Campaign mode: starts at the saved voyage with the saved bank and gear,
    /// and persists progress at the end of every voyage. Call once.
    /// </summary>
    public void BeginCampaign(CampaignSave save, bool coop)
    {
        _campaignMode = true;
        _voyageIndex = Math.Clamp(save.VoyageIndex, 0, Campaign.Voyages.Count - 1);

        // Test hook: jump the campaign to a specific voyage for scripted runs.
        if (int.TryParse(Environment.GetEnvironmentVariable("TITANIC_VOYAGE"), out var forcedVoyage))
            _voyageIndex = Math.Clamp(forcedVoyage, 0, Campaign.Voyages.Count - 1);

        CreatePlayers(coop);
        TixBalance = Math.Max(0, save.Bank);
        for (var i = 0; i < _sessions.Count && i < save.Players.Count; i++)
            save.Players[i].ApplyTo(_sessions[i].Inventory);

        ResetVoyage(Campaign.Voyages[_voyageIndex]);
        _music?.Play();
    }

    private void CreatePlayers(bool coop)
    {
        var playerSheet = GetSheet("assets/artwork/isometric-demo/character.png", 16, 32);
        var player1 = new Player(playerSheet) { Speed = BasePlayerSpeed, ZIndex = 1, SortOffsetY = PlayerBaseSortOffsetY, AnimateOnlyWhenMoving = true };
        player1.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));
        _sessions.Add(new PlayerSession(player1, null));
        GameObjects.Add(player1);
        Camera.Target = player1;

        if (coop)
        {
            var bindings = new Dictionary<InputAction, SDL.Scancode[]>
            {
                [InputAction.MoveUp] = new[] { SDL.Scancode.I },
                [InputAction.MoveDown] = new[] { SDL.Scancode.K },
                [InputAction.MoveLeft] = new[] { SDL.Scancode.J },
                [InputAction.MoveRight] = new[] { SDL.Scancode.L },
                [InputAction.Jump] = new[] { SDL.Scancode.O },
                [InputAction.Fire] = new[] { SDL.Scancode.U },
                [InputAction.Confirm] = new[] { SDL.Scancode.P },
                [InputAction.Cancel] = new[] { SDL.Scancode.Semicolon },
            };
            var input2 = new KeyboardInputSource(bindings);
            var player2Sheet = GetSheet("assets/artwork/isometric-demo/character.png", 16, 32);
            var player2 = new Player(player2Sheet, input2) { Speed = BasePlayerSpeed, ZIndex = 1, SortOffsetY = PlayerBaseSortOffsetY, AnimateOnlyWhenMoving = true };
            player2.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));
            _sessions.Add(new PlayerSession(player2, input2));
            GameObjects.Add(player2);

            var anchor = new MidpointAnchor(player1, player2);
            GameObjects.Add(anchor);
            Camera.Target = anchor;
        }

    }

    /// <summary>
    /// Tears down whatever voyage was running and starts this one from its boat
    /// deck: fresh disaster clock and phase, fresh world state (boats, rescue
    /// ship, particles, drift), same players with their banked tix and gear -
    /// the whole point of the campaign is that those carry over.
    /// </summary>
    private void ResetVoyage(VoyageConfig voyage)
    {
        _voyage = voyage;
        _voyageCleared = false;

        // World objects from the previous voyage.
        foreach (var launched in _launchedBoats) GameObjects.Remove(launched.Boat);
        _launchedBoats.Clear();
        RemoveRescueShipSprite();
        _rescueShip.State = RescueShipState.NotSummoned;
        _flareSummonRequested = false;
        foreach (var stale in _particles) GameObjects.Remove(stale);
        _particles.Clear();
        _tntCharges.Clear();
        CloseShopMenu();

        // Disaster clock and phase.
        Phase = VoyagePhase.Cruising;
        IsGameOver = false;
        _finalMessage = "";
        _voyageClock = 0f;
        _collisionBonusSeconds = 0f;
        _hasSplit = false;
        _doorCooldown = 0f;
        _transientTimer = 0f;
        _shakeSecondsRemaining = 0f;
        RollCollisionTime();

        // Role and per-voyage one-shots. A bought-but-unfired flare gun carries
        // over; a fired one was consumed.
        CurrentRole = null;
        _currentRoleRoomPath = null;
        _watcherBonusUsed = _captainBonusUsed = _engineerBonusUsed = false;
        _floodDelayBonusByPath.Clear();
        _pocketWatchUsed = false;
        if (FlareGunFired) FlareGunOwned = false;
        FlareGunFired = false;

        // Ship drift starts from zero on a fresh hull.
        _shipDriftAccumX = _shipDriftAccumY = 0f;
        _shipDriftAppliedX = _shipDriftAppliedY = 0;

        // Players: alive, aboard, ashore of any lifeboat.
        foreach (var session in _sessions)
        {
            session.Escaped = false;
            session.Rescued = false;
            session.IsDying = false;
            session.Player.InputEnabled = true;
            session.Player.Z = 0f;
            session.Inventory.IsSwimming = false;
            session.Inventory.HazardGraceSeconds = 0f;
            session.Inventory.FoodBuffSecondsRemaining = 0f;
            session.Inventory.FoodBuffMultiplier = 1f;
            if (!GameObjects.Contains(session.Player)) GameObjects.Add(session.Player);
        }

        LoadRoom(voyage.ExteriorPath, "");

        var goalText = _campaignMode && voyage.TixGoal > 0 ? $" Goal: bank {voyage.TixGoal} tix." : "";
        ShowMessage($"{voyage.Name} - {voyage.Briefing}{goalText}", 7f);
    }

    private void RollCollisionTime()
    {
        _baseCollisionAtSeconds = _voyage.CollisionMinSeconds
            + (float)_random.NextDouble() * (_voyage.CollisionMaxSeconds - _voyage.CollisionMinSeconds);

        // Test hook: pin the collision time (seconds) for scripted/headless runs,
        // so the whole sinking-to-rescue timeline can be exercised unattended.
        if (float.TryParse(Environment.GetEnvironmentVariable("TITANIC_COLLISION_AT"), out var forcedCollisionAt))
            _baseCollisionAtSeconds = forcedCollisionAt;
    }

    public void LoadRoom(string targetPath, string spawnName)
    {
        if (targetPath == _voyage.ExteriorPath && _hasSplit) targetPath = _voyage.SplitPath;

        // The shop counter (and thus the open menu's subject) doesn't exist in
        // the next room - close before tearing the old room down. Particles and
        // fizzing TNT belong to the room being left behind, too.
        CloseShopMenu();
        foreach (var stale in _particles) GameObjects.Remove(stale);
        _particles.Clear();
        _tntCharges.Clear();

        if (CurrentRoom is not null)
        {
            foreach (var obj in CurrentRoom.RoomObjects) GameObjects.Remove(obj);
        }

        // The ocean/boat-deck levels persist their sailed distance across room
        // reloads (a fresh Room instance is built every time a door is used,
        // including re-entering the boat deck) - other rooms never drift, so they
        // always start at zero regardless of how far the ship has sailed.
        var isDriftingLevel = targetPath == _voyage.ExteriorPath || targetPath == _voyage.SplitPath;

        // Launched lifeboats (and their escaped passengers) live in the open-ocean
        // world - they carry across boat-deck reloads (same world, e.g. the split),
        // but not into interior rooms.
        if (!isDriftingLevel)
        {
            foreach (var launched in _launchedBoats) GameObjects.Remove(launched.Boat);
            _launchedBoats.Clear();
            foreach (var session in _sessions)
            {
                if (session.Escaped) GameObjects.Remove(session.Player);
            }
        }

        var room = isDriftingLevel
            ? new Room(targetPath, this, _shipDriftAppliedX, _shipDriftAppliedY)
            : new Room(targetPath, this);
        CurrentRoom = room;
        GameObjects.AddRange(room.RoomObjects);
        ConfigureRoomPresentation(room);

        // A freshly built Iceberg sprite starts unoffset at its baseline (near-bow)
        // position - reset the tracker so UpdateIcebergApproach computes a clean delta.
        _icebergOffsetAppliedX = 0;
        _icebergOffsetAppliedY = 0;

        // The Carpathia is only visible from the open ocean; her sim state
        // persists across room changes either way.
        if (isDriftingLevel) CreateRescueShipSpriteIfNeeded();
        else RemoveRescueShipSprite();

        (int Column, int Row) spawnTile;
        if (spawnName != "" && room.SpawnPoints.TryGetValue(spawnName, out var found))
        {
            spawnTile = found;
        }
        else
        {
            var playerStart = room.Level.Entities.FirstOrDefault(e => e.Type == "playerStart");
            spawnTile = playerStart is not null ? (playerStart.Column, playerStart.Row) : (0, 0);
        }

        PlaceSessions(spawnTile);

        if (CurrentRole is not null && _currentRoleRoomPath != room.Path)
        {
            CurrentRole = null;
            _currentRoleRoomPath = null;
        }
    }

    /// <summary>
    /// Configures the parts of the world that depend on which perspective the
    /// just-loaded room is: side-view rooms give every player real gravity/tile
    /// collision physics (and an axis-locked scrolling camera matching the
    /// level's ScrollMode); isometric rooms restore the free-follow camera and
    /// strip the physics back off, matching the classic top-down movement.
    /// </summary>
    private void ConfigureRoomPresentation(Room room)
    {
        foreach (var session in _sessions)
        {
            session.Player.Platformer = room.IsPlatformer
                ? new PlatformerBody
                {
                    TileAt = room.KindAt,
                    TileWidth = room.Level.TileWidth,
                    TileHeight = room.Level.TileHeight,
                    MapOriginX = room.FlatMap!.X,
                    MapOriginY = room.FlatMap.Y
                }
                : null;
        }

        if (room.IsPlatformer)
        {
            Camera.Axis = room.Level.ScrollMode.Equals("vertical", StringComparison.OrdinalIgnoreCase)
                ? ScrollAxis.Vertical
                : ScrollAxis.Horizontal;
            Camera.OneWay = room.Level.ScrollMode.Equals("forwardOnly", StringComparison.OrdinalIgnoreCase);
            // Co-op forward-only platformer rooms gate on P1 only - a simplification
            // (campaign content sticks to horizontal/vertical scroll for co-op rooms).
            Camera.LeadingEdgeGate = Camera.OneWay ? _sessions[0].Player : null;
            Camera.MinX = 0;
            Camera.MaxX = room.FlatMap!.PixelWidth;
            Camera.MinY = 0;
            Camera.MaxY = room.FlatMap.PixelHeight;
        }
        else
        {
            Camera.Axis = ScrollAxis.Free;
            Camera.OneWay = false;
            Camera.LeadingEdgeGate = null;
            Camera.MinX = null;
            Camera.MaxX = null;
            Camera.MinY = null;
            Camera.MaxY = null;
        }
    }

    private void PlaceSessions((int Column, int Row) spawnTile)
    {
        // An escaped player is riding a launched lifeboat - never teleport them
        // back onto the ship (e.g. when the split rebuilds the boat deck).
        if (!_sessions[0].Escaped)
        {
            var (x, y) = CurrentRoom.StandOnTile(spawnTile.Column, spawnTile.Row, 16, 32);
            _sessions[0].Player.SnapTo(x, y);
            _sessions[0].Player.Z = 0;
            if (_sessions[0].Player.Platformer is { } body0) body0.VelocityY = 0f;
            _sessions[0].LastGoodX = x;
            _sessions[0].LastGoodY = y;
            _sessions[0].EntrySpawnTile = spawnTile;
        }

        if (_sessions.Count > 1 && !_sessions[1].Escaped)
        {
            var offsetColumn = spawnTile.Column + 1;
            var secondTile = !CurrentRoom.IsSolid(offsetColumn, spawnTile.Row)
                ? (offsetColumn, spawnTile.Row)
                : spawnTile;
            var (x2, y2) = CurrentRoom.StandOnTile(secondTile.Item1, secondTile.Item2, 16, 32);
            _sessions[1].Player.SnapTo(x2, y2);
            _sessions[1].Player.Z = 0;
            if (_sessions[1].Player.Platformer is { } body1) body1.VelocityY = 0f;
            _sessions[1].LastGoodX = x2;
            _sessions[1].LastGoodY = y2;
            _sessions[1].EntrySpawnTile = secondTile;
        }
    }

    /// <summary>
    /// Player.X/Y is the sprite's top-left corner, positioned by StandOnTile so the
    /// sprite's feet sit at the tile's front vertex - not the same point WorldToTile
    /// expects. This inverts that anchor adjustment (for the standard 16x32 player
    /// sprite), and undoes the tilemap's own drift offset (a sailing ship's tiles
    /// move in world space while tile coordinates themselves stay fixed), before
    /// resolving the tile the player is actually standing on.
    /// </summary>
    private (int Column, int Row) TileUnderPlayer(Player player) => TileUnder(player.X, player.Y, 16, 32);

    /// <summary>
    /// Generalized form of the anchor-inversion above - width/height are the
    /// sprite's own footprint (16x32 for both the player and every NpcKind), so
    /// Game.UpdateNpcWander can reuse the same math for NPC wall collision. The
    /// actual isometric-vs-flat math lives on Room, which knows which one it is.
    /// </summary>
    private (int Column, int Row) TileUnder(int x, int y, int width, int height) => CurrentRoom.TileUnder(x, y, width, height);

    /// <summary>
    /// Advances the ship's persisted drift total (if the current room drifts) and
    /// shifts the room plus every player by the incremental delta (position and
    /// last-good-position alike, so collision keeps working relative to the moving
    /// world) - tracked as a float accumulator so fractional per-frame movement
    /// isn't lost to rounding, and as an integer "applied so far" so a fresh Room
    /// instance for the same drifting level can pick up exactly where the last one left off.
    /// </summary>
    /// <summary>
    /// 1 at full steam, easing to 0 over EngineStopSeconds once the iceberg is
    /// struck - the ship coasts to dead-in-the-water instead of implausibly
    /// steaming on while sinking. Everything speed-related (drift, wake, bow
    /// spray, smoke) scales through this one factor.
    /// </summary>
    private float ShipSpeedFactor()
    {
        var sinceCollision = SecondsSinceCollision();
        if (sinceCollision < 0f) return 1f;
        return Math.Clamp(1f - sinceCollision / EngineStopSeconds, 0f, 1f);
    }

    private void AdvanceShipDrift(float deltaTime)
    {
        if (CurrentRoom.Level.DriftSpeedX == 0f && CurrentRoom.Level.DriftSpeedY == 0f) return;

        var speedFactor = ShipSpeedFactor();
        if (speedFactor <= 0f) return;

        _shipDriftAccumX += CurrentRoom.Level.DriftSpeedX * speedFactor * deltaTime;
        _shipDriftAccumY += CurrentRoom.Level.DriftSpeedY * speedFactor * deltaTime;
        var targetX = (int)MathF.Round(_shipDriftAccumX);
        var targetY = (int)MathF.Round(_shipDriftAccumY);
        var deltaX = targetX - _shipDriftAppliedX;
        var deltaY = targetY - _shipDriftAppliedY;
        _shipDriftAppliedX = targetX;
        _shipDriftAppliedY = targetY;
        if (deltaX == 0 && deltaY == 0) return;

        CurrentRoom.ShiftBy(deltaX, deltaY);
        foreach (var session in _sessions)
        {
            session.Player.SnapTo(session.Player.X + deltaX, session.Player.Y + deltaY);
            session.LastGoodX += deltaX;
            session.LastGoodY += deltaY;
        }
    }

    /// <summary>
    /// Advances ship drift, the iceberg's approach, wake/smoke effects, water
    /// shimmer, camera shake, and the sinking waterline, then syncs each player's
    /// GroundZ from the tile they're currently over - all of this must run before
    /// GameObject.Update so movement/gravity this frame use the post-drift world.
    /// </summary>
    public void BeforeFrame(float deltaTime)
    {
        AdvanceShipDrift(deltaTime);
        UpdateIcebergApproach();
        UpdateShipEffects(deltaTime);
        UpdateWaterShimmer(deltaTime);
        UpdateCameraShake(deltaTime);
        UpdateSinkingWaterline();
        UpdateSplitBulge();
        UpdateRescueShip(deltaTime);
        UpdateLaunchedBoats(deltaTime);
        UpdatePlayerSpeeds(deltaTime);
        CurrentRoom.AdvanceWaterLine(deltaTime);

        foreach (var session in _sessions)
        {
            if (session.Escaped) continue;

            if (CurrentRoom.IsPlatformer)
            {
                // Flat rooms never overlap sprites in a meaningful depth order -
                // a fixed offset is enough, unlike the isometric elevation dressing
                // below. PlatformerBody owns height/collision directly; GroundZ
                // (the isometric Z-hop's ground reference) plays no part here.
                session.Player.SortOffsetY = 0;
                if (session.Player.Platformer is { } body)
                {
                    body.WaterLineY = CurrentRoom.WaterLineY;
                    body.Buoyant = session.Inventory.HasLifeJacket || session.Inventory.HazardGraceSeconds > 0f;
                }
                continue;
            }

            var (column, row) = TileUnderPlayer(session.Player);
            var ownElevation = CurrentRoom.GetElevationPixels(column, row);
            session.Player.GroundZ = ownElevation;
            session.Player.SortOffsetY = PlayerBaseSortOffsetY + PlayerAheadElevationBonus(column, row, ownElevation);
        }
    }

    /// <summary>
    /// How much extra sort margin a standing player needs beyond their own tile's
    /// elevation, to clear a taller neighboring tile "ahead" of them (the +column/
    /// +row direction, which depth-sorts after - see IsometricGrid.TileToWorld). A
    /// terraced deck's raised edge stacks its tile art upward via RenderTileStack,
    /// reaching visually past its own footprint into the space where a shorter
    /// neighbor - or a character standing on one - is drawn; a flat tile has no
    /// such reach, so PlayerBaseSortOffsetY alone only covers the flat case.
    /// </summary>
    private int PlayerAheadElevationBonus(int column, int row, int ownElevation)
    {
        var ahead = Math.Max(CurrentRoom.GetElevationPixels(column + 1, row),
            Math.Max(CurrentRoom.GetElevationPixels(column, row + 1),
                CurrentRoom.GetElevationPixels(column + 1, row + 1)));
        return Math.Max(0, ahead - ownElevation);
    }

    private bool IsDriftingRoom() =>
        CurrentRoom.Level.DriftSpeedX != 0f || CurrentRoom.Level.DriftSpeedY != 0f;

    /// <summary>
    /// The Carpathia's whole life: summoned (by flare or a few seconds after the
    /// ship sinks), steams in from beyond the horizon along the wreck's heading,
    /// stops off the stern for a fixed boarding window, then departs - ending the
    /// game with whoever made it aboard. The sim always runs; only the sprite is
    /// tied to having the exterior loaded.
    /// </summary>
    private void UpdateRescueShip(float deltaTime)
    {
        if (IsDriftingRoom())
        {
            var (sternX, sternY) = CurrentRoom.StandOnTile(_voyage.CenterColumn, _voyage.SternRow, 0, 0);
            _wreckPointX = sternX;
            _wreckPointY = sternY;

            // She approaches broadside-on, off the starboard (+column) side of
            // the stern - the same outboard axis launched lifeboats row along -
            // never along the hull, which would park her on the wreck itself.
            // (Drifting rooms are always the isometric exterior hull, never a
            // platformer room, so Grid is guaranteed non-null here.)
            var (originX, originY) = CurrentRoom.Grid!.TileToWorld(0, 0);
            var (stepX, stepY) = CurrentRoom.Grid.TileToWorld(1, 0);
            float axisX = stepX - originX, axisY = stepY - originY;
            var length = MathF.Sqrt(axisX * axisX + axisY * axisY);
            if (length > 0f)
            {
                _wreckDirX = axisX / length;
                _wreckDirY = axisY / length;
            }
        }

        switch (_rescueShip.State)
        {
            case RescueShipState.NotSummoned:
                var scheduled = Phase == VoyagePhase.Sunk &&
                    SecondsSinceCollision() >= _voyage.SunkAfterCollisionSeconds + CarpathiaSummonAfterSunkSeconds;
                if (!_flareSummonRequested && !scheduled) return;
                _flareSummonRequested = false;
                _rescueShip.State = RescueShipState.Steaming;
                _rescueShip.PreciseX = _wreckPointX + _wreckDirX * CarpathiaSpawnDistance;
                _rescueShip.PreciseY = _wreckPointY + _wreckDirY * CarpathiaSpawnDistance;
                ShowMessage("A ship on the horizon - the Carpathia is coming!", 4f);
                CreateRescueShipSpriteIfNeeded();
                break;

            case RescueShipState.Steaming:
                var targetX = _wreckPointX + _wreckDirX * CarpathiaStopDistance;
                var targetY = _wreckPointY + _wreckDirY * CarpathiaStopDistance;
                var toTargetX = targetX - _rescueShip.PreciseX;
                var toTargetY = targetY - _rescueShip.PreciseY;
                var distance = MathF.Sqrt(toTargetX * toTargetX + toTargetY * toTargetY);
                var step = CarpathiaSpeed * deltaTime;
                if (distance <= step)
                {
                    _rescueShip.PreciseX = targetX;
                    _rescueShip.PreciseY = targetY;
                    _rescueShip.State = RescueShipState.Boarding;
                    _rescueShip.BoardingSecondsRemaining = _voyage.BoardingWindowSeconds;
                    ShowMessage($"The Carpathia is alongside - get aboard within {_voyage.BoardingWindowSeconds:0}s!", 5f);
                }
                else
                {
                    _rescueShip.PreciseX += toTargetX / distance * step;
                    _rescueShip.PreciseY += toTargetY / distance * step;
                }
                SyncRescueShipSprite();
                break;

            case RescueShipState.Boarding:
                _rescueShip.BoardingSecondsRemaining -= deltaTime;
                if (_rescueShip.BoardingSecondsRemaining <= 0f)
                {
                    _rescueShip.State = RescueShipState.Departed;
                    if (!IsGameOver)
                    {
                        var rescued = _sessions.Count(s => s.Rescued);
                        EndGame($"The Carpathia departs... {rescued} of {_sessions.Count} rescued. Final tix: {TixBalance}.");
                    }
                }
                break;

            case RescueShipState.Departed:
                // She steams back out the way she came - a slow exit for the tableau.
                _rescueShip.PreciseX += _wreckDirX * CarpathiaSpeed * deltaTime;
                _rescueShip.PreciseY += _wreckDirY * CarpathiaSpeed * deltaTime;
                SyncRescueShipSprite();
                break;
        }
    }

    /// <summary>Builds the Carpathia's sprite for the current (drifting) room - the sim position is authoritative, the sprite is just its view.</summary>
    private void CreateRescueShipSpriteIfNeeded()
    {
        if (_rescueShip.State == RescueShipState.NotSummoned) return;
        if (!IsDriftingRoom()) return;
        if (_rescueShip.Sprite is null)
        {
            var (path, width, height) = PropKinds["carpathia"];
            var sheet = GetSheet(path, width, height);
            // Afloat on open water: like launched boats, the sort footprint must
            // clear the flat water tiles around the hull or they'd clip it.
            _rescueShip.Sprite = new Sprite(sheet) { ZIndex = 1, SortOffsetY = height + CurrentRoom.Level.TileHeight };
            GameObjects.Add(_rescueShip.Sprite);
        }
        SyncRescueShipSprite();
    }

    private void RemoveRescueShipSprite()
    {
        if (_rescueShip.Sprite is null) return;
        GameObjects.Remove(_rescueShip.Sprite);
        _rescueShip.Sprite = null;
    }

    private void SyncRescueShipSprite()
    {
        if (_rescueShip.Sprite is null) return;
        var (_, width, height) = PropKinds["carpathia"];
        // PreciseX/Y is the gangway (center-bottom of the hull, slightly above
        // the waterline art) - lay the sprite out around it.
        _rescueShip.Sprite.X = (int)MathF.Round(_rescueShip.PreciseX - width / 2f);
        _rescueShip.Sprite.Y = (int)MathF.Round(_rescueShip.PreciseY - height + 12);
    }

    /// <summary>
    /// The single writer of Player.Speed: base speed times deck boots, times any
    /// active snack buff (whose timer counts down here), times the swim penalty
    /// while a life jacket or blanket is keeping the player alive in the water.
    /// </summary>
    private void UpdatePlayerSpeeds(float deltaTime)
    {
        foreach (var session in _sessions)
        {
            var inventory = session.Inventory;
            if (inventory.FoodBuffSecondsRemaining > 0f)
            {
                inventory.FoodBuffSecondsRemaining -= deltaTime;
                if (inventory.FoodBuffSecondsRemaining <= 0f) inventory.FoodBuffMultiplier = 1f;
            }

            var speed = BasePlayerSpeed * inventory.FoodBuffMultiplier;
            if (inventory.HasDeckBoots) speed *= DeckBootsMultiplier;
            if (inventory.IsSwimming) speed *= SwimSpeedMultiplier;
            session.Player.Speed = speed;
        }
    }

    /// <summary>
    /// Rows every launched lifeboat (and its passenger): away from the wreck at
    /// first, then curving toward the Carpathia's gangway once she's on the
    /// water - and aboard her (rescued, boat and passenger leave the world) when
    /// they reach it during the boarding window.
    /// </summary>
    private void UpdateLaunchedBoats(float deltaTime)
    {
        for (var i = _launchedBoats.Count - 1; i >= 0; i--)
        {
            var launched = _launchedBoats[i];

            if (_rescueShip.State is RescueShipState.Steaming or RescueShipState.Boarding)
            {
                var toShipX = _rescueShip.PreciseX - launched.PreciseX;
                var toShipY = _rescueShip.PreciseY - launched.PreciseY;
                var distance = MathF.Sqrt(toShipX * toShipX + toShipY * toShipY);
                if (distance > 1f)
                {
                    launched.VelocityX = toShipX / distance * CarpathiaRowTowardSpeed;
                    launched.VelocityY = toShipY / distance * CarpathiaRowTowardSpeed;
                }

                if (_rescueShip.State == RescueShipState.Boarding && distance <= CarpathiaBoardRadius && !IsGameOver)
                {
                    var session = _sessions.FirstOrDefault(s => s.Player == launched.Passenger);
                    GameObjects.Remove(launched.Boat);
                    GameObjects.Remove(launched.Passenger);
                    _launchedBoats.RemoveAt(i);
                    if (session is not null) RescueSession(session);
                    continue;
                }
            }

            launched.PreciseX += launched.VelocityX * deltaTime;
            launched.PreciseY += launched.VelocityY * deltaTime;
            launched.Boat.X = (int)MathF.Round(launched.PreciseX);
            launched.Boat.Y = (int)MathF.Round(launched.PreciseY);
            // seated amidships: horizontally centered in the 32-wide boat, feet just above its stern seat
            launched.Passenger.SnapTo(launched.Boat.X + 8, launched.Boat.Y - 12);
        }
    }

    /// <summary>
    /// Rescue by one's own feet (or life-jacket swim): any player who gets within
    /// board radius of the gangway while the Carpathia is alongside is taken
    /// aboard - no button needed, the crew hauls them up.
    /// </summary>
    private void CheckCarpathiaBoarding()
    {
        if (_rescueShip.State != RescueShipState.Boarding) return;
        if (!IsDriftingRoom()) return;

        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            var dx = session.Player.X + 8 - _rescueShip.PreciseX;
            var dy = session.Player.Y + 16 - _rescueShip.PreciseY;
            if (MathF.Sqrt(dx * dx + dy * dy) > CarpathiaBoardRadius) continue;
            GameObjects.Remove(session.Player);
            RescueSession(session);
        }
    }

    private void RescueSession(PlayerSession session)
    {
        if (session.Rescued) return;
        if (_shopMenuOwner == session) CloseShopMenu();

        session.Rescued = true;
        session.Escaped = true;
        session.Player.InputEnabled = false;
        TixBalance += SternSurvivorBonusTix;

        var who = _sessions.Count > 1 ? $"P{_sessions.IndexOf(session) + 1} is" : "You are";
        ShowMessage($"{who} aboard the Carpathia! +{SternSurvivorBonusTix} tix.", 3f);

        if (_sessions.All(s => s.Rescued))
            EndGame($"Everyone rescued by RMS Carpathia! Final tix: {TixBalance}.");
    }

    /// <summary>Cycles the tilemap's water variants on a timer so the whole ocean shimmers.</summary>
    private void UpdateWaterShimmer(float deltaTime)
    {
        _shimmerTimer -= deltaTime;
        if (_shimmerTimer > 0f) return;
        _shimmerTimer = WaterShimmerInterval;
        if (CurrentRoom.Tilemap is { } tilemap) tilemap.AnimationPhase++;
    }

    private void StartShake(float amplitude, float seconds)
    {
        _shakeAmplitude = amplitude;
        _shakeSecondsRemaining = seconds;
    }

    /// <summary>Random jitter that decays to nothing - the physical punch of impact/split.</summary>
    private void UpdateCameraShake(float deltaTime)
    {
        if (_shakeSecondsRemaining <= 0f)
        {
            Camera.ShakeX = 0f;
            Camera.ShakeY = 0f;
            return;
        }

        _shakeSecondsRemaining -= deltaTime;
        var falloff = Math.Max(0f, _shakeSecondsRemaining);
        Camera.ShakeX = ((float)_random.NextDouble() * 2f - 1f) * _shakeAmplitude * falloff;
        Camera.ShakeY = ((float)_random.NextDouble() * 2f - 1f) * _shakeAmplitude * falloff;
    }

    /// <summary>
    /// While sinking, marches the waterline aft over the exterior hull: by the
    /// split, everything forward of the break is awash; by Sunk, only the last few
    /// stern rows remain above water (the classic final refuge). Runs every frame
    /// against the current room, so re-entering the deck mid-sinking immediately
    /// shows the right waterline; interior rooms use the separate all-at-once
    /// flooding and are untouched by this.
    /// </summary>
    private void UpdateSinkingWaterline()
    {
        if (CurrentRoom.Level.DriftSpeedX == 0f && CurrentRoom.Level.DriftSpeedY == 0f) return;

        var sinceCollision = SecondsSinceCollision();
        if (sinceCollision <= 0f) return;

        int waterlineRow;
        if (Phase == VoyagePhase.Collision || Phase == VoyagePhase.Sinking)
        {
            var progress = Math.Clamp(sinceCollision / _voyage.SplitAfterCollisionSeconds, 0f, 1f);
            waterlineRow = (int)(progress * _voyage.WaterlineRowAtSplit);
        }
        else if (Phase == VoyagePhase.Split || Phase == VoyagePhase.Sunk)
        {
            var sinceSplit = sinceCollision - _voyage.SplitAfterCollisionSeconds;
            var span = _voyage.SunkAfterCollisionSeconds - _voyage.SplitAfterCollisionSeconds;
            var progress = span > 0f ? Math.Clamp(sinceSplit / span, 0f, 1f) : 1f;
            waterlineRow = _voyage.WaterlineRowAtSplit + (int)(progress * (_voyage.WaterlineRowAtSunk - _voyage.WaterlineRowAtSplit));
        }
        else
        {
            return;
        }

        var removed = CurrentRoom.SubmergeRowsThrough(waterlineRow, "water");
        foreach (var obj in removed) GameObjects.Remove(obj);
    }

    /// <summary>
    /// Ramps the pre-split deck bulge from nothing to full over the final
    /// SplitBulgeSeconds of the Sinking phase - the hull visibly humping upward
    /// around the coming break line before the ship snaps in two. Only the
    /// drifting exterior deck participates; interior rooms never see this.
    /// </summary>
    private void UpdateSplitBulge()
    {
        if (CurrentRoom.Level.DriftSpeedX == 0f && CurrentRoom.Level.DriftSpeedY == 0f) return;
        if (Phase != VoyagePhase.Sinking) return;

        var start = _voyage.SplitAfterCollisionSeconds - SplitBulgeSeconds;
        var sinceCollision = SecondsSinceCollision();
        if (sinceCollision <= start) return;

        var strength = Math.Clamp((sinceCollision - start) / SplitBulgeSeconds, 0f, 1f);
        CurrentRoom.ApplySplitBulge(_voyage.SplitMidRow, SplitBulgeHalfWidthRows, SplitBulgeMaxPixels, strength);
    }

    /// <summary>
    /// 1 when the iceberg should sit at its farthest point (not yet sighted, or
    /// just sighted at the start of Warning), shrinking to 0 exactly as Collision
    /// triggers. Deliberately keyed off the same CollisionAtSeconds the phase
    /// transition itself uses, so a Watcher/Captain bonus that pushes Collision
    /// back also visibly slows the iceberg's approach - one source of truth.
    /// </summary>
    private float IcebergRemainingFraction()
    {
        if (Phase == VoyagePhase.Cruising) return 1f;
        if (Phase != VoyagePhase.Warning) return 0f;

        var span = CollisionAtSeconds - WarningAtSeconds;
        if (span <= 0f) return 0f;
        return Math.Clamp((CollisionAtSeconds - _voyageClock) / span, 0f, 1f);
    }

    /// <summary>
    /// Nudges the iceberg (on top of the normal ship-relative drift every
    /// RoomObject already gets) so it visibly closes the distance from
    /// IcebergApproachDistance away down to right at the bow as Collision nears.
    /// </summary>
    private void UpdateIcebergApproach()
    {
        var iceberg = CurrentRoom.Iceberg;
        if (iceberg is null) return;

        var driftSpeedX = CurrentRoom.Level.DriftSpeedX;
        var driftSpeedY = CurrentRoom.Level.DriftSpeedY;
        var speed = MathF.Sqrt(driftSpeedX * driftSpeedX + driftSpeedY * driftSpeedY);
        if (speed <= 0f) return;

        var directionX = driftSpeedX / speed;
        var directionY = driftSpeedY / speed;
        var fraction = IcebergRemainingFraction();

        var targetX = (int)MathF.Round(directionX * IcebergApproachDistance * fraction);
        var targetY = (int)MathF.Round(directionY * IcebergApproachDistance * fraction);
        var deltaX = targetX - _icebergOffsetAppliedX;
        var deltaY = targetY - _icebergOffsetAppliedY;
        if (deltaX == 0 && deltaY == 0) return;

        _icebergOffsetAppliedX = targetX;
        _icebergOffsetAppliedY = targetY;
        iceberg.X += deltaX;
        iceberg.Y += deltaY;
    }

    /// <summary>
    /// Spawns a fixed (non-drifting) wake-foam trail behind the stern, spray at the
    /// bow, and backward-drifting smoke puffs from the funnels while under way, and
    /// expires old particles - the visible "we are actually moving" cues to go with
    /// the iceberg's approach. All gated on ShipSpeedFactor, so they die out
    /// together as the ship coasts to a stop after impact. Only active on a
    /// drifting level; clears out immediately otherwise (stepping inside
    /// inherently stops them).
    /// </summary>
    private void UpdateShipEffects(float deltaTime)
    {
        var driftSpeedX = CurrentRoom.Level.DriftSpeedX;
        var driftSpeedY = CurrentRoom.Level.DriftSpeedY;
        var speedFactor = ShipSpeedFactor();
        // (Interior rooms spawn no ship effects - drift is zero - but other
        // particles, e.g. TNT smoke and swim foam, still live and expire here;
        // LoadRoom clears the list wholesale on every room change.)
        if ((driftSpeedX != 0f || driftSpeedY != 0f) && speedFactor > 0.05f)
        {
            _wakeSpawnTimer -= deltaTime;
            if (_wakeSpawnTimer <= 0f)
            {
                _wakeSpawnTimer = WakeSpawnInterval / speedFactor;
                var (wakeX, wakeY) = CurrentRoom.StandOnTile(_voyage.CenterColumn, _voyage.SternRow, 16, 8);
                var wakeSheet = GetSheet(WakeIconPath, 16, 8);
                // SortOffsetY must exceed a tile's own depth-sort height (TileHeight,
                // see IsometricTilemap) or the tile this sits on always paints over it.
                var wake = new Particle(wakeSheet, WakeLifespanSeconds) { X = wakeX, Y = wakeY, SortOffsetY = CurrentRoom.Level.TileHeight };
                _particles.Add(wake);
                GameObjects.Add(wake);
            }

            _bowSprayTimer -= deltaTime;
            if (_bowSprayTimer <= 0f)
            {
                _bowSprayTimer = BowSprayInterval / speedFactor;
                var (sprayX, sprayY) = CurrentRoom.StandOnTile(_voyage.CenterColumn, _voyage.BowRow, 16, 8);
                var spraySheet = GetSheet(WakeIconPath, 16, 8);
                // small sideways scatter so the spray reads as splashing off the bow, not a fixed dot
                var spray = new Particle(spraySheet, BowSprayLifespanSeconds)
                {
                    X = sprayX + _random.Next(-10, 11),
                    Y = sprayY + _random.Next(-4, 5),
                    SortOffsetY = CurrentRoom.Level.TileHeight
                };
                _particles.Add(spray);
                GameObjects.Add(spray);
            }

            if (CurrentRoom.Funnels.Count > 0)
            {
                _smokeSpawnTimer -= deltaTime;
                if (_smokeSpawnTimer <= 0f)
                {
                    _smokeSpawnTimer = SmokeSpawnInterval / speedFactor;
                    var funnel = CurrentRoom.Funnels[_random.Next(CurrentRoom.Funnels.Count)];
                    var smokeSheet = GetSheet(SmokeIconPath, 16, 16);
                    var speed = MathF.Sqrt(driftSpeedX * driftSpeedX + driftSpeedY * driftSpeedY);
                    var (_, funnelWidth, _) = PropKinds["funnel"];
                    var smoke = new Particle(smokeSheet, SmokeLifespanSeconds)
                    {
                        X = funnel.X + (funnelWidth - 16) / 2,
                        Y = funnel.Y - 8,
                        ZIndex = 3,
                        // Sorts alongside the funnel itself (its own SortOffsetY -
                        // which includes any deck elevation it stands on - plus the
                        // 8px this spawns above it), so nothing standing in front of
                        // the funnel can hide smoke rising above it. Height is
                        // climbed via Z (RiseSpeed), not world Y, so the rise never
                        // corrupts the depth sort either.
                        SortOffsetY = funnel.SortOffsetY + 8,
                        VelocityX = speed > 0f ? -driftSpeedX / speed * 10f : 0f,
                        VelocityY = speed > 0f ? -driftSpeedY / speed * 10f : 0f,
                        RiseSpeed = 16f
                    };
                    _particles.Add(smoke);
                    GameObjects.Add(smoke);
                }
            }
        }

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            if (_particles[i].IsExpired)
            {
                GameObjects.Remove(_particles[i]);
                _particles.RemoveAt(i);
            }
        }
    }

    public void AfterFrame(float deltaTime)
    {
        _confirmConsumed.Clear();
        UpdateVoyageClock(deltaTime);

        // After the game has been decided, the world keeps sinking/rowing for the
        // final tableau, but nothing gameplay-relevant happens to anyone anymore.
        if (!IsGameOver)
        {
            foreach (var session in _sessions)
            {
                if (session.Escaped) continue;

                if (session.IsDying)
                {
                    session.DyingTimer -= deltaTime;
                    if (session.DyingTimer <= 0f) Respawn(session);
                    continue;
                }

                var inventory = session.Inventory;
                if (inventory.HazardGraceSeconds > 0f) inventory.HazardGraceSeconds -= deltaTime;

                if (CurrentRoom.IsPlatformer)
                {
                    UpdatePlatformerHazard(session, deltaTime);
                    continue;
                }

                var (column, row) = TileUnderPlayer(session.Player);

                if (CurrentRoom.IsSolid(column, row))
                {
                    session.Player.SnapTo(session.LastGoodX, session.LastGoodY);
                }
                else
                {
                    session.LastGoodX = session.Player.X;
                    session.LastGoodY = session.Player.Y;

                    if (CurrentRoom.TryGetHazard(column, row, out var hazard) && session.Player.IsGrounded)
                    {
                        // A life jacket turns lethal water into slow swimming for
                        // good; a blanket's grace window does the same for a few
                        // seconds after it saved this player (see TriggerDeath).
                        if (inventory.HasLifeJacket || inventory.HazardGraceSeconds > 0f)
                        {
                            inventory.IsSwimming = true;
                            SpawnSwimFoam(session, deltaTime);
                        }
                        else
                        {
                            TriggerDeath(session, hazard);
                        }
                    }
                    else
                    {
                        inventory.IsSwimming = false;
                    }
                }
            }

            UpdateNpcWander();
            UpdateShopMenu();
            UpdateTntCharges();

            if (_doorCooldown > 0f) _doorCooldown -= deltaTime;
            else CheckDoors();

            CheckPickups();
            CheckNpcInteractionAndRoleBonus();
            CheckShop();
            CheckFireAction();
            CheckLifeboats();
            CheckCarpathiaBoarding();
        }

        CheckVoyageAdvance();
        UpdateHudText(deltaTime);
    }

    private void UpdateVoyageClock(float deltaTime)
    {
        _voyageClock += deltaTime;

        switch (Phase)
        {
            case VoyagePhase.Cruising:
                if (_voyageClock >= WarningAtSeconds)
                {
                    Phase = VoyagePhase.Warning;
                    ShowMessage("Lookout: \"Iceberg, right ahead!\"", 4f);
                }
                break;

            case VoyagePhase.Warning:
                if (_voyageClock >= CollisionAtSeconds)
                {
                    Phase = VoyagePhase.Collision;
                    ShowMessage("The ship has struck an iceberg!", 4f);
                    StartShake(amplitude: 7f, seconds: 1.2f);
                    _groanSound?.Play();
                }
                break;

            case VoyagePhase.Collision:
                Phase = VoyagePhase.Sinking;
                ShowMessage("Water is rising below decks - get to a lifeboat! (Enter to board)", 5f);
                ApplyFloodIfDueForCurrentRoom();
                break;

            case VoyagePhase.Sinking:
                ApplyFloodIfDueForCurrentRoom();
                if (SecondsSinceCollision() >= _voyage.SplitAfterCollisionSeconds)
                {
                    Phase = VoyagePhase.Split;
                    ShowMessage("The ship has split in two!", 4f);
                    StartShake(amplitude: 9f, seconds: 1.6f);
                    _groanSound?.Play();
                    TriggerSplit();
                }
                break;

            case VoyagePhase.Split:
                ApplyFloodIfDueForCurrentRoom();
                if (SecondsSinceCollision() >= _voyage.SunkAfterCollisionSeconds)
                {
                    Phase = VoyagePhase.Sunk;
                    ShowMessage("The ship has gone down. Survive the wreck.", 6f);
                    // An unfired flare is never wasted - it goes up as the ship does down.
                    if (FlareGunOwned && !FlareGunFired) FireFlareGun();
                }
                break;

            case VoyagePhase.Sunk:
                ApplyFloodIfDueForCurrentRoom();
                // Rescue is no longer an instant payout here - the Carpathia
                // physically arrives and must be boarded (see UpdateRescueShip).
                break;
        }
    }

    private void TriggerSplit()
    {
        _hasSplit = true;
        if (CurrentRoom.Path != _voyage.ExteriorPath) return;

        // Always the stern half: by the moment the ship breaks, the sinking
        // waterline has already put the entire forward half awash, so anyone
        // still alive scrambles aft with the upheaval.
        LoadRoom(_voyage.SplitPath, "aftHalf");
    }

    private void ApplyFloodIfDueForCurrentRoom()
    {
        if (CurrentRoom.HasFlooded) return;
        var delay = EffectiveFloodDelaySeconds(CurrentRoom.Path, CurrentRoom.Level.FloodDelaySeconds);
        if (delay < 0f) return;
        if (SecondsSinceCollision() >= delay) CurrentRoom.ApplyFlood();
    }

    private void CheckDoors()
    {
        foreach (var session in _sessions)
        {
            if (session.Escaped) continue;
            var (column, row) = TileUnderPlayer(session.Player);
            var door = CurrentRoom.Doors.FirstOrDefault(d => d.Column == column && d.Row == row);
            if (door is not null)
            {
                _doorSound?.Play();
                LoadRoom(door.Target, door.Spawn);
                _doorCooldown = DoorCooldownSeconds;
                return;
            }
        }
    }

    private void CheckPickups()
    {
        List<TixPickup>? collected = null;
        foreach (var pickup in CurrentRoom.TixPickups)
        {
            if (pickup is LaunchedTix { Landed: false }) continue;

            foreach (var session in _sessions)
            {
                if (session.Escaped) continue;
                if (Distance(session.Player, pickup) <= PickupRadius)
                {
                    TixBalance += pickup.Value;
                    (collected ??= new List<TixPickup>()).Add(pickup);
                    break;
                }
            }
        }

        if (collected is null) return;
        _tixSound?.Play();
        foreach (var pickup in collected)
        {
            CurrentRoom.TixPickups.Remove(pickup);
            CurrentRoom.RoomObjects.Remove(pickup);
            GameObjects.Remove(pickup);
        }
    }

    /// <summary>
    /// Wall collision for wandering NPCs: same trial-move-then-revert as player
    /// movement (Npc.Update already picked a direction and moved this frame; this
    /// just undoes it if that landed on a solid tile), since Npc itself has no
    /// notion of the Room it's standing in.
    /// </summary>
    private void UpdateNpcWander()
    {
        foreach (var npc in CurrentRoom.Npcs)
        {
            var (column, row) = TileUnder(npc.X, npc.Y, 16, 32);
            if (CurrentRoom.IsSolid(column, row))
            {
                npc.SnapTo(npc.LastGoodX, npc.LastGoodY);
            }
            else
            {
                npc.LastGoodX = npc.X;
                npc.LastGoodY = npc.Y;
            }
        }
    }

    private void CheckNpcInteractionAndRoleBonus()
    {
        if (CurrentRole is null)
        {
            foreach (var session in _sessions)
            {
                if (session.Escaped) continue;
                var npc = CurrentRoom.Npcs.FirstOrDefault(n => Distance(session.Player, n) <= InteractRadius);
                if (npc is not null && ConfirmAvailable(session))
                {
                    ConsumeConfirm(session);
                    CurrentRole = npc.Role;
                    _currentRoleRoomPath = CurrentRoom.Path;
                    var line = npc.NextLine();
                    CurrentRoom.Npcs.Remove(npc);
                    CurrentRoom.RoomObjects.Remove(npc);
                    GameObjects.Remove(npc);
                    // The dialogue line does the talking; the persistent "Role: X"
                    // HUD segment already confirms the takeover, so this stays a
                    // spoken line rather than a status readout doubling up on it.
                    ShowMessage($"{Capitalize(npc.Role)}: {line}", 3.5f);
                    return;
                }
            }
        }
        else
        {
            foreach (var session in _sessions)
            {
                if (session.Escaped) continue;
                if (ConfirmAvailable(session) && TryApplyRoleBonus())
                {
                    ConsumeConfirm(session);
                    return;
                }
            }
        }
    }

    private bool TryApplyRoleBonus()
    {
        var tooLate = Phase == VoyagePhase.Collision || Phase == VoyagePhase.Sinking ||
                      Phase == VoyagePhase.Split || Phase == VoyagePhase.Sunk;

        switch (CurrentRole)
        {
            case "watcher":
                if (_watcherBonusUsed || tooLate) return false;
                _watcherBonusUsed = true;
                _collisionBonusSeconds += WatcherBonusSeconds;
                ShowMessage("Iceberg reported early - a little more time before impact.", 3f);
                return true;

            case "captain":
                if (_captainBonusUsed || tooLate) return false;
                _captainBonusUsed = true;
                _collisionBonusSeconds += CaptainBonusSeconds;
                ShowMessage("Hard-a-starboard! Impact delayed a little longer.", 3f);
                return true;

            case "engineer":
                if (_engineerBonusUsed || Phase == VoyagePhase.Cruising) return false;
                _engineerBonusUsed = true;
                _floodDelayBonusByPath[_currentRoleRoomPath!] = EngineerFloodBonusSeconds;
                ShowMessage("Pumps running hard - this compartment will hold a little longer.", 3f);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Distance from this session's player to the current room's shop counter
    /// (null if the room has none), measured the same way as CheckShop/ShopHint so
    /// both agree on when a player is "at" the shop.
    /// </summary>
    private float? DistanceToShop(PlayerSession session)
    {
        if (CurrentRoom.ShopTile is null) return null;
        var (shopColumn, shopRow) = CurrentRoom.ShopTile.Value;
        var (originX, originY) = CurrentRoom.TileOrigin(shopColumn, shopRow);
        var shopX = originX + CurrentRoom.Level.TileWidth / 2f;
        var shopY = originY + CurrentRoom.Level.TileHeight;
        var dx = session.Player.X - shopX;
        var dy = session.Player.Y - shopY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// The purser's office shop: Confirm at the counter opens the shop menu for
    /// that player (one shared panel - a second player pressing Confirm while
    /// it's open just gets told the purser is busy).
    /// </summary>
    private void CheckShop()
    {
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            var distance = DistanceToShop(session);
            if (distance is null || distance > InteractRadius) continue;
            if (!ConfirmAvailable(session)) continue;
            ConsumeConfirm(session);

            if (_shopMenuOwner is not null)
            {
                if (_shopMenuOwner != session)
                    ShowMessage("The purser is busy with the other passenger.", 2f);
                continue;
            }

            OpenShopMenu(session);
        }
    }

    private void OpenShopMenu(PlayerSession session)
    {
        _shopMenuOwner = session;
        _shopMenuIndex = 0;
        // Movement is suspended while browsing (Up/Down navigate the menu instead) -
        // same suspension mechanism the death freeze uses.
        session.Player.InputEnabled = false;
        _shopMenu.IsVisible = true;
        RefreshShopMenu();
    }

    private void CloseShopMenu()
    {
        if (_shopMenuOwner is null) return;
        // Death and escape also disable input and must keep it disabled - only an
        // ordinary browse-and-close hands movement back.
        if (!_shopMenuOwner.IsDying && !_shopMenuOwner.Escaped)
            _shopMenuOwner.Player.InputEnabled = true;
        _shopMenuOwner = null;
        _shopMenu.IsVisible = false;
    }

    /// <summary>
    /// Drives the open shop menu from its owner's input: Up/Down move the
    /// selection, Confirm buys (or closes, on the Close row), Cancel closes.
    /// Runs before the other Confirm interactions each frame, so a menu Confirm
    /// can never simultaneously talk to an NPC or board a lifeboat.
    /// </summary>
    private void UpdateShopMenu()
    {
        if (_shopMenuOwner is null) return;
        var owner = _shopMenuOwner;

        if (owner.IsDying || owner.Escaped)
        {
            CloseShopMenu();
            return;
        }

        if (SessionJustPressed(owner, InputAction.Cancel))
        {
            CloseShopMenu();
            return;
        }

        var items = VisibleShopItems();
        var rowCount = items.Count + 1; // + the Close row
        if (_shopMenuIndex >= rowCount) _shopMenuIndex = rowCount - 1;

        if (SessionJustPressed(owner, InputAction.MoveUp))
            _shopMenuIndex = (_shopMenuIndex - 1 + rowCount) % rowCount;
        if (SessionJustPressed(owner, InputAction.MoveDown))
            _shopMenuIndex = (_shopMenuIndex + 1) % rowCount;

        if (ConfirmAvailable(owner))
        {
            ConsumeConfirm(owner);
            if (_shopMenuIndex == items.Count)
            {
                CloseShopMenu();
                return;
            }
            TryPurchase(items[_shopMenuIndex], owner);
        }

        RefreshShopMenu();
    }

    /// <summary>
    /// The catalog minus items that no longer make sense to sell: the pocket
    /// watch once the collision has happened (or one was already bought - its
    /// effect is instant and once per voyage), and the flare gun once the team
    /// owns or has fired one.
    /// </summary>
    private List<ShopItem> VisibleShopItems()
    {
        var beforeCollision = Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning;
        return ShopCatalog.Items.Where(item => item.Kind switch
        {
            ShopItemKind.PocketWatch => beforeCollision && !_pocketWatchUsed,
            ShopItemKind.FlareGun => !FlareGunOwned && !FlareGunFired,
            _ => true
        }).ToList();
    }

    private void RefreshShopMenu()
    {
        if (_shopMenuOwner is null) return;
        var inventory = _shopMenuOwner.Inventory;
        var items = VisibleShopItems();

        var rows = new List<string>();
        foreach (var item in items)
        {
            var label = item.Kind switch
            {
                ShopItemKind.TixLauncher when inventory.HasTixLauncher => $"Sell Tix Launcher  +{LauncherSellRefund}",
                ShopItemKind.LifeJacket when inventory.HasLifeJacket => $"{item.Name}  {item.Price}  (owned)",
                ShopItemKind.DeckBoots when inventory.HasDeckBoots => $"{item.Name}  {item.Price}  (owned)",
                ShopItemKind.Blanket when inventory.Blankets > 0 => $"{item.Name}  {item.Price}  (x{inventory.Blankets})",
                _ => $"{item.Name}  {item.Price}"
            };
            rows.Add(label);
        }
        rows.Add("Close");

        var ownerLabel = _sessions.Count > 1 ? $"P{_sessions.IndexOf(_shopMenuOwner) + 1} - " : "";
        _shopMenu.SetContent($"PURSER'S SHOP - {ownerLabel}Tix: {TixBalance}", rows, _shopMenuIndex);
    }

    private void TryPurchase(ShopItem item, PlayerSession buyer)
    {
        var inventory = buyer.Inventory;

        // The launcher row doubles as sell-back once owned.
        if (item.Kind == ShopItemKind.TixLauncher && inventory.HasTixLauncher)
        {
            inventory.HasTixLauncher = false;
            TixBalance += LauncherSellRefund;
            ShowMessage($"Sold the Tix Launcher back for {LauncherSellRefund} tix.", 2.5f);
            return;
        }

        if (item.Kind == ShopItemKind.LifeJacket && inventory.HasLifeJacket)
        {
            ShowMessage("You already have a life jacket on.", 2f);
            return;
        }
        if (item.Kind == ShopItemKind.DeckBoots && inventory.HasDeckBoots)
        {
            ShowMessage("You're already wearing deck boots.", 2f);
            return;
        }

        if (TixBalance < item.Price)
        {
            ShowMessage($"{item.Name} costs {item.Price} tix - you have {TixBalance}.", 2.5f);
            return;
        }

        TixBalance -= item.Price;
        switch (item.Kind)
        {
            case ShopItemKind.LifeJacket:
                inventory.HasLifeJacket = true;
                ShowMessage("Life jacket on - the water can't kill you, but swimming is slow.", 3f);
                break;

            case ShopItemKind.DeckBoots:
                inventory.HasDeckBoots = true;
                ShowMessage("Deck boots on - permanently faster on your feet.", 3f);
                break;

            case ShopItemKind.Blanket:
                inventory.Blankets++;
                ShowMessage($"Blanket bought ({inventory.Blankets} carried) - each cancels one icy death.", 3f);
                break;

            case ShopItemKind.FlareGun:
                FlareGunOwned = true;
                ShowMessage("Flare gun bought - after a collision, Up+E summons the Carpathia.", 3.5f);
                break;

            case ShopItemKind.PocketWatch:
                _pocketWatchUsed = true;
                _collisionBonusSeconds += PocketWatchBonusSeconds;
                ShowMessage("The lookout consults the pocket watch - impact delayed!", 3f);
                break;

            case ShopItemKind.TixLauncher:
                inventory.HasTixLauncher = true;
                ShowMessage("Purchased the Tix Launcher! Press E to fire.", 3f);
                break;

            case ShopItemKind.Snack:
                inventory.Snacks.Enqueue(item.Snack!);
                ShowMessage($"{item.Name} bought - {(inventory.HasTixLauncher ? "Down+E" : "E")} eats it for a speed burst.", 3f);
                break;

            case ShopItemKind.Tnt:
                inventory.TntCharges.Enqueue(item.Tnt!.Value);
                ShowMessage($"{item.Name} bought - hold Space (jump) and press E to place. Stand back!", 3f);
                break;
        }
    }

    /// <summary>
    /// The Fire key (P1 E, P2 U, Android TIX) does different things by chord,
    /// checked most-specific first: Jump held = place TNT, Up held = fire the
    /// flare gun, Down held (or no launcher owned) = eat a snack, otherwise fire
    /// the tix launcher. Everything degrades gracefully - a chord whose item
    /// isn't carried falls through to the next branch.
    /// </summary>
    private void CheckFireAction()
    {
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            if (_shopMenuOwner == session) continue;
            if (!SessionJustPressed(session, InputAction.Fire)) continue;
            var inventory = session.Inventory;

            // Jump is a real action key in platformer rooms (the actual jump
            // button), so the TNT chord swaps to Down there instead of doubling
            // up on it; the flare is exterior-topside only (you need line of
            // sight to fire one), and Up+Fire becomes the snack chord instead.
            var isPlatformer = CurrentRoom.IsPlatformer;
            var tntChord = isPlatformer ? InputAction.MoveDown : InputAction.Jump;
            if (SessionPressed(session, tntChord) && inventory.TntCharges.Count > 0)
            {
                PlaceTnt(session);
                continue;
            }

            if (!isPlatformer && SessionPressed(session, InputAction.MoveUp) && FlareGunOwned && !FlareGunFired)
            {
                TryFireFlareGun();
                continue;
            }

            var snackChord = isPlatformer ? InputAction.MoveUp : InputAction.MoveDown;
            var wantsSnack = !inventory.HasTixLauncher || SessionPressed(session, snackChord);
            if (wantsSnack && inventory.Snacks.Count > 0)
            {
                EatSnack(session);
                continue;
            }

            if (!inventory.HasTixLauncher) continue;

            if (TixBalance < LauncherFireCost)
            {
                ShowMessage("Not enough tix to fire the launcher.", 2f);
                continue;
            }

            TixBalance -= LauncherFireCost;
            FireLauncher(session);
            ShowMessage($"The tix launcher fires - {LauncherFireCost} tix scatter as {LauncherFireCount} coins!", 2f);
        }
    }

    private void EatSnack(PlayerSession session)
    {
        var snack = session.Inventory.Snacks.Dequeue();
        // Eating replaces any active buff rather than stacking with it.
        session.Inventory.FoodBuffMultiplier = snack.SpeedMultiplier;
        session.Inventory.FoodBuffSecondsRemaining = snack.DurationSeconds;
        ShowMessage($"You scarf the {snack.Kind} - speed boost for {snack.DurationSeconds:0}s!", 2.5f);
    }

    private void TryFireFlareGun()
    {
        if (Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning)
        {
            ShowMessage("Save the flare - the Carpathia won't answer before there's real trouble.", 2.5f);
            return;
        }
        FireFlareGun();
    }

    private void FireFlareGun()
    {
        FlareGunFired = true;
        _flareSummonRequested = true;
        ShowMessage("The flare arcs into the night - the Carpathia is coming!", 4f);
    }

    private void PlaceTnt(PlayerSession session)
    {
        var size = session.Inventory.TntCharges.Dequeue();
        var (column, row) = TileUnderPlayer(session.Player);
        var sheet = GetSheet(TntIconPath, 16, 16);
        var (x, y, sortOffset) = CurrentRoom.StandOnTileElevated(column, row, 16, 16);
        var charge = new TntCharge(sheet, size, column, row) { X = x, Y = y, ZIndex = 1, SortOffsetY = sortOffset };
        CurrentRoom.AttachRowAnchored(charge, row);
        GameObjects.Add(charge);
        _tntCharges.Add(charge);
        ShowMessage($"{size} TNT placed - {TntCharge.FuseSeconds(size):0.#}s fuse. RUN!", 2f);
    }

    /// <summary>Detonates any charge whose fuse ran out (a charge whose deck row already went under is just forgotten).</summary>
    private void UpdateTntCharges()
    {
        for (var i = _tntCharges.Count - 1; i >= 0; i--)
        {
            var charge = _tntCharges[i];
            if (!CurrentRoom.RoomObjects.Contains(charge))
            {
                _tntCharges.RemoveAt(i);
                continue;
            }
            if (!charge.FuseExpired) continue;
            _tntCharges.RemoveAt(i);
            DetonateTnt(charge);
        }
    }

    private void DetonateTnt(TntCharge charge)
    {
        CurrentRoom.ReleaseFromShip(charge);
        GameObjects.Remove(charge);

        var radiusTiles = TntCharge.BlastRadiusTiles(charge.Size);
        var (shakeAmplitude, shakeSeconds) = charge.Size switch
        {
            TntSize.Small => (5f, 0.8f),
            TntSize.Medium => (8f, 1.2f),
            _ => (12f, 1.6f)
        };
        StartShake(shakeAmplitude, shakeSeconds);
        _groanSound?.Play();

        CurrentRoom.BlastAt(charge.Column, charge.Row, radiusTiles);

        // Smoke burst scattering outward from the charge.
        var smokeSheet = GetSheet(SmokeIconPath, 16, 16);
        for (var i = 0; i < radiusTiles * 8; i++)
        {
            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var speed = 20f + (float)_random.NextDouble() * 40f;
            var puff = new Particle(smokeSheet, 1.1f)
            {
                X = charge.X + _random.Next(-6, 7),
                Y = charge.Y + _random.Next(-4, 5),
                ZIndex = 3,
                SortOffsetY = CurrentRoom.Level.TileHeight * 2,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed * 0.5f,
                RiseSpeed = 30f
            };
            _particles.Add(puff);
            GameObjects.Add(puff);
        }

        // Anyone standing too close is caught in the blast - the life jacket is
        // no help here, though a carried blanket still absorbs it (TriggerDeath).
        var blastPixels = radiusTiles * CurrentRoom.Level.TileWidth;
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            var dx = session.Player.X + 8 - (charge.X + 8);
            var dy = session.Player.Y + 16 - (charge.Y + 8);
            if (MathF.Sqrt(dx * dx + dy * dy) <= blastPixels)
                TriggerDeath(session, "blast");
        }

        CheckTntIcebergPayoff(charge, blastPixels);
    }

    /// <summary>
    /// The Large-TNT gambit: shatter the approaching iceberg during the Warning
    /// to buy a big collision delay - the expensive, skill-based alternative to
    /// the pocket watch (the berg is out on the water, so getting a charge near
    /// it usually means a life jacket swim or a last-second bow placement).
    /// </summary>
    private void CheckTntIcebergPayoff(TntCharge charge, float blastPixels)
    {
        var berg = CurrentRoom.Iceberg;
        if (berg is null || Phase != VoyagePhase.Warning) return;

        var (_, bergWidth, bergHeight) = PropKinds["iceberg"];
        var dx = berg.X + bergWidth / 2f - (charge.X + 8);
        var dy = berg.Y + bergHeight / 2f - (charge.Y + 8);
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > blastPixels + 40f) return;

        if (charge.Size != TntSize.Large)
        {
            ShowMessage("The berg shrugs it off - that needs Large TNT.", 3f);
            return;
        }

        var detached = CurrentRoom.DetachIceberg();
        if (detached is not null) GameObjects.Remove(detached);
        _collisionBonusSeconds += TntIcebergBonusSeconds;
        ShowMessage("The iceberg shatters! The collision is postponed!", 4f);
    }

    /// <summary>Foam trail while swimming - the visual cue that the water is survivable right now.</summary>
    private void SpawnSwimFoam(PlayerSession session, float deltaTime)
    {
        session.Inventory.SwimFoamTimer -= deltaTime;
        if (session.Inventory.SwimFoamTimer > 0f) return;
        session.Inventory.SwimFoamTimer = SwimFoamInterval;

        var sheet = GetSheet(WakeIconPath, 16, 8);
        var foam = new Particle(sheet, 0.6f)
        {
            X = session.Player.X,
            Y = session.Player.Y + 24,
            SortOffsetY = CurrentRoom.Level.TileHeight + 8
        };
        _particles.Add(foam);
        GameObjects.Add(foam);
    }

    private void FireLauncher(PlayerSession session)
    {
        var sheet = GetSheet(TixIconPath, 16, 16);
        for (var i = 0; i < LauncherFireCount; i++)
        {
            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var speed = 40f + (float)_random.NextDouble() * 60f;
            var velocityX = MathF.Cos(angle) * speed;
            var velocityY = MathF.Sin(angle) * speed * 0.5f;
            var velocityZ = 150f + (float)_random.NextDouble() * 100f;

            var coin = new LaunchedTix(sheet, velocityX, velocityY, velocityZ)
            {
                X = session.Player.X,
                Y = session.Player.Y,
                ZIndex = 2,
                SortOffsetY = 8,
                Value = 1
            };
            CurrentRoom.TixPickups.Add(coin);
            CurrentRoom.RoomObjects.Add(coin);
            GameObjects.Add(coin);
        }
    }

    private void CheckLifeboats()
    {
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            if (!ConfirmAvailable(session)) continue;
            if (TryBoardLifeboat(session.Player)) ConsumeConfirm(session);
        }
    }

    /// <summary>
    /// Boards the nearest lifeboat within reach, if the collision has happened: the
    /// boat is released from the ship and rows away carrying this player, who is
    /// out of the game (and out of danger) from then on, with a survival bonus
    /// banked. Returns false if there's no boat in reach, it's too early to
    /// launch, or this player can't board right now.
    /// </summary>
    public bool TryBoardLifeboat(Player player)
    {
        var session = _sessions.FirstOrDefault(s => s.Player == player);
        if (session is null || session.Escaped || session.IsDying) return false;

        var boat = LifeboatInReach(player);
        if (boat is null) return false;

        if (Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning)
        {
            ShowMessage("The crew won't launch the lifeboats before there's real danger.", 2.5f);
            return false;
        }

        BoardLifeboat(session, boat);
        return true;
    }

    /// <summary>
    /// The nearest still-hanging lifeboat within boarding reach of this player,
    /// measured sprite-center to sprite-center (top-left to top-left distance made
    /// reach feel unfair on the boat's far side).
    /// </summary>
    private Sprite? LifeboatInReach(Player player)
    {
        var (_, boatWidth, boatHeight) = PropKinds["lifeboat"];
        foreach (var boat in CurrentRoom.Lifeboats)
        {
            var dx = player.X + 8 - (boat.X + boatWidth / 2f);
            var dy = player.Y + 16 - (boat.Y + boatHeight / 2f);
            if (MathF.Sqrt(dx * dx + dy * dy) <= LifeboatBoardRadius) return boat;
        }
        return null;
    }

    private void BoardLifeboat(PlayerSession session, Sprite boat)
    {
        CurrentRoom.ReleaseFromShip(boat);

        if (_shopMenuOwner == session) CloseShopMenu();
        session.Escaped = true;
        session.Player.InputEnabled = false;
        session.Player.Z = 0f;
        TixBalance += LifeboatEscapeBonusTix;

        // Afloat on open water, the boat and its passenger must out-sort the flat
        // water tiles around them (same rule as the wake particles): a tile's own
        // depth-sort height is TileHeight, so anything floating on water needs its
        // SortOffsetY pushed at least that far past its sprite height, or tiles
        // "in front" clip the hull and the passenger's legs.
        boat.SortOffsetY += CurrentRoom.Level.TileHeight;

        // The passenger is snapped to (boat.Y - 12) every frame (see
        // UpdateLaunchedBoats), which - combined with the boat's own SortOffsetY
        // above - lands their SortY (Y + SortOffsetY) exactly on the boat's own
        // SortY every frame: 32 + 16 vs 20(boat height) + 16 - 12. Equal ZIndex too,
        // so that exact tie left the painter's-algorithm sort to break it arbitrarily
        // (it flips with unrelated array churn like particle spawns), flickering the
        // passenger's legs in and out from behind the boat. The +1 guarantees the
        // passenger always sorts after (renders on top of) their own boat.
        session.Player.SortOffsetY = PlayerBaseSortOffsetY + CurrentRoom.Level.TileHeight + 1;

        // Row straight away from the hull: out along the column (port/starboard)
        // axis, on whichever side of the ship's centerline the boat hangs.
        // (Lifeboats only ever exist on the isometric exterior hull, so Grid/
        // Tilemap are guaranteed non-null here.)
        var (_, boatWidth, boatHeight) = PropKinds["lifeboat"];
        var feetX = boat.X + (boatWidth - CurrentRoom.Grid!.TileWidth) / 2 - CurrentRoom.Tilemap!.X;
        var feetY = boat.Y + boatHeight - CurrentRoom.Grid.TileHeight - CurrentRoom.Tilemap.Y;
        var (boatColumn, _) = CurrentRoom.Grid.WorldToTile(feetX, feetY);
        var centerColumn = CurrentRoom.Level.Tiles.Length > 0 ? (CurrentRoom.Level.Tiles[0].Length - 1) / 2f : 0f;
        var side = boatColumn <= centerColumn ? -1f : 1f;

        var (originX, originY) = CurrentRoom.Grid.TileToWorld(0, 0);
        var (stepX, stepY) = CurrentRoom.Grid.TileToWorld(1, 0);
        float axisX = stepX - originX, axisY = stepY - originY;
        var axisLength = MathF.Sqrt(axisX * axisX + axisY * axisY);

        _launchedBoats.Add(new LaunchedBoat(boat, session.Player,
            axisX / axisLength * side * LifeboatRowSpeed,
            axisY / axisLength * side * LifeboatRowSpeed));

        ShowMessage($"Away in a lifeboat! +{LifeboatEscapeBonusTix} tix survival bonus.", 3f);

        // With everyone off the wreck there's nothing left to wait for - call the
        // Carpathia in now (the boats will row to her and the game ends aboard).
        if (_sessions.All(s => s.Escaped) && _rescueShip.State == RescueShipState.NotSummoned)
            _flareSummonRequested = true;
    }

    private void EndGame(string message)
    {
        CloseShopMenu();
        IsGameOver = true;

        if (_campaignMode)
        {
            _voyageCleared = _sessions.Any(s => s.Rescued);
            if (_voyageCleared && _voyage.TixGoal > 0 && TixBalance >= _voyage.TixGoal)
            {
                TixBalance += _voyage.GoalBonusTix;
                message += $" Banking goal met: +{_voyage.GoalBonusTix} bonus!";
            }

            var lastVoyage = _voyageIndex >= Campaign.Voyages.Count - 1;
            message += _voyageCleared
                ? lastVoyage ? "  CAMPAIGN COMPLETE! (Enter: sail the finale again)" : "  (Enter: next voyage)"
                : "  (Enter: retry this voyage)";

            SaveCampaignProgress();
        }

        _finalMessage = message;
        Console.WriteLine(message);
    }

    private void SaveCampaignProgress()
    {
        var save = new CampaignSave
        {
            VoyageIndex = _voyageCleared ? Math.Min(_voyageIndex + 1, Campaign.Voyages.Count - 1) : _voyageIndex,
            Bank = TixBalance,
            Players = _sessions.Select(s => InventorySave.From(s.Inventory)).ToList(),
        };
        save.Save();
        Cloud?.QueueUpload(save);
    }

    /// <summary>
    /// The only interaction left after a voyage has been decided: Enter moves
    /// the campaign along - next voyage if this one was cleared (anyone aboard
    /// the Carpathia), a retry of the same ship otherwise.
    /// </summary>
    private void CheckVoyageAdvance()
    {
        if (!_campaignMode || !IsGameOver) return;

        foreach (var session in _sessions)
        {
            if (!SessionJustPressed(session, InputAction.Confirm)) continue;
            if (_voyageCleared && _voyageIndex < Campaign.Voyages.Count - 1) _voyageIndex++;
            ResetVoyage(Campaign.Voyages[_voyageIndex]);
            return;
        }
    }

    /// <summary>
    /// The platformer-room counterpart of the isometric hazard block above:
    /// wall/floor collision is already fully resolved by PlatformerBody, so
    /// there's no IsSolid revert here - just tile hazards (steam vents etc.,
    /// only while actually standing on one) and the rising floodwater, which
    /// PlatformerBody.InWater already computes from the player's own feet
    /// position vs Room.WaterLineY.
    /// </summary>
    private void UpdatePlatformerHazard(PlayerSession session, float deltaTime)
    {
        var inventory = session.Inventory;
        var body = session.Player.Platformer;
        if (body is null) return;

        session.LastGoodX = session.Player.X;
        session.LastGoodY = session.Player.Y;

        var (column, row) = TileUnderPlayer(session.Player);
        var hazardKind = "";
        var tileHazard = body.OnGround && CurrentRoom.TryGetHazard(column, row, out hazardKind);
        var drowning = body.InWater;

        if (tileHazard || drowning)
        {
            if (inventory.HasLifeJacket || inventory.HazardGraceSeconds > 0f)
            {
                inventory.IsSwimming = drowning;
                if (drowning) SpawnSwimFoam(session, deltaTime);
            }
            else
            {
                TriggerDeath(session, drowning ? "drown" : hazardKind);
            }
        }
        else
        {
            inventory.IsSwimming = false;
        }
    }

    private void TriggerDeath(PlayerSession session, string hazard)
    {
        // A carried blanket cancels the death outright (any death - icy water,
        // floodwater, even a TNT blast) and buys a short window of hazard
        // immunity to scramble clear in.
        var inventory = session.Inventory;
        if (inventory.Blankets > 0)
        {
            inventory.Blankets--;
            inventory.HazardGraceSeconds = BlanketGraceSeconds;
            ShowMessage($"A blanket keeps you alive! ({inventory.Blankets} left) - move!", 2.5f);
            return;
        }

        // Dying at the counter (the purser's office floods mid-game) must not
        // leave a menu open fighting over InputEnabled - death wins.
        if (_shopMenuOwner == session) CloseShopMenu();

        session.IsDying = true;
        session.DyingTimer = DeathFreezeSeconds;
        session.Player.InputEnabled = false;
        TixBalance = Math.Max(0, TixBalance - TixPenaltyOnDeath);

        var verb = hazard switch
        {
            "freeze" => "froze in the North Atlantic",
            "blast" => "were blown off your feet by the TNT",
            "steam" => "were scalded by a ruptured steam line",
            _ => "drowned in the flooding compartment"
        };
        ShowMessage($"You {verb}... ({TixPenaltyOnDeath} tix lost)", DeathFreezeSeconds);
    }

    private void Respawn(PlayerSession session)
    {
        session.IsDying = false;
        session.Player.InputEnabled = true;
        session.Player.Z = 0f;

        var (column, row) = session.EntrySpawnTile;

        // The sinking can put the original entry spawn underwater - respawning
        // there would just kill the player again in an endless loop, so fall back
        // to the safest remaining deck tile (sternmost, near the centerline).
        if (CurrentRoom.IsSolid(column, row) || CurrentRoom.TryGetHazard(column, row, out _))
        {
            var safe = CurrentRoom.FindSafeTile();
            if (safe is not null)
            {
                (column, row) = safe.Value;
                session.EntrySpawnTile = safe.Value;
            }
        }

        var (x, y) = CurrentRoom.StandOnTile(column, row, 16, 32);
        session.Player.SnapTo(x, y);
        if (session.Player.Platformer is { } body) body.VelocityY = 0f;
        session.LastGoodX = x;
        session.LastGoodY = y;
    }

    private void ShowMessage(string text, float seconds)
    {
        _transientMessage = text;
        _transientTimer = seconds;
    }

    private void UpdateHudText(float deltaTime)
    {
        UpdateInventoryHud();

        if (IsGameOver)
        {
            Hud.SetText(_finalMessage);
            return;
        }

        if (_transientTimer > 0f)
        {
            _transientTimer -= deltaTime;
            Hud.SetText(_transientMessage);
            return;
        }

        var roleText = CurrentRole is not null ? $"  |  Role: {Capitalize(CurrentRole)}" : "";
        Hud.SetText($"Tix: {TixBalance}{roleText}  |  {PhaseLabel()}{LifeboatHint()}{ShopHint()}");
    }

    /// <summary>
    /// The second HUD line: what each player is carrying. Lives on its own line
    /// because the main status line is single-line/no-wrap and already long.
    /// </summary>
    private void UpdateInventoryHud()
    {
        var parts = new List<string>();
        for (var i = 0; i < _sessions.Count; i++)
        {
            var inventory = _sessions[i].Inventory;
            var bits = new List<string>();
            if (inventory.HasLifeJacket) bits.Add("jacket");
            if (inventory.HasDeckBoots) bits.Add("boots");
            if (inventory.HasTixLauncher) bits.Add("launcher(E)");
            if (inventory.Blankets > 0) bits.Add($"blankets x{inventory.Blankets}");
            if (inventory.Snacks.Count > 0) bits.Add($"snacks x{inventory.Snacks.Count}");
            if (inventory.TntCharges.Count > 0) bits.Add($"TNT x{inventory.TntCharges.Count}");
            if (bits.Count == 0) continue;
            parts.Add((_sessions.Count > 1 ? $"P{i + 1}: " : "") + string.Join(" ", bits));
        }
        if (FlareGunOwned && !FlareGunFired) parts.Add("flare gun (Up+E)");
        _inventoryHud.SetText(string.Join("  |  ", parts));
    }

    /// <summary>
    /// A persistent on-screen prompt whenever a player is standing within boarding
    /// reach of a lifeboat - boarding is the game's most important action and
    /// shouldn't rely on players guessing that a boat is interactive.
    /// </summary>
    private string LifeboatHint()
    {
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            if (LifeboatInReach(session.Player) is null) continue;

            return Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning
                ? "  |  Lifeboat (locked until danger)"
                : "  |  Enter/P: BOARD LIFEBOAT";
        }
        return "";
    }

    /// <summary>
    /// A persistent on-screen prompt whenever a player is standing at the shop
    /// counter with the menu closed, mirroring LifeboatHint.
    /// </summary>
    private string ShopHint()
    {
        if (_shopMenuOwner is not null) return "";
        foreach (var session in _sessions)
        {
            if (session.Escaped || session.IsDying) continue;
            var distance = DistanceToShop(session);
            if (distance is null || distance > InteractRadius) continue;

            return "  |  Enter: SHOP";
        }
        return "";
    }

    private string PhaseLabel()
    {
        var sinceCollision = SecondsSinceCollision();

        // Boarding overrides everything: the countdown is the only number that
        // matters once she's alongside (an early flare can put her here well
        // before the Sunk phase).
        if (_rescueShip.State == RescueShipState.Boarding)
            return $"CARPATHIA ALONGSIDE - board within {Math.Max(0f, _rescueShip.BoardingSecondsRemaining):0}s!";

        var cruisingLabel = _campaignMode
            ? $"{_voyage.Name} ({_voyageIndex + 1}/{Campaign.Voyages.Count}) - Cruising" +
              (_voyage.TixGoal > 0 ? $"  |  Goal: bank {_voyage.TixGoal}" : "")
            : "Cruising the North Atlantic";

        return Phase switch
        {
            VoyagePhase.Cruising => cruisingLabel,
            VoyagePhase.Warning => "ICEBERG WARNING",
            VoyagePhase.Collision => "COLLISION",
            VoyagePhase.Sinking => $"SINKING - {sinceCollision:0}s since impact",
            VoyagePhase.Split => $"THE SHIP HAS SPLIT - {sinceCollision:0}s since impact",
            VoyagePhase.Sunk => _rescueShip.State switch
            {
                RescueShipState.Steaming => "SUNK - the Carpathia approaches off the starboard side!",
                RescueShipState.Departed => "The Carpathia has departed.",
                _ => "SUNK - cling to the stern!"
            },
            _ => ""
        };
    }

    private bool SessionJustPressed(PlayerSession session, InputAction action) =>
        session.InputSource?.IsActionJustPressed(action) ?? InputActions.IsJustPressed(action);

    private bool SessionPressed(PlayerSession session, InputAction action) =>
        session.InputSource?.IsActionPressed(action) ?? InputActions.IsPressed(action);

    /// <summary>This session pressed Confirm this frame and no earlier check has claimed it yet.</summary>
    private bool ConfirmAvailable(PlayerSession session) =>
        !_confirmConsumed.Contains(session) && SessionJustPressed(session, InputAction.Confirm);

    private void ConsumeConfirm(PlayerSession session) => _confirmConsumed.Add(session);

    private static float Distance(GameObject a, GameObject b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpper(value[0]) + value[1..];
}
