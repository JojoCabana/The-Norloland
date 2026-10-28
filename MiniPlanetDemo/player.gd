extends CharacterBody3D
## Same WASD and mouse controls as flat_player.gd, with radial up/gravity.
@export var speed: float = 5.0
@export var jump_speed: float = 5.0
@export var gravity_strength: float = 18.0
@export var mouse_sensitivity: float = 0.0025
var pitch: float = 0.0
@onready var camera: Camera3D = $Camera3D

func _ready() -> void:
    motion_mode = CharacterBody3D.MOTION_MODE_GROUNDED
    floor_snap_length = 0.35
    floor_max_angle = deg_to_rad(55.0)
    _align_to_planet()
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
    var current_up: Vector3 = global_transform.basis.y.normalized()
    global_transform.basis = (Basis(Quaternion(current_up, up)) * global_transform.basis).orthonormalized()
    up_direction = up

func _physics_process(delta: float) -> void:
    _align_to_planet()
    var up: Vector3 = up_direction
    var axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    # The flat controller's movement, projected onto the local ground plane.
    var direction: Vector3 = global_transform.basis.x * axis.x - global_transform.basis.z * axis.y
    direction = direction.slide(up).normalized()
    var radial_velocity: float = velocity.dot(up)
    if is_on_floor():
        radial_velocity = 0.0
        if Input.is_key_pressed(KEY_SPACE):
            radial_velocity = jump_speed
    else:
        radial_velocity -= gravity_strength * delta
    velocity = direction * speed + up * radial_velocity
    move_and_slide()
    _align_to_planet()
