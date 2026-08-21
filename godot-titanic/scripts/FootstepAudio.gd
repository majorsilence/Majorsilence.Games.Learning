extends AudioStreamPlayer

## Procedural footstep "thud" -- a short decaying noise burst, not a
## sample (no audio assets in this project). Player3D.gd calls trigger()
## on each half-cycle of its own walk-bob timer. Buffer is serviced from
## _physics_process, not _process -- see the NpcIdle.gd note in memory on
## why _process's timing isn't trustworthy in this project/headless mode;
## audio buffer underruns would be the audible version of that same bug.

const MIX_RATE := 22050.0
const BURST_SECONDS := 0.12

var _playback: AudioStreamGeneratorPlayback
var _burst_samples_left := 0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = MIX_RATE
	gen.buffer_length = 0.3
	stream = gen
	volume_db = -6.0
	_rng.randomize()
	play()
	_playback = get_stream_playback()


func trigger() -> void:
	_burst_samples_left = int(MIX_RATE * BURST_SECONDS)


func _physics_process(_delta: float) -> void:
	if not _playback:
		return
	var to_fill := _playback.get_frames_available()
	for i in to_fill:
		var sample := 0.0
		if _burst_samples_left > 0:
			var elapsed := 1.0 - float(_burst_samples_left) / (MIX_RATE * BURST_SECONDS)
			var envelope := exp(-elapsed * 14.0)
			sample = (_rng.randf() * 2.0 - 1.0) * envelope * 0.5
			_burst_samples_left -= 1
		_playback.push_frame(Vector2(sample, sample))
