# Testing the prototype

## Local testing (Windows)

1. Install **Godot 4.4.1 .NET** and the **.NET 8 SDK**.
2. Clone this repository.
3. Open \`project.godot\` in Godot and allow the C# solution to build.
4. Run the project with F6/F5.

If the game does not launch, copy the first red error from Godot's Output panel and open an issue or include it in your next message.

## Windows executable

The GitHub Actions workflow \`.github/workflows/windows-build.yml\` attempts a Windows desktop export when relevant files are pushed or when manually started.

After a successful workflow:
1. Open the repository's **Actions** tab.
2. Select **Build Windows prototype**.
3. Open the latest successful run.
4. Download the **TheNorloland-Windows** artifact.

The artifact is temporary and retained for 14 days. A successful export has not been verified until the workflow completes successfully.

## Version 0.1.1: staged chunk construction

- Verify `v0.1.1` in the HUD and construction stages in F3.
- With the same nonzero seed, fixed view distance, and adaptive distance disabled, compare the same route on main and the performance branch. Record frame-time spikes while loading, not only average FPS.
- Wait for the queue and build stage to become idle; compare terrain, tree placement, and resource interactions. Decoration now arrives after the terrain.
- Cross chunk boundaries repeatedly, reduce view distance during loading, and fell a tree while a chunk is building. No job should continue decorating an unloaded or replaced chunk.
- Pause during loading and resume; construction should pause too.
- Dig during construction; remaining decoration should sample the updated height grid.

Validation in the editing environment: diff whitespace checks and code review only. Godot/.NET build and runtime profiling have not been executed.
