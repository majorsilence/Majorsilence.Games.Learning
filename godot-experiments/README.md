# Godot Experiments

A standalone Godot 4.7 project, separate from the `.sln` (Titanic and the
FF-clone RPG run on the custom C#/SDL engine in `Majorsilence.Games.Learning`
and `Majorsilence.Games.Rpg` — this folder does not touch that code). Purpose:
spike what "more advanced levels," including 3D rooms, could feel like if
built in Godot, and exercise the Godot MCP tooling (`claude mcp add godot`,
package `@coding-solo/godot-mcp`) that's now configured for this repo.

## What's here

- `Main.tscn` — menu, pick a room.
- `scenes/Room2D.tscn` — a top-down 2D room (`CharacterBody2D`, `StaticBody2D`
  walls, arrow-key movement). Walking through the blue door tile transitions
  to the 3D room.
- `scenes/Room3D.tscn` — a 3D room built from `CSGBox3D` primitives (no mesh/
  texture assets needed), a capsule `CharacterBody3D` player, and a fixed
  chase camera. Walking south off the floor's open edge transitions back to
  the 2D room.
- No binary art assets: walls/floor/player are all colored primitives
  (`ColorRect` in 2D, `CSGBox3D`/`CapsuleMesh` in 3D), so everything here is
  plain text and diffs cleanly.

## Running it

```
/home/peter/bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64 --path godot-experiments
```

Or open the editor (`--path godot-experiments -e`) to poke at scenes visually.

Headless smoke test (no GPU/window, just checks for script/parse errors):

```
GODOT=/home/peter/bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
$GODOT --headless --path godot-experiments --import
$GODOT --headless --path godot-experiments scenes/Room3D.tscn --quit-after 60
```

## Godot MCP

The `godot` MCP server (`claude mcp add godot -- npx @coding-solo/godot-mcp`,
pointed at the Godot 4.7 binary above via `GODOT_PATH`) is registered for
this repo in Claude Code's local config. It can launch the editor, run/stop
projects, capture debug output, and do basic scene/node scaffolting. It does
not expose script-editing tools, so hand-authoring `.tscn`/`.gd` files (as
here) or editing them in the actual editor remains the way to do real level
work; the MCP server is best used for launching/running/inspecting rather
than for scene authoring.

## Open questions before this goes further

- Whether a 3D room would actually integrate with Titanic/the RPG (embedded
  view, separate scene transition, shared save state) or stay a separate
  prototype — no decision made yet, this is just a feasibility spike.
- Art direction for a 3D room — the current primitives are placeholders.
