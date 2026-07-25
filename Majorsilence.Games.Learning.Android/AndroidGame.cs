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
        // Size is nominal - SDL windows on Android cover the screen; the explicit
        // fullscreen flag additionally hides the system bars (immersive mode), so
        // nothing opaque overlaps the top of the game (where the HUD sits).
        using var window = new Window("Titanic", 1280, 720, highPixelDensity: true, fullscreen: true);
        using var renderer = new Renderer(window);
        using var audioDevice = new AudioDevice();

        using var gameStartSound = new Sound(audioDevice, "assets/audio/game-start.mp3");
        gameStartSound.Play();

        // 14pt in the zoomed ~360px-short-side logical space (see Game.TargetViewShortSide).
        var hud = new Hud(renderer, "assets/fonts/Gidole-Regular.ttf", 14,
            new SDL.Color { A = 0, B = 210, G = 210, R = 210 }) { X = 8, Y = 8 };

        var game = new Game(renderer, hud, audioDevice);

        void SyncViewport()
        {
            renderer.SyncLogicalPresentationToWindow(Game.TargetViewShortSide);
            var (w, h) = renderer.LogicalSize;
            game.Camera.ViewportWidth = w;
            game.Camera.ViewportHeight = h;

            // Keep the HUD out of any display cutout (camera notch): SDL reports
            // the safe area in window points; convert its top inset to logical.
            var (_, windowH) = window.Size;
            if (SDL.GetWindowSafeArea(window, out var safe) && windowH > 0)
            {
                hud.Y = 8 + (int)MathF.Ceiling(safe.Y * h / (float)windowH);
            }
        }
        SyncViewport();
        InputManager.WindowResized += SyncViewport;

        // No console on Android, so no mode prompt: always continue the saved
        // campaign (a fresh install simply starts at voyage 1).
        game.BeginCampaign(CampaignSave.Load(), coop: false);

        // On-screen d-pad and buttons; registered as an extra InputActions
        // source so the shared game code needs no touch awareness.
        var touchControls = new TouchControls(renderer);
        InputActions.RegisterSource(touchControls);
        game.GameObjects.Add(touchControls);

        renderer.DrawColor(18, 28, 42, 255);
        var loop = new EventLoop(renderer);
        loop.Start(game.GameObjects, game.Camera, game.BeforeFrame, game.AfterFrame);
    }
}
