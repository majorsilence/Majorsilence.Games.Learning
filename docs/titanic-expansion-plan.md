5# Titanic Expansion Plan

Turn the current single-room `titanic.json` demo into a large, walkable, multi-room,
sinkable ship with an economy (tix), NPC crew roles the player can take over, and
multiplayer.

## Requested features (scope)

1. Much larger map — the ship is an actual walkable vessel.
2. Falling/jumping off the ship is lethal: the player drowns or freezes in the water.
3. Rooms on different decks of the ship are separate maps; walking through a door/stair
   loads the new room's map.
4. Currency is **tix** ("ship tix" aboard the Titanic).
5. The ship is sinkable: hitting the iceberg starts a sinking sequence, and partway
   through the sinking the ship **splits in half**.
6. **Tix launcher** item: launches 100 tix at a time, costs 1000 tix to buy.
7. NPCs: a **captain** (bridge), **engine room engineers**, **watch post watchers**
   (crow's nest).
8. Players can **take over** any of those NPC roles.
9. **Multiplayer.**

## Where the engine is today (and the gaps)

What exists: JSON level format (`LevelMap`/`LevelLoader`) with legend/ASCII tiles,
entities (`{type, column, row}`), per-level tileset, elevations; `Camera` with follow,
axis locks, bounds; isometric + flat tilemaps with per-tile depth sorting; `DynamicObject`
with delta-time movement, Z elevation, jump/gravity; `InputActions` abstraction;
`EventLoop.Start(gameObjects, camera, beforeUpdate)`.

Every feature above needs at least one engine capability that does not exist yet:

| Gap | Needed by |
| --- | --- |
| Entities carry only `type/column/row` — no per-entity data (door target, NPC role, spawn name) | rooms, NPCs, shop |
| One static scene built in `Program.cs` at startup; no teardown/reload at runtime | rooms, sinking split |
| No collision/walkability — the player can walk across water and through walls | walkable ship, interiors |
| No hazards, health, death, or respawn | drown/freeze |
| No runtime add/remove of GameObjects (the list is fixed before `loop.Start`) | tix pickups, launcher, NPCs |
| No interaction system (interact key, proximity checks, prompts) | doors, shop, role takeover |
| No dynamic HUD text (title texture is created once) | tix counter, prompts, warnings |
| No persistent game state across room loads | tix balance, voyage timeline |
| `IsometricTilemap` tiles/elevations are immutable after construction | flooding, deck tilt, split |
| No timers/scripted-event system | sinking timeline |
| No NPC/AI behaviors | crew NPCs |
| No networking | multiplayer |

The plan is ordered so each phase delivers a playable increment and builds the engine
piece the next phase needs.

---

## Phase 0 — Commit current work

The Titanic single-room demo, level selector, and `TilesetPath`/`TileFrames` loader
changes are still uncommitted. Commit them first so this work starts from a clean tree
(suggested message: `titanic demo, per-level tilesets, console level selector`).

## Phase 1 — Room system (multi-map ship skeleton)

**Goal:** walk between 2–3 placeholder rooms through doors. This is the structural
refactor everything else sits on.

Format changes (backward compatible, defaults empty):
- `LevelEntity.Properties : Dictionary<string, string>` — free-form per-entity data
  (this was explicitly anticipated in the original level-format design).
- New entity types interpreted by the game:
  - `door`: `properties: { "target": "titanic/a-deck.json", "spawn": "fromBoatDeck" }`
  - `spawnPoint`: `properties: { "name": "fromBoatDeck" }`
  - `playerStart` remains the initial spawn for the whole game.

Engine/game changes:
- Extract scene construction out of `Program.cs` into a `Room` class (owns tilemap,
  props, doors, spawn points, its `gameObjects` list, and camera bounds) plus a
  `Game` orchestrator that owns the player, camera, shared textures, and the current
  `Room`. `Program.cs` shrinks to: pick level → build `Game` → run loop.
- Room switching: each frame (in `beforeUpdate`), check the player's current tile
  against door tiles; on overlap, queue a transition; between frames, tear down the
  old room, `LevelLoader.Load` the target, rebuild, and `player.SnapTo(...)` the named
  spawn point (**must** be `SnapTo` — plain `X=`/`Y=` is silently undone by the
  `_preciseX/Y` shadow). Reset camera bounds; camera `Target` stays the player.
- `EventLoop` iterates a list the game mutates, so transitions (and later,
  spawning pickups) must not happen mid-iteration: give `Game` a deferred
  add/remove/switch queue flushed once per frame.
- Texture lifetime: cache textures per path in `Game` (rooms share tilesets/props);
  never `using var` inside room construction (already-learned gotcha).

Verification: three tiny linked rooms, screenshot-walk a round trip through both doors,
assert the player lands on each spawn point tile.

## Phase 2 — Collision, hazards, death (walkable ship edges)

**Goal:** walls stop you; stepping/falling into water kills you (drown) or freezes you,
then you respawn.

Format changes:
- `LevelMap.Solid : List<string>` — tile-type names the player cannot enter
  (walls, railings, void).
- `LevelMap.Hazards : Dictionary<string, string>` — tile-type name → hazard kind,
  e.g. `{ "water": "freeze" }` on exterior maps, `{ "floodwater": "drown" }` inside.

Engine/game changes:
- Movement blocking: in `beforeUpdate` (or a small `TileCollision` helper), test the
  tile the player is about to occupy; if solid, `SnapTo` back. Tile-granular is enough
  for this game's 16px-tall tiles; no sub-tile AABB physics needed yet.
- Hazard check: if the player's grounded tile is a hazard (and they are `IsGrounded` —
  jumping over a 1-tile gap stays legal), start a short death sequence: freeze input,
  tint/flash or play a splash/freeze animation, then respawn at the room's entry spawn
  with a tix penalty (drop some tix as pickups where they died — gives phase 4 a hook).
