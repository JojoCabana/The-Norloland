extends CharacterBody3D
## Flat-ground movement baseline. No radial gravity or spherical correction.
@export var speed: float = 5.0
@export var jump_speed: float = 5.0
@export var mouse_sensitivity: float = 0.0025
var pitch: float = 0.0
@onready var camera: Camera3D = $Camera3D

func _ready() -> void:
    Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _unhandled_input(event: InputEvent) -> void:
    if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
        Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
    elif event is InputEventMouseButton and event.pressed:
        Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
    elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
        rotate_y(-event.relative.x * mouse_sensitivity)
        pitch = clampf(pitch - event.relative.y * mouse_sensitivity, -1.45, 1.45)
        camera.rotation.x = pitch

func _physics_process(delta: float) -> void:
    var axis := Vector2(
        float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
        float(Input.is_key_pressed(KEY_W)) - float(Input.is_key_pressed(KEY_S))
    ).normalized()
    var direction: Vector3 = global_transform.basis.x * axis.x - global_transform.basis.z * axis.y
    velocity.x = direction.x * speed
    velocity.z = direction.z * speed
    if is_on_floor():
        if Input.is_key_pressed(KEY_SPACE):
            velocity.y = jump_speed
        else:
            velocity.y = 0.0
    else:
        velocity.y -= 18.0 * delta
    move_and_slide()
