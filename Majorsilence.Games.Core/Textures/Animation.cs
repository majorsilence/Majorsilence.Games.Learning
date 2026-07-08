namespace Majorsilence.Games.Core.Textures;

/// <summary>
/// Advances through a sequence of sprite sheet frame indices over time.
/// Tracks wall-clock time internally since the engine's Update() has no delta-time parameter.
/// </summary>
public class Animation
{
    private readonly int[] _frames;
    private readonly double _frameDurationMs;
    private double _elapsedMs;
    private int _currentFrame;
    private long _lastTick;

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
        _lastTick = Environment.TickCount64;
    }

    public int CurrentFrame => _frames[_currentFrame];

    public void Update()
    {
        if (IsFinished) return;

        var now = Environment.TickCount64;
        _elapsedMs += now - _lastTick;
        _lastTick = now;

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
        _lastTick = Environment.TickCount64;
    }
}
