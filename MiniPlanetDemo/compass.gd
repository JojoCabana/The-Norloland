extends CanvasLayer
## Spherical navigation HUD: +Y north, +X longitude zero, planet centered at origin.
@export var planet_radius: float = 60.0
@onready var player: Node3D = get_parent().get_node("Player")
var label: Label

func _ready() -> void:
    label = Label.new()
    label.set_anchors_preset(Control.PRESET_CENTER_TOP)
    label.position = Vector2(-230, 16)
    label.custom_minimum_size = Vector2(460, 64)
    label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    label.add_theme_font_size_override("font_size", 18)
    add_child(label)

func _process(_delta: float) -> void:
    var position := player.global_position
    var radius := position.length()
    if radius < 0.001:
        label.text = "Position im Planetenzentrum"
        return
    var up := position / radius
    var latitude := rad_to_deg(asin(clampf(up.y, -1.0, 1.0)))
    var longitude := rad_to_deg(atan2(up.z, up.x))
    var altitude := radius - planet_radius
    var north := Vector3.UP - up * up.y
    var heading_text := "POL | Richtung undefiniert"
    if north.length_squared() >= 0.000001:
        north = north.normalized()
        var east := north.cross(up).normalized()
        var forward := -player.global_transform.basis.z
        forward = forward - up * forward.dot(up)
        if forward.length_squared() >= 0.000001:
            forward = forward.normalized()
            var heading := fposmod(rad_to_deg(atan2(forward.dot(east), forward.dot(north))), 360.0)
            var sectors := ["N", "NO", "O", "SO", "S", "SW", "W", "NW"]
            var index := int(floor((heading + 22.5) / 45.0)) % 8
            heading_text = "%s | %03d°" % [sectors[index], roundi(heading)]
    label.text = "%s\nBreite: %.4f° | Länge: %.4f° | Höhe: %.1f m" % [heading_text, latitude, longitude, altitude]
