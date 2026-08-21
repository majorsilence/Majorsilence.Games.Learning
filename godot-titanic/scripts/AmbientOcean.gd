extends AudioStreamPlayer

## Soft, slowly-modulated low-passed noise standing in for wind/ocean
## ambience on deck -- no audio assets in this project, so this is a
## deliberately simple/forgiving choice: a one-pole low-pass turns raw
## white noise into a soft rumble (real wind/ocean sound is itself mostly
## noise-shaped, so this is a reasonable stand-in without needing to
## synthesize anything tonal that could come out sounding actually wrong
## in a way this session has no way to hear and check). Attached to every
## room whose mood has a sky (see DayNightCycle -- same "stars" mood gate).

const MIX_RATE := 22050.0
const LOWPASS_COEFF := 0.045

var _playback: AudioStreamGeneratorPlayback
var _rng := RandomNumberGenerator.new()
var _lp_state := 0.0
var _t := 0.0


func _ready() -> void:
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = MIX_RATE
	gen.buffer_length = 0.5
	stream = gen
	volume_db = -20.0
	_rng.randomize()
	play()
	_playback = get_stream_playback()


func _physics_process(delta: float) -> void:
	if not _playback:
		return
	_t += delta
	var amp := 0.16 + 0.06 * sin(_t * 0.15)
	var to_fill := _playback.get_frames_available()
	for i in to_fill:
		var white := _rng.randf() * 2.0 - 1.0
		_lp_state += (white - _lp_state) * LOWPASS_COEFF
		_playback.push_frame(Vector2(_lp_state, _lp_state) * amp)
