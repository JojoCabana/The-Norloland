extends CharacterBody3D
## Surface-locked first-person controller: the planet stays stationary.
@export var speed: float = 5.0
@export var gravity_strength: float = 22.0
@export var jump_speed: float = 7.0
@export var mouse_sensitivity: float = 0.0025
var pitch: float = 0.0
var height_above_ground: float = 0.95
var vertical_speed: float = 0.0
@onready var camera: Camera3D = $Camera3D
@onready var planet: Node3D = get_parent().get_node("Planet")

func _ready() -> void:
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
    var rotation_to_up := Quaternion(global_transform.basis.y.normalized(), up)
    global_transform.basis = (Basis(rotation_to_up) * global_transform.basis).orthonormalized()
    up_direction = up

func _physics_process(delta: float) -> void:
    var up: Vector3 = global_position.normalized()
    if up.length_squared() < 0.01:
        return
    _align_to_surface()
    var forward: Vector3 = -global_transform.basis.z
    forward = (forward - up * forward.dot(up)).normalized()
    var right: Vector3 = global_transform.basis.x
    right = (right - up * right.dot(up)).normalized()
    var axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    var tangent: Vector3 = (right * axis.x + forward * axis.y) * speed
    # Move the character around the planet, not the planet underneath it.
    var next_position: Vector3 = global_position + tangent * delta
    var next_up: Vector3 = next_position.normalized()
    var ground_radius: float = planet.surface_radius(next_up)
    var standing_radius: float = ground_radius + 0.95
    if Input.is_key_pressed(KEY_SPACE) and height_above_ground <= 0.951 and vertical_speed <= 0.0:
        vertical_speed = jump_speed
    vertical_speed -= gravity_strength * delta
    height_above_ground = maxf(0.95, height_above_ground + vertical_speed * delta)
    if height_above_ground <= 0.95 and vertical_speed < 0.0:
        vertical_speed = 0.0
    global_position = next_up * (standing_radius + height_above_ground - 0.95)
    _align_to_surface()
