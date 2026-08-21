extends Node

## Drives a full day/night cycle for outdoor ("deck_night" mood) rooms:
## a single tracked "celestial body" direction sweeps across the sky
## (sun by day, moon by night -- DAY_NIGHT_SKY_SHADER draws whichever fits
## based on how far above the horizon it is), the sibling DirectionalLight3D
## rotates and re-colors to match, and the sibling WorldEnvironment's
## ambient light blends between the two @export'd palettes. Attached to a
## plain sibling Node (not to WorldEnvironment itself) so it can reach both
## without one owning the other.

@export var cycle_seconds: float = 240.0
@export var night_ambient_color := Color(0.35, 0.45, 0.65)
@export var day_ambient_color := Color(1.0, 1.0, 1.0)
@export var night_ambient_energy := 0.45
@export var day_ambient_energy := 0.9
@export var night_light_color := Color(0.55, 0.65, 0.9)
@export var day_light_color := Color(1.0, 0.97, 0.9)
@export var night_light_energy := 0.55
@export var day_light_energy := 1.3

# Start a little after sunrise, not at midnight, so the first look at a
# freshly-loaded deck isn't total darkness.
var _time_of_day := 0.22

@onready var _world_env: WorldEnvironment = get_parent().get_node("WorldEnvironment")
@onready var _light: DirectionalLight3D = get_parent().get_node("DirectionalLight3D")
@onready var _sky_material: ShaderMaterial = _world_env.environment.sky.sky_material


func _process(delta: float) -> void:
	_time_of_day = fmod(_time_of_day + delta / cycle_seconds, 1.0)
	var angle := _time_of_day * TAU
	var sun_dir := Vector3(0.35, sin(angle), -cos(angle)).normalized()
	var day_t: float = clamp(smoothstep(-0.15, 0.15, sun_dir.y), 0.0, 1.0)

	_sky_material.set_shader_parameter("sun_dir", sun_dir)

	_light.look_at(_light.global_position - sun_dir, Vector3.UP)
	_light.light_color = night_light_color.lerp(day_light_color, day_t)
	_light.light_energy = lerp(night_light_energy, day_light_energy, day_t)

	var env := _world_env.environment
	env.ambient_light_color = night_ambient_color.lerp(day_ambient_color, day_t)
	env.ambient_light_energy = lerp(night_ambient_energy, day_ambient_energy, day_t)