- Minimal `PlayerState` (alive/dying/respawning) and a `Hud` object: a
  `StationaryObject` subclass that re-renders its text texture when its string changes
  (dispose old texture, create new). Used now for "You froze in the North Atlantic",
  later for the tix counter and role prompts.

Verification: walk off the deck edge → death message → respawn; walk into a wall → stopped.

## Phase 3 — The ship itself (maps + artwork)

**Goal:** the actual large Titanic. All content, no new engine features.

Exterior: replace `titanic.json` with a large **Boat Deck** map (~70×24 tiles),
ship-shaped — tapered bow, rounded stern, railings (solid), 4 funnels, mast, lifeboat
props, surrounded by water (hazard). The iceberg sits far off the bow.

Interior rooms (one JSON each, linked by `door` entities on stairs/hatches):

```
crow's nest (watch post)
      │ mast ladder
BOAT DECK (exterior) ── bridge (captain)
      │ grand staircase
A-DECK: first-class corridor ── cabins (2–3 small rooms)
      │ staircase
D-DECK: dining saloon ── purser's office (shop, phase 4)
      │ crew stairs
ENGINE ROOM (engineers) ── BOILER ROOM
```

~9 rooms total. Interior rooms use `heights`/elevations sparingly (catwalks in the
engine room, staircase landings) since 2.5D jumping already works.

Artwork (procedural Pillow scripts, same 4x-supersample technique as existing assets):
- Interior tileset: wood floor, carpet, steel plate, wall tiles (solid), stair tile,
  door-frame tile, floodwater.
- Props: lifeboat, mast, bench, cabin bed, dining table, boiler, engine, telegraph/wheel
  (bridge), purser counter.
- All reuse the existing `propKinds` dictionary — new entries, no engine change.

Verification: screenshot every room; walk the full graph bow-to-boiler-room.

## Phase 4 — Tix economy + tix launcher

**Goal:** collect ship tix, see a balance, buy and fire the tix launcher.

