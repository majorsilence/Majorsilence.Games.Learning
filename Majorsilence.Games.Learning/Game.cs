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
    private const int BoatDeckSplitMidRow = 12;

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
        var player1 = new Player(playerSheet) { Speed = 120f, ZIndex = 1, SortOffsetY = 32 };
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
            var player2 = new Player(player2Sheet, input2) { Speed = 120f, ZIndex = 1, SortOffsetY = 32 };
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
    private void AdvanceShipDrift(float deltaTime)
    {
        if (CurrentRoom.Level.DriftSpeedX == 0f && CurrentRoom.Level.DriftSpeedY == 0f) return;

        _shipDriftAccumX += CurrentRoom.Level.DriftSpeedX * deltaTime;
        _shipDriftAccumY += CurrentRoom.Level.DriftSpeedY * deltaTime;
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
    /// Advances ship drift, then syncs each player's GroundZ from the tile they're
    /// currently over - all of this must run before GameObject.Update so
    /// movement/gravity this frame use the post-drift world.
    /// </summary>
    public void BeforeFrame(float deltaTime)
    {
        AdvanceShipDrift(deltaTime);

        foreach (var session in _sessions)
        {
            var (column, row) = TileUnderPlayer(session.Player);
            session.Player.GroundZ = CurrentRoom.GetElevationPixels(column, row);
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

        var (_, row) = TileUnderPlayer(_sessions[0].Player);
        var spawnName = row < BoatDeckSplitMidRow ? "forwardHalf" : "aftHalf";
        LoadRoom(BoatDeckSplitPath, spawnName);
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

    private string PhaseLabel() => Phase switch
    {
        VoyagePhase.Cruising => "Cruising the North Atlantic",
        VoyagePhase.Warning => "ICEBERG WARNING",
        VoyagePhase.Collision => "COLLISION",
        VoyagePhase.Sinking => "SINKING",
        VoyagePhase.Split => "THE SHIP HAS SPLIT",
        VoyagePhase.Sunk => "SUNK",
        _ => ""
    };

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
