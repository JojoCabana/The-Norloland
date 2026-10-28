extends CharacterBody3D
## WASD, mouse look, Space jump, Esc release mouse. Gravity always toward center.
@export var speed := 9.0
@export var gravity_strength := 18.0
@export var jump_speed := 7.0
@export var mouse_sensitivity := 0.0025
var pitch := 0.0
@onready var camera: Camera3D = $Camera3D

func _ready() -> void:
    Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _unhandled_input(event: InputEvent) -> void:
    if event is InputEventMouseButton and event.pressed:
        Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
    if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
        Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
    if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
        rotate_object_local(Vector3.UP, -event.relative.x * mouse_sensitivity)
        pitch = clampf(pitch - event.relative.y * mouse_sensitivity, -1.45, 1.45)
        camera.rotation.x = pitch

func _physics_process(delta: float) -> void:
    var up := global_position.normalized()
    if up.length_squared() < 0.1:
        return
    var correction := Quaternion(global_transform.basis.y.normalized(), up)
    global_transform.basis = (Basis(correction) * global_transform.basis).orthonormalized()
    up_direction = up
    var forward := -global_transform.basis.z
    forward = (forward - up * forward.dot(up)).normalized()
    var right := global_transform.basis.x
    right = (right - up * right.dot(up)).normalized()
    var input_axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    var movement := (right * input_axis.x + forward * input_axis.y) * speed
    var radial_speed := velocity.dot(up)
    if is_on_floor():
        radial_speed = maxf(radial_speed, 0.0)
        if Input.is_key_pressed(KEY_SPACE):
            radial_speed = jump_speed
    else:
        radial_speed -= gravity_strength * delta
    velocity = movement + up * radial_speed
    move_and_slide()
