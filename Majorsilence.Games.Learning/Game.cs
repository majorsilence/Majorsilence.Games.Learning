using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
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
    public const string BoatDeckPath = "assets/levels/titanic.json";
    public const string BoatDeckSplitPath = "assets/levels/titanic-rooms/boat-deck-split.json";
    private const int BoatDeckSplitMidRow = 40;

    private const float WarningAtSeconds = 20f;
    private const float BaseCollisionAtSeconds = 50f;
    private const float SplitAfterCollisionSeconds = 70f;
    private const float SunkAfterCollisionSeconds = 110f;

    private const float WatcherBonusSeconds = 20f;
    private const float CaptainBonusSeconds = 15f;
    private const float EngineerFloodBonusSeconds = 20f;

    private const float DoorCooldownSeconds = 0.4f;
    private const float DeathFreezeSeconds = 1.6f;
    private const float InteractRadius = 40f;
    private const float PickupRadius = 20f;
    private const int TixPenaltyOnDeath = 50;
    private const int LauncherCost = 1000;
    private const int LauncherFireCost = 100;
    private const int LauncherFireCount = 100;

    // The iceberg starts this many world pixels ahead of the bow (in the ship's
    // direction of travel) once it's sighted, and closes that distance as the
    // voyage clock counts down the rest of the Warning phase - so it's visibly
    // "out there" getting closer, not just an abstract timer.
    private const float IcebergApproachDistance = 550f;

    // Stern/bow tip tiles on the boat deck hull (see assets/levels/titanic.json) -
    // where the wake trail and bow spray spawn from.
    private const int SternColumn = 9;
    private const int SternRow = 77;
    private const int BowColumn = 9;
    private const int BowRow = 2;

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

    // How far down the hull (row index, bow at the top) the waterline reaches at
    // key story beats: by the moment the ship splits, everything forward of the
    // split row is awash; by Sunk, all but the last few stern rows are gone -
    // the stern is the classic final refuge.
    private const int WaterlineRowAtSplit = BoatDeckSplitMidRow - 1;
    private const int WaterlineRowAtSunk = 65;

    public Camera Camera { get; } = new();
    public List<GameObject> GameObjects { get; } = new();
    public Hud Hud { get; }
    public Room CurrentRoom { get; private set; } = null!;
    public VoyagePhase Phase { get; private set; } = VoyagePhase.Cruising;
    public int TixBalance { get; private set; } = 300;
    public bool HasTixLauncher { get; private set; }
    public string? CurrentRole { get; private set; }

    public string DefaultTilesetPath { get; }
    public Dictionary<string, int> DefaultTileFrameIndex { get; }
    public string TixIconPath { get; }
    public string WakeIconPath { get; }
    public string SmokeIconPath { get; }
    public Dictionary<string, (string ImagePath, int Width, int Height)> PropKinds { get; }
    public Dictionary<string, (string ImagePath, int Width, int Height)> NpcKinds { get; }

    private readonly Renderer _renderer;
    private readonly Dictionary<string, Texture> _textureCache = new();
    private readonly Dictionary<(string Path, int Width, int Height), SpriteSheet> _sheetCache = new();
    private readonly List<PlayerSession> _sessions = new();
    private readonly Random _random = new();
    private readonly Dictionary<string, float> _floodDelayBonusByPath = new();

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

        public PlayerSession(Player player, IInputSource? inputSource)
        {
            Player = player;
            InputSource = inputSource;
        }
    }

    public Game(Renderer renderer, Hud hud)
    {
        _renderer = renderer;
        Hud = hud;
        GameObjects.Add(Hud);

        DefaultTilesetPath = "assets/artwork/isometric-demo/tileset.png";
        DefaultTileFrameIndex = new Dictionary<string, int> { ["grass"] = 0, ["dirt"] = 1, ["water"] = 2, ["stone"] = 3, ["sand"] = 4 };
        TixIconPath = "assets/artwork/titanic-demo/tix-coin.png";
        WakeIconPath = "assets/artwork/titanic-demo/wake-foam.png";
        SmokeIconPath = "assets/artwork/titanic-demo/smoke-puff.png";

        PropKinds = new Dictionary<string, (string, int, int)>
        {
            ["tree"] = ("assets/artwork/isometric-demo/tree.png", 32, 48),
            ["funnel"] = ("assets/artwork/titanic-demo/funnel.png", 24, 56),
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
        };

        NpcKinds = new Dictionary<string, (string, int, int)>
        {
            ["captain"] = ("assets/artwork/titanic-demo/captain.png", 16, 32),
            ["engineer"] = ("assets/artwork/titanic-demo/engineer.png", 16, 32),
            ["watcher"] = ("assets/artwork/titanic-demo/watcher.png", 16, 32),
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

    /// <summary>Seconds elapsed since the scripted collision, or -1 before it has happened.</summary>
    public float SecondsSinceCollision() =>
        Phase == VoyagePhase.Cruising || Phase == VoyagePhase.Warning ? -1f : _voyageClock - CollisionAtSeconds;

    private float CollisionAtSeconds => BaseCollisionAtSeconds + _collisionBonusSeconds;

    public float EffectiveFloodDelaySeconds(string path, float baseDelay)
    {
        if (baseDelay < 0f) return -1f;
        return baseDelay + _floodDelayBonusByPath.GetValueOrDefault(path);
    }

    /// <summary>Creates the player(s) and loads the first room. Call once, before running EventLoop.</summary>
    public void Begin(string entryLevelPath, bool coop)
    {
        var playerSheet = GetSheet("assets/artwork/isometric-demo/character.png", 16, 32);
        var player1 = new Player(playerSheet) { Speed = 120f, ZIndex = 1, SortOffsetY = 32, AnimateOnlyWhenMoving = true };
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
            };
            var input2 = new KeyboardInputSource(bindings);
            var player2Sheet = GetSheet("assets/artwork/isometric-demo/character.png", 16, 32);
            var player2 = new Player(player2Sheet, input2) { Speed = 120f, ZIndex = 1, SortOffsetY = 32, AnimateOnlyWhenMoving = true };
            player2.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));
            _sessions.Add(new PlayerSession(player2, input2));
            GameObjects.Add(player2);

            var anchor = new MidpointAnchor(player1, player2);
            GameObjects.Add(anchor);
            Camera.Target = anchor;
        }

        LoadRoom(entryLevelPath, "");
    }

    public void LoadRoom(string targetPath, string spawnName)
    {
        if (targetPath == BoatDeckPath && _hasSplit) targetPath = BoatDeckSplitPath;

        if (CurrentRoom is not null)
        {
            foreach (var obj in CurrentRoom.RoomObjects) GameObjects.Remove(obj);
        }

        // The ocean/boat-deck levels persist their sailed distance across room
        // reloads (a fresh Room instance is built every time a door is used,
        // including re-entering the boat deck) - other rooms never drift, so they
        // always start at zero regardless of how far the ship has sailed.
        var isDriftingLevel = targetPath == BoatDeckPath || targetPath == BoatDeckSplitPath;
        var room = isDriftingLevel
            ? new Room(targetPath, this, _shipDriftAppliedX, _shipDriftAppliedY)
            : new Room(targetPath, this);
        CurrentRoom = room;
        GameObjects.AddRange(room.RoomObjects);

        // A freshly built Iceberg sprite starts unoffset at its baseline (near-bow)
        // position - reset the tracker so UpdateIcebergApproach computes a clean delta.
        _icebergOffsetAppliedX = 0;
        _icebergOffsetAppliedY = 0;

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

    private void PlaceSessions((int Column, int Row) spawnTile)
    {
        var (x, y) = CurrentRoom.StandOnTile(spawnTile.Column, spawnTile.Row, 16, 32);
        _sessions[0].Player.SnapTo(x, y);
        _sessions[0].Player.Z = 0;
        _sessions[0].LastGoodX = x;
        _sessions[0].LastGoodY = y;
        _sessions[0].EntrySpawnTile = spawnTile;

        if (_sessions.Count > 1)
        {
            var offsetColumn = spawnTile.Column + 1;
            var secondTile = !CurrentRoom.IsSolid(offsetColumn, spawnTile.Row)
                ? (offsetColumn, spawnTile.Row)
                : spawnTile;
            var (x2, y2) = CurrentRoom.StandOnTile(secondTile.Item1, secondTile.Item2, 16, 32);
            _sessions[1].Player.SnapTo(x2, y2);
            _sessions[1].Player.Z = 0;
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
    private (int Column, int Row) TileUnderPlayer(Player player)
    {
        const int width = 16;
        const int height = 32;
        var feetX = player.X + (width - CurrentRoom.Grid.TileWidth) / 2 - CurrentRoom.Tilemap.X;
        var feetY = player.Y + height - CurrentRoom.Grid.TileHeight - CurrentRoom.Tilemap.Y;
        return CurrentRoom.Grid.WorldToTile(feetX, feetY);
    }

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

        foreach (var session in _sessions)
        {
            var (column, row) = TileUnderPlayer(session.Player);
            session.Player.GroundZ = CurrentRoom.GetElevationPixels(column, row);
        }
    }

    /// <summary>Cycles the tilemap's water variants on a timer so the whole ocean shimmers.</summary>
    private void UpdateWaterShimmer(float deltaTime)
    {
        _shimmerTimer -= deltaTime;
        if (_shimmerTimer > 0f) return;
        _shimmerTimer = WaterShimmerInterval;
        CurrentRoom.Tilemap.AnimationPhase++;
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
            var progress = Math.Clamp(sinceCollision / SplitAfterCollisionSeconds, 0f, 1f);
            waterlineRow = (int)(progress * WaterlineRowAtSplit);
        }
        else if (Phase == VoyagePhase.Split || Phase == VoyagePhase.Sunk)
        {
            var sinceSplit = sinceCollision - SplitAfterCollisionSeconds;
            var span = SunkAfterCollisionSeconds - SplitAfterCollisionSeconds;
            var progress = span > 0f ? Math.Clamp(sinceSplit / span, 0f, 1f) : 1f;
            waterlineRow = WaterlineRowAtSplit + (int)(progress * (WaterlineRowAtSunk - WaterlineRowAtSplit));
        }
        else
        {
            return;
        }

        var removed = CurrentRoom.SubmergeRowsThrough(waterlineRow, "water");
        foreach (var obj in removed) GameObjects.Remove(obj);
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
        if (driftSpeedX == 0f && driftSpeedY == 0f)
        {
            foreach (var stale in _particles) GameObjects.Remove(stale);
            _particles.Clear();
        }
        else if (speedFactor > 0.05f)
        {
            _wakeSpawnTimer -= deltaTime;
            if (_wakeSpawnTimer <= 0f)
            {
                _wakeSpawnTimer = WakeSpawnInterval / speedFactor;
                var (wakeX, wakeY) = CurrentRoom.StandOnTile(SternColumn, SternRow, 16, 8);
                var wakeSheet = GetSheet(WakeIconPath, 16, 8);
                // SortOffsetY must exceed a tile's own depth-sort height (TileHeight,
                // see IsometricTilemap) or the tile this sits on always paints over it.
                var wake = new Particle(wakeSheet, WakeLifespanSeconds) { X = wakeX, Y = wakeY, SortOffsetY = CurrentRoom.Grid.TileHeight };
                _particles.Add(wake);
                GameObjects.Add(wake);
            }

            _bowSprayTimer -= deltaTime;
            if (_bowSprayTimer <= 0f)
            {
                _bowSprayTimer = BowSprayInterval / speedFactor;
                var (sprayX, sprayY) = CurrentRoom.StandOnTile(BowColumn, BowRow, 16, 8);
                var spraySheet = GetSheet(WakeIconPath, 16, 8);
                // small sideways scatter so the spray reads as splashing off the bow, not a fixed dot
                var spray = new Particle(spraySheet, BowSprayLifespanSeconds)
                {
                    X = sprayX + _random.Next(-10, 11),
                    Y = sprayY + _random.Next(-4, 5),
                    SortOffsetY = CurrentRoom.Grid.TileHeight
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
                    var smoke = new Particle(smokeSheet, SmokeLifespanSeconds)
                    {
                        X = funnel.X,
                        Y = funnel.Y - 8,
                        ZIndex = 3,
                        // Sorts alongside the funnel itself (SortOffsetY 64 = the
                        // funnel's own 56 plus the 8px this spawns above it), so
                        // nothing standing in front of the funnel can hide smoke
                        // rising above it. Height is climbed via Z (RiseSpeed), not
                        // world Y, so the rise never corrupts the depth sort either.
                        SortOffsetY = 64,
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
        UpdateVoyageClock(deltaTime);

        foreach (var session in _sessions)
        {
            if (session.IsDying)
            {
                session.DyingTimer -= deltaTime;
                if (session.DyingTimer <= 0f) Respawn(session);
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
                    TriggerDeath(session, hazard);
                }
            }
        }

        if (_doorCooldown > 0f) _doorCooldown -= deltaTime;
        else CheckDoors();

        CheckPickups();
        CheckNpcInteractionAndRoleBonus();
        CheckShop();
        CheckLauncherFire();

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
                }
                break;

            case VoyagePhase.Collision:
                Phase = VoyagePhase.Sinking;
                ShowMessage("Water is rising below decks...", 4f);
                ApplyFloodIfDueForCurrentRoom();
                break;

            case VoyagePhase.Sinking:
                ApplyFloodIfDueForCurrentRoom();
                if (SecondsSinceCollision() >= SplitAfterCollisionSeconds)
                {
                    Phase = VoyagePhase.Split;
                    ShowMessage("The ship has split in two!", 4f);
                    StartShake(amplitude: 9f, seconds: 1.6f);
                    TriggerSplit();
                }
                break;

            case VoyagePhase.Split:
                ApplyFloodIfDueForCurrentRoom();
                if (SecondsSinceCollision() >= SunkAfterCollisionSeconds)
                {
                    Phase = VoyagePhase.Sunk;
                    ShowMessage("The ship has gone down. Survive the wreck.", 6f);
                }
                break;

            case VoyagePhase.Sunk:
                ApplyFloodIfDueForCurrentRoom();
                break;
        }
    }

    private void TriggerSplit()
    {
        _hasSplit = true;
        if (CurrentRoom.Path != BoatDeckPath) return;

        // Always the stern half: by the moment the ship breaks, the sinking
        // waterline has already put the entire forward half awash, so anyone
        // still alive scrambles aft with the upheaval.
        LoadRoom(BoatDeckSplitPath, "aftHalf");
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
            var (column, row) = TileUnderPlayer(session.Player);
            var door = CurrentRoom.Doors.FirstOrDefault(d => d.Column == column && d.Row == row);
            if (door is not null)
            {
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
                if (Distance(session.Player, pickup) <= PickupRadius)
                {
                    TixBalance += pickup.Value;
                    (collected ??= new List<TixPickup>()).Add(pickup);
                    break;
                }
            }
        }

        if (collected is null) return;
        foreach (var pickup in collected)
        {
            CurrentRoom.TixPickups.Remove(pickup);
            CurrentRoom.RoomObjects.Remove(pickup);
            GameObjects.Remove(pickup);
        }
    }

    private void CheckNpcInteractionAndRoleBonus()
    {
        if (CurrentRole is null)
        {
            foreach (var session in _sessions)
            {
                var npc = CurrentRoom.Npcs.FirstOrDefault(n => Distance(session.Player, n) <= InteractRadius);
                if (npc is not null && SessionJustPressed(session, InputAction.Confirm))
                {
                    CurrentRole = npc.Role;
                    _currentRoleRoomPath = CurrentRoom.Path;
                    CurrentRoom.Npcs.Remove(npc);
                    CurrentRoom.RoomObjects.Remove(npc);
                    GameObjects.Remove(npc);
                    ShowMessage($"You are now the {Capitalize(npc.Role)}.", 3f);
                    return;
                }
            }
        }
        else
        {
            foreach (var session in _sessions)
            {
                if (SessionJustPressed(session, InputAction.Confirm) && TryApplyRoleBonus())
                    return;
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

    private void CheckShop()
    {
        if (CurrentRoom.ShopTile is null) return;
        var (shopColumn, shopRow) = CurrentRoom.ShopTile.Value;
        var (tileX, tileY) = CurrentRoom.Grid.TileToWorld(shopColumn, shopRow);
        var shopX = tileX + CurrentRoom.Grid.TileWidth / 2f;
        var shopY = tileY + CurrentRoom.Grid.TileHeight;

        foreach (var session in _sessions)
        {
            var dx = session.Player.X - shopX;
            var dy = session.Player.Y - shopY;
            if (MathF.Sqrt(dx * dx + dy * dy) > InteractRadius) continue;
            if (!SessionJustPressed(session, InputAction.Confirm)) continue;

            if (HasTixLauncher)
            {
                ShowMessage("You already own the Tix Launcher.", 2.5f);
            }
            else if (TixBalance >= LauncherCost)
            {
                TixBalance -= LauncherCost;
                HasTixLauncher = true;
                ShowMessage("Purchased the Tix Launcher! Press E to fire.", 3f);
            }
            else
            {
                ShowMessage($"The Tix Launcher costs {LauncherCost} tix - you have {TixBalance}.", 2.5f);
            }
            return;
        }
    }

    private void CheckLauncherFire()
    {
        if (!HasTixLauncher) return;

        foreach (var session in _sessions)
        {
            if (!SessionJustPressed(session, InputAction.Fire)) continue;

            if (TixBalance < LauncherFireCost)
            {
                ShowMessage("Not enough tix to fire the launcher.", 2f);
                return;
            }

            TixBalance -= LauncherFireCost;
            FireLauncher(session);
            ShowMessage("The tix launcher fires - 100 tix scatter!", 2f);
            return;
        }
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

    private void TriggerDeath(PlayerSession session, string hazard)
    {
        session.IsDying = true;
        session.DyingTimer = DeathFreezeSeconds;
        session.Player.InputEnabled = false;
        TixBalance = Math.Max(0, TixBalance - TixPenaltyOnDeath);

        var verb = hazard == "freeze" ? "froze in the North Atlantic" : "drowned in the flooding compartment";
        ShowMessage($"You {verb}... ({TixPenaltyOnDeath} tix lost)", DeathFreezeSeconds);
    }

    private void Respawn(PlayerSession session)
    {
        session.IsDying = false;
        session.Player.InputEnabled = true;
        session.Player.Z = 0f;

        var (column, row) = session.EntrySpawnTile;
        var (x, y) = CurrentRoom.StandOnTile(column, row, 16, 32);
        session.Player.SnapTo(x, y);
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
        if (_transientTimer > 0f)
        {
            _transientTimer -= deltaTime;
            Hud.SetText(_transientMessage);
            return;
        }

        var roleText = CurrentRole is not null ? $"  |  Role: {Capitalize(CurrentRole)}" : "";
        var launcherText = HasTixLauncher ? "  |  Tix Launcher (E to fire)" : "";
        Hud.SetText($"Tix: {TixBalance}{roleText}{launcherText}  |  {PhaseLabel()}");
    }

    private string PhaseLabel()
    {
        var sinceCollision = SecondsSinceCollision();
        return Phase switch
        {
            VoyagePhase.Cruising => "Cruising the North Atlantic",
            VoyagePhase.Warning => "ICEBERG WARNING",
            VoyagePhase.Collision => "COLLISION",
            VoyagePhase.Sinking => $"SINKING - {sinceCollision:0}s since impact",
            VoyagePhase.Split => $"THE SHIP HAS SPLIT - {sinceCollision:0}s since impact",
            VoyagePhase.Sunk => $"SUNK - only the stern remains. Survived {sinceCollision:0}s",
            _ => ""
        };
    }

    private bool SessionJustPressed(PlayerSession session, InputAction action) =>
        session.InputSource?.IsActionJustPressed(action) ?? InputActions.IsJustPressed(action);

    private static float Distance(GameObject a, GameObject b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpper(value[0]) + value[1..];
}
