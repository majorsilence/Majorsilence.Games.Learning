using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.Input;
using SDL3;

namespace Majorsilence.Games.Learning.Android;

/// <summary>
/// The Android equivalent of Program.cs's RunTitanicShip: same window/renderer/
/// audio/Game/EventLoop wiring, minus the console prompts (level chooser and
/// co-op question have no terminal here) - it boots straight into the Titanic
/// voyage in single-player. Runs on the SDL thread (see MainActivity.Main).
/// </summary>
internal static class AndroidGame
{
    public static void Run()
    {
        // Size is nominal - SDL windows on Android are always fullscreen.
        using var window = new Window("Titanic", 1280, 720, highPixelDensity: true);
        using var renderer = new Renderer(window);
        using var audioDevice = new AudioDevice();

        using var gameStartSound = new Sound(audioDevice, "assets/audio/game-start.mp3");
        gameStartSound.Play();

        var hud = new Hud(renderer, "assets/fonts/Gidole-Regular.ttf", 18,
            new SDL.Color { A = 0, B = 210, G = 210, R = 210 }) { X = 8, Y = 8 };

        var game = new Game(renderer, hud, audioDevice);

        void SyncViewport()
        {
            renderer.SyncLogicalPresentationToWindow();
            var (w, h) = renderer.Size;
            game.Camera.ViewportWidth = w;
            game.Camera.ViewportHeight = h;
        }
        SyncViewport();
        InputManager.WindowResized += SyncViewport;

        game.Begin("assets/levels/titanic.json", coop: false);

        renderer.DrawColor(18, 28, 42, 255);
        var loop = new EventLoop(renderer);
        loop.Start(game.GameObjects, game.Camera, game.BeforeFrame, game.AfterFrame);
    }
}
