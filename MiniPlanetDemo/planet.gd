extends StaticBody3D
## First milestone: walkable smooth planet. Mesh is NOT YET editable voxel terrain.
const RADIUS := 250.0
const LAT_SEGMENTS := 96
const LON_SEGMENTS := 192

var terrain_noise := FastNoiseLite.new()
var detail_noise := FastNoiseLite.new()

func _ready() -> void:
    terrain_noise.seed = 1701
    terrain_noise.frequency = 0.018
    terrain_noise.fractal_type = FastNoiseLite.FRACTAL_FBM
    terrain_noise.fractal_octaves = 4
    detail_noise.seed = 821
    detail_noise.frequency = 0.085
    _build_planet()

func _height(dir: Vector3) -> float:
    return RADIUS + terrain_noise.get_noise_3dv(dir * RADIUS) * 6.0 + detail_noise.get_noise_3dv(dir * RADIUS) * 0.9

func _color(dir: Vector3, elevation: float) -> Color:
    var latitude := absf(dir.y)
    if latitude > 0.76:
        return Color(0.87, 0.93, 0.96).lerp(Color.WHITE, clampf((latitude - 0.76) / 0.24, 0.0, 1.0))
    if latitude > 0.67:
        return Color(0.58, 0.65, 0.52).lerp(Color(0.87, 0.93, 0.96), (latitude - 0.67) / 0.09)
    var biome := terrain_noise.get_noise_3dv(dir * RADIUS * 0.45)
    if biome > 0.05:
        return Color(0.60, 0.52, 0.32).lerp(Color(0.72, 0.65, 0.43), clampf(elevation / 6.0, 0.0, 1.0))
    return Color(0.22, 0.46, 0.18).lerp(Color(0.37, 0.52, 0.22), clampf(elevation / 6.0, 0.0, 1.0))

func _point(lat: int, lon: int) -> Vector3:
    var phi := PI * float(lat) / float(LAT_SEGMENTS)
    var theta := TAU * float(lon) / float(LON_SEGMENTS)
    var direction := Vector3(sin(phi) * cos(theta), cos(phi), sin(phi) * sin(theta))
    return direction * _height(direction)

func _build_planet() -> void:
    var st := SurfaceTool.new()
    st.begin(Mesh.PRIMITIVE_TRIANGLES)
    for lat in range(LAT_SEGMENTS):
        for lon in range(LON_SEGMENTS):
            var a := _point(lat, lon)
            var b := _point(lat + 1, lon)
            var c := _point(lat + 1, lon + 1)
            var d := _point(lat, lon + 1)
            _triangle(st, a, c, b)
            _triangle(st, a, d, c)
    st.generate_normals()
    var mesh := st.commit()
    var material := StandardMaterial3D.new()
    material.vertex_color_use_as_albedo = true
    material.roughness = 1.0
    mesh.surface_set_material(0, material)
    var visual := MeshInstance3D.new()
    visual.mesh = mesh
    add_child(visual)
    var collision := CollisionShape3D.new()
    collision.shape = mesh.create_trimesh_shape()
    add_child(collision)

func _triangle(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
    for p in [a, b, c]:
        var dir := p.normalized()
        st.set_color(_color(dir, p.length() - RADIUS))
        st.add_vertex(p)
