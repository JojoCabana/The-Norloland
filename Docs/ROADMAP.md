# The Norloland — Development Roadmap

Current decisions and verified status: [Session handover 2026-10-27](PROJEKTSTAND_2026-10-27.md). This roadmap describes long-term milestones; partial implementations are detailed in that handover.

## Next priorities after 0.1.5
- [ ] Complete laptop runtime/restart tests for saving, slots, harvesting and chunk transitions.
- [ ] Separate world state, game commands, persistence and UI before expanding gameplay for eventual authoritative multiplayer. Networking itself is deferred.
- [ ] Persist player position and review synchronous save costs.
- [ ] Profile chunk construction stages before adding background computation.

## Milestone 0: Playable technical prototype
- [x] Godot 4 .NET project structure
- [x] First-person controller and mouse look
- [x] Seeded height-based voxel terrain
- [x] Dynamic chunk load/unload around the player
- [x] Procedural low-poly tree placement
- [ ] Run and validate in Godot .NET on Windows

## Milestone 1: Make the world feel natural
- [ ] More distinct biomes and smoother biome boundaries
- [ ] Water bodies, rivers, shorelines
- [ ] Better tree varieties, rocks, and ground details
- [ ] Reduce visible seams and improve chunk borders

## Milestone 2: Real voxel terrain
- [ ] Store individual block data per chunk
- [ ] Break and place blocks
- [ ] Greedy meshing and asynchronous generation
- [ ] Cave generation and underground layers

## Milestone 3: Structures
- [ ] Structure templates and placement rules
- [ ] Cabins, ruins, farms, and small settlements
- [ ] Biome-aware orientation and terrain checks
- [ ] Deterministic placement from the world seed

## Milestone 4: Saving and gameplay
- [ ] Save seed and player position
- [ ] Persist modified chunks
- [ ] Inventory, resources, crafting, and building
- [ ] Survival systems and exploration objectives
- [ ] NPCs and quests

## Milestone 5: Performance and release
- [ ] Profile on the target PC
- [ ] Worker-thread generation
- [ ] Graphics and gameplay settings
- [x] Automated Windows build/export for Godot 4.7.2 .NET (0.1.4 and 0.1.5 CI succeeded)
- [ ] Verify a downloadable Windows build