- `tix` entity: small coin-like pickup sprite; walking over it removes it and
  increments the balance (uses phase 1's deferred remove). Scatter them around rooms.
- HUD: `Tix: 1,240` via the phase 2 `Hud` object. Balance lives on `Game`
  (survives room loads).
- **Purser's office shop**: a `shop` entity; standing adjacent shows a prompt
  ("Confirm: buy Tix Launcher — 1000 tix"). Pressing Confirm with ≥1000 tix buys it.
  Introduces the interact pattern (proximity + `InputAction.Confirm`) reused for
  role takeover in phase 5.
- **Tix launcher**: once owned, a fire action (new `InputAction.Fire`, e.g. `X` key)
  launches **100 tix at a time** (requires balance ≥100; deducts 100): spawn 100 tix
  pickups as tiny `DynamicObject`s with random planar velocity and an upward Z arc
  (jump physics already exists); on landing they become normal pickups anyone can
  collect. Pointless-but-fun in single player; becomes a way to shower tix on other
  players in multiplayer.

Verification: collect tix → counter updates; buy launcher at 1000; fire → 100 pickups
scatter and are re-collectable; balance math asserts in a test.

## Phase 5 — NPC crew + role takeover

**Goal:** captain on the bridge, engineers in the engine room, watchers in the crow's
nest — all with idle behaviors, all takeover-able.

- `Npc : DynamicObject` with a tiny behavior enum: `Idle` (stand at post, face a
  direction), `Patrol` (walk between waypoint tiles), `Work` (looping work animation —
  engineers shoveling). Placed via entities:
  `{ "type": "npc", "properties": { "role": "captain" } }` (also `engineer`, `watcher`).
- New character sprite sheets (recolors/variants of the existing character):
  captain (white cap/dark coat), engineer (soot/overalls), watcher (dark coat/cap).
- **Role takeover**: stand next to an NPC, prompt "Confirm: take over as Watcher".
  The NPC steps aside; the player holds the role (HUD shows it) until they leave the
  post room. Duties matter in phase 6:
  - **Watcher** (crow's nest): a "spot" action; spotting the iceberg early extends the
    time before collision (NPC watchers spot it late).
  - **Captain** (bridge): after the warning, a "hard-a-starboard" action further
    delays the collision (it still happens — the ship always sinks; roles buy time).
  - **Engineer** (engine room): while the post is manned (NPC or player), pumps run
    and phase 6's flooding advances slower; the player doing a simple keep-pressing
    "stoke" action slows it further.

Verification: NPCs idle/patrol correctly in their rooms; takeover prompt works;
role shown on HUD; role effects assertable once phase 6 lands.

## Phase 6 — The sinking (iceberg, flooding, split in half)

**Goal:** the voyage always ends: iceberg strike → progressive flooding deck by deck →
the ship splits in half → fully sinks; survive as long as possible.

- `VoyageState` on `Game` (persists across room loads): a state machine with a clock —
  `Cruising → Sighted → Collision → Sinking(progress 0..1) → Split → Sunk`.
  Timings are data (constants or a small JSON), modified by phase 5 roles
  (watcher/captain delay `Collision`; manned engines scale sinking rate down).
- Engine change: `IsometricTilemap.SetTile(col,row,index)` and
  `SetElevation(col,row,px)` (drop `readonly`, arrays are already mutable) so rooms
  can flood and the deck can tilt at runtime.
- **Flooding** (bottom-up, on the sinking clock): boiler room → engine room → dining
  saloon → A-deck → boat deck. A room's flood level converts floor tiles to
  `floodwater` (drown hazard) row by row from its low end; a fully flooded room's
  doors become lethal to enter. Rooms flood by *timeline*, not by being loaded — a room
  entered late is already partly under water.
- **Bow-down tilt**: on the exterior map, progressively lower bow-tile elevations /
  raise stern elevations a step at a time as `progress` grows (the elevation system
  renders this correctly for free), and march the waterline aft by converting deck
  tiles to water.
- **The split** (at ~60–70% progress): a scripted beat — screen shake, crack sound,
  swap the exterior map for a pre-authored `boat-deck-split.json` (two halves, gap of
  water midships, stern half elevated/tilting) using the phase 1 room loader; the
  player is re-placed on whichever half they were standing on. Interior rooms of the
  forward half become inaccessible (doors flooded).
- **Endgame**: at `Sunk`, everything is water; show survival time + tix collected.
  Death by flooding uses the phase 2 drown hazard unchanged.

Verification: scripted fast-forward run (accelerated clock) capturing screenshots at
each state; assert tile mutations (waterline position, split map swap) at known clock
values in a test with a stub renderer where possible.

## Phase 7 — Multiplayer

Biggest item by far — staged, because 7b alone is comparable to everything above.

**7a — Local co-op (shared screen), first:**
- Second `Player` fed by a second input binding set (WASD + a second confirm/jump key,
  or a gamepad source — `IInputSource`/`RegisterSource` was built for exactly this).
- `Camera.Target` follows the midpoint of players in the current room, clamped so both
  stay on screen; both players must be on/near a door for a room transition (prompt
  shows "waiting for other player").
- Tix balances per player; both can hold roles; the launcher becomes genuinely useful
  (shower your co-op partner with tix).
- This forces the sim/rendering separation (per-player state, no globals) that 7b needs.

**7b — LAN client-server:**
- Authoritative server (headless `Game` without a renderer — the phase 1 refactor makes
  this split feasible): owns `VoyageState`, tix, roles, entity/NPC positions, flooding.
  Clients send input actions; server broadcasts ~10–20 Hz snapshots; clients interpolate.
- Transport: LiteNetLib (MIT, UDP, .NET-native) recommended over hand-rolled sockets;
  messages as small structs or `System.Text.Json` (already a dependency) to start.
- Players may be in *different rooms*: each client renders only its player's room;
  the server simulates all rooms every tick (flooding already runs on the global clock,
  so this mostly falls out of phase 6's design).
- Out of scope even for 7b: internet matchmaking/NAT traversal, prediction/rollback,
  cheating protection. LAN/direct-IP only.

## Suggested order & increments

Each phase is a separately committable, runnable increment:
0. commit current work →
1. rooms (placeholder maps) →
2. collision/hazards/death →
3. real ship content →
4. tix + launcher →
5. NPCs + roles →
6. sinking + split →
7a. local co-op →
7b. LAN.

Phases 1–2 are the engine foundation; 3–6 are mostly content plus one focused engine
feature each; 7 is its own project. A sensible first milestone to actually build:
**phases 0–3** (the large walkable multi-room ship with lethal water), which fully
covers the earlier "make the titanic map much larger… move between rooms" request.

## Level-format changes (all backward compatible)

| Field | Type | Default | Used for |
| --- | --- | --- | --- |
| `LevelEntity.Properties` | `Dictionary<string,string>` | empty | doors, spawns, NPCs, shop |
| `LevelMap.Solid` | `List<string>` | empty | walls, railings |
| `LevelMap.Hazards` | `Dictionary<string,string>` | empty | water=freeze, floodwater=drown |

Existing levels (`demo.json`, side-scrollers) never need editing; `LevelLoaderTest`
gains fixtures for each new field plus validation (door without `target`, hazard
naming an unknown tile type, etc.).
