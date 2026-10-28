# Mini Planet Demo — milestone 1

Separate standalone Godot 4 project inside the feature branch. Open **MiniPlanetDemo/** with Godot 4.x (4.7 target; not tested in 4.7).

## Implemented
- Procedural spherical terrain, ~250 m base radius, hills, grassland, steppe and two icy polar caps.
- Walkable triangle-mesh collision; first-person player with center-directed gravity.
- WASD movement, mouse look, Space jump, Esc releases cursor; click to recapture.
- No water.

## Not yet implemented
- This milestone uses a static procedural triangle mesh, **not** a voxel density field.
- Terraforming/mining, caves, indestructible core, chunk streaming, persistence and biome props are future milestones.
- This is a source prototype, not an exported executable.

## Run
1. Check out branch `feature/mini-planet-simulation`.
2. In Godot Project Manager choose **Import** and select `MiniPlanetDemo/project.godot`.
3. Press F6/F5 to run the main scene/project.

The demo is intentionally isolated from the existing Norloland root project.
