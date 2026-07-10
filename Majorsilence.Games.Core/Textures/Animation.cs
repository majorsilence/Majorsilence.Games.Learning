namespace Majorsilence.Games.Core.Textures;

/// <summary>
/// Advances through a sequence of sprite sheet frame indices over time.
/// Driven by the same per-frame delta-time as the rest of the engine.
/// </summary>
public class Animation
{
    private readonly int[] _frames;
    private readonly double _frameDurationMs;
    private double _elapsedMs;
    private int _currentFrame;

    public bool Loop { get; set; }
    public bool IsFinished { get; private set; }

    public Animation(int[] frames, double frameDurationMs, bool loop = true)
    {
        if (frames == null || frames.Length == 0)
            throw new MajorsilenceException("Animation requires at least one frame.");
        if (frameDurationMs <= 0)
            throw new MajorsilenceException("Animation frame duration must be greater than zero.");

        _frames = frames;
        _frameDurationMs = frameDurationMs;
        Loop = loop;
    }

    public int CurrentFrame => _frames[_currentFrame];

    public void Update(float deltaTimeSeconds)
    {
        if (IsFinished) return;

        _elapsedMs += deltaTimeSeconds * 1000.0;

        while (_elapsedMs >= _frameDurationMs && !IsFinished)
        {
            _elapsedMs -= _frameDurationMs;
            Advance();
        }
    }

    private void Advance()
    {
        if (_currentFrame + 1 < _frames.Length)
        {
            _currentFrame++;
        }
        else if (Loop)
        {
            _currentFrame = 0;
        }
        else
        {
            IsFinished = true;
        }
    }

    public void Reset()
    {
        _currentFrame = 0;
        _elapsedMs = 0;
        IsFinished = false;
    }
}
