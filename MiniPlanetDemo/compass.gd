extends CanvasLayer
## Local compass on a spherical planet. Planet north is world +Y.
@onready var player: Node3D = get_parent().get_node("Player")
var label: Label

func _ready() -> void:
    label = Label.new()
    label.set_anchors_preset(Control.PRESET_CENTER_TOP)
    label.position = Vector2(-170, 16)
    label.custom_minimum_size = Vector2(340, 40)
    label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    label.add_theme_font_size_override("font_size", 22)
    add_child(label)

func _process(_delta: float) -> void:
    var up := player.global_position.normalized()
    var north := Vector3.UP - up * Vector3.UP.dot(up)
    if north.length_squared() < 0.000001:
        label.text = "POL  |  Richtung Nord undefiniert"
        return
    north = north.normalized()
    var east := north.cross(up).normalized()
    var forward := -player.global_transform.basis.z
    forward = (forward - up * forward.dot(up)).normalized()
    var heading := fposmod(rad_to_deg(atan2(forward.dot(east), forward.dot(north))), 360.0)
    var direction := "N"
    if heading >= 337.5 or heading < 22.5:
        direction = "N"
    elif heading < 67.5:
        direction = "NO"
    elif heading < 112.5:
        direction = "O"
    elif heading < 157.5:
        direction = "SO"
    elif heading < 202.5:
        direction = "S"
    elif heading < 247.5:
        direction = "SW"
    elif heading < 292.5:
        direction = "W"
    else:
        direction = "NW"
    label.text = "%s  |  %03d°" % [direction, roundi(heading)]
