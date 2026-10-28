extends CharacterBody3D
## First-person walking on a stationary spherical planet.
@export var speed: float = 5.0
@export var gravity_strength: float = 22.0
@export var jump_speed: float = 7.0
@export var mouse_sensitivity: float = 0.0025
var pitch: float = 0.0
var vertical_speed: float = 0.0
@onready var camera: Camera3D = $Camera3D

func _ready() -> void:
    floor_snap_length = 0.5
    floor_max_angle = deg_to_rad(55.0)
    motion_mode = CharacterBody3D.MOTION_MODE_GROUNDED
    _align_to_surface()
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

func _align_to_surface() -> void:
    var up: Vector3 = global_position.normalized()
    if up.length_squared() < 0.01:
        return
    var current_up: Vector3 = global_transform.basis.y.normalized()
    var rotation_to_up := Quaternion(current_up, up)
    global_transform.basis = (Basis(rotation_to_up) * global_transform.basis).orthonormalized()
    up_direction = up

func _physics_process(delta: float) -> void:
    _align_to_surface()
    var up: Vector3 = up_direction
    var forward: Vector3 = -global_transform.basis.z
    forward = (forward - up * forward.dot(up)).normalized()
    var right: Vector3 = global_transform.basis.x
    right = (right - up * right.dot(up)).normalized()
    var input_axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    var tangent_velocity: Vector3 = (right * input_axis.x + forward * input_axis.y) * speed
    if is_on_floor():
        vertical_speed = 0.0
        if Input.is_key_pressed(KEY_SPACE):
            vertical_speed = jump_speed
    else:
        vertical_speed -= gravity_strength * delta
    velocity = tangent_velocity + up * vertical_speed
    move_and_slide()
    vertical_speed = velocity.dot(up_direction)
