using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Rpg;

// An original NES-era JRPG built on the same engine as the Titanic game - the
// second game on it, which is the point: everything it needs that wasn't there
// (top-down movement, a dialogue window) lands in Core where both can use it.
//
// Test hooks, env-gated and inert otherwise:
//   RPG_MAP="path|spawn"     start on a specific map instead of the first
//   RPG_SCREENSHOT="path"    save one frame a moment in, then keep running
//   RPG_SCRIPT="right:2,..." replay an input script and quit when it ends
//                            (see ScriptedInput), for hands-off checks
//   RPG_SHOT_AT="4.5"        take RPG_SCREENSHOT that many seconds in instead,
//                            e.g. at the end of a script rather than at the start
const string StartMap = "assets/levels/ashholt.json";
const string FontPath = "assets/fonts/Gidole-Regular.ttf";

// ~240 logical pixels on the short edge: fifteen 16px tiles, close to the
// vertical field of view of the console this style comes from.
const int TargetViewShortSide = 240;

using var window = new Window("Vale of Ash", 640, 480, highPixelDensity: true);
using var renderer = new Renderer(window);

// Sound is optional. A machine with no audio device - a CI box, a container,
// a scripted verification run - should play the game silently rather than fail
// to start.
using var audioDevice = OpenAudio();
using var game = new RpgGame(renderer, FontPath, audioDevice);

static AudioDevice? OpenAudio()
{
    try
    {
        return new AudioDevice();
    }
    catch (MajorsilenceException error)
    {
        Console.Error.WriteLine($"Audio unavailable, running silent: {error.Message}");
        return null;
    }
}

void SyncViewport()
{
    renderer.SyncLogicalPresentationToWindow(TargetViewShortSide);
    var (width, height) = renderer.LogicalSize;
    game.Camera.ViewportWidth = width;
    game.Camera.ViewportHeight = height;
}

SyncViewport();
InputManager.WindowResized += SyncViewport;

var mapSpec = Environment.GetEnvironmentVariable("RPG_MAP");
if (mapSpec is not null)
{
    var parts = mapSpec.Split('|', 2);
    game.LoadMap(parts[0], parts.Length > 1 ? parts[1] : "");
}
else
{
    game.LoadMap(StartMap);
}

var screenshotPath = Environment.GetEnvironmentVariable("RPG_SCREENSHOT");
var shotAt = Environment.GetEnvironmentVariable("RPG_SHOT_AT") is { } at ? float.Parse(at) : (float?)null;
var frames = 0;
var clock = 0f;
var traceNext = 0f;

var scriptText = Environment.GetEnvironmentVariable("RPG_SCRIPT");
ScriptedInput? scripted = null;
if (scriptText is not null)
{
    scripted = new ScriptedInput(scriptText);
    InputActions.RegisterSource(scripted);
}

renderer.DrawColor(16, 16, 24, 255);
var loop = new EventLoop(renderer);
loop.Start(game.GameObjects, game.Camera,
    beforeUpdate: deltaTime =>
    {
        scripted?.Advance(deltaTime);
        game.Update(deltaTime);
    },
    afterUpdate: deltaTime =>
    {
        clock += deltaTime;
        game.CheckDoors();

        if (scripted is not null && clock >= traceNext)
        {
            traceNext += 0.5f;
            var (column, row) = game.HeroTile;
            var track = Path.GetFileNameWithoutExtension(game.Music.NowPlaying);
            Console.WriteLine($"t={clock:0.0} map={game.MapName} tile=({column},{row}) at=({game.Hero.PreciseX:0.0},{game.Hero.PreciseY:0.0}) facing={game.Hero.Facing} music={(track == "" ? "-" : track)}"
                + (game.Dialogue.IsOpen ? " [talking]" : ""));
        }

        if (screenshotPath is not null)
        {
            var due = shotAt is { } seconds ? clock >= seconds : ++frames == 60;
            if (due)
            {
                renderer.SaveScreenshot(screenshotPath);
                screenshotPath = null;
            }
        }

        // A finished script means a finished run - nothing is driving the game
        // any more, so don't leave a window sitting there.
        if (scripted is { Finished: true }) loop.Stop();
    });
