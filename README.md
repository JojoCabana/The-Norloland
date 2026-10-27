# The Norloland

A single-player-first, procedurally generated voxel-world prototype built with **Godot 4.4 .NET / C#**.

## Current prototype

- First-person movement, jumping, mouse-look, and basic collision.
- Deterministic terrain generation from a seed.
- Random seed on launch by default; set \`WorldSeed\` in the Inspector to reuse a world.
- Chunks load around the player and unload when they are too far away.
- Blocky terrain surfaces, basic grass/dirt/stone colors, and procedurally placed low-poly trees.

This is an early technical prototype, not a finished game. Buildings, water, caves, biomes, block editing, saving, and gameplay systems are planned follow-up milestones.

## Requirements

- Godot **4.4.1 .NET** (the .NET/C# edition, not the standard edition)
- .NET 8 SDK
- Windows, Linux, or macOS

## Run it

1. Install Godot 4.4.1 .NET and the .NET 8 SDK.
2. Clone or download this repository.
3. Open \`project.godot\` in Godot.
4. Let Godot import the project and build the C# solution.
5. Press **F6** or **F5** to run.

## Controls

- **W A S D** — move
- **Space** — jump
- **Mouse** — look around
- **Esc** — release the mouse
- **Left click** — capture the mouse again

## World seed

The exported \`WorldSeed\` property on the Main node controls generation:
- \`0\` chooses a new random seed at launch.
- Any non-zero value makes terrain and tree placement repeatable for that seed.

The HUD displays the active seed.

## Development roadmap

1. **Prototype foundation** — player, terrain chunks, seed-based generation.
2. **World generation** — better biomes, water, rivers, caves, and terrain variety.
3. **Structures** — rule-based placement of cabins, ruins, farms, and settlements.
4. **World persistence** — save seed, player position, and changed world data.
5. **Gameplay** — gathering, building, survival, exploration, quests, and simulation.
6. **Optimization and packaging** — profiling, settings, and verified Windows builds.

## Notes

Version 0.1.1 builds terrain and collision first, then trees, grass, flowers, shrubs, and stones in separate frames. Construction remains on the main thread; each stage can still cause a frame spike, and completing the full view takes more frames. Background terrain computation and finer time budgets remain follow-up work. The HUD displays the version; increment Main.AppVersion with subsequent updates.
