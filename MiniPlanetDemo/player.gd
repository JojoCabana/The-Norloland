extends CharacterBody3D
## Deterministic spherical walking test. Movement rotates the player around
## the planet center; planet mesh never moves. Collision solver is bypassed
## temporarily to isolate the previously failing grounded movement.
@export var speed: float = 5.0
@export var jump_speed: float = 5.0
@export var gravity_strength: float = 18.0
@export var mouse_sensitivity: float = 0.0025
const FEET_OFFSET: float = 0.95
var pitch: float = 0.0
var jump_height: float = 0.0
var vertical_speed: float = 0.0
@onready var camera: Camera3D = $Camera3D
@onready var planet: Node3D = get_parent().get_node("Planet")

func _ready() -> void:
    _align_to_planet()
    _snap_to_surface()
    Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _unhandled_input(event: InputEvent) -> void:
    if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
        Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
    elif event is InputEventMouseButton and event.pressed:
        Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
    elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
        rotate_object_local(Vector3.UP, -event.relative.x * mouse_sensitivity)
        pitch = clampf(pitch - event.relative.y * mouse_sensitivity, -1.45, 1.45)
        camera.rotation.x = pitch

func _align_to_planet() -> void:
    var up: Vector3 = global_position.normalized()
    if up.length_squared() < 0.01:
        return
    var rotation_to_up := Quaternion(global_transform.basis.y.normalized(), up)
    global_transform.basis = (Basis(rotation_to_up) * global_transform.basis).orthonormalized()
    up_direction = up

func _snap_to_surface() -> void:
    var up: Vector3 = global_position.normalized()
    global_position = up * (planet.surface_radius(up) + FEET_OFFSET + jump_height)

func _physics_process(delta: float) -> void:
    var up: Vector3 = global_position.normalized()
    var axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    var direction: Vector3 = global_transform.basis.x * axis.x - global_transform.basis.z * axis.y
    direction = direction.slide(up)
    if direction.length_squared() > 0.000001:
        direction = direction.normalized()
        var rotation_axis: Vector3 = up.cross(direction).normalized()
        var rotation_angle: float = speed * delta / maxf(global_position.length(), 1.0)
        var step_rotation := Quaternion(rotation_axis, rotation_angle)
        global_position = step_rotation * global_position
        global_transform.basis = (Basis(step_rotation) * global_transform.basis).orthonormalized()
    if Input.is_key_pressed(KEY_SPACE) and jump_height <= 0.0 and vertical_speed <= 0.0:
        vertical_speed = jump_speed
    vertical_speed -= gravity_strength * delta
    jump_height = maxf(0.0, jump_height + vertical_speed * delta)
    if jump_height <= 0.0 and vertical_speed < 0.0:
        vertical_speed = 0.0
    _snap_to_surface()
    _align_to_planet()
