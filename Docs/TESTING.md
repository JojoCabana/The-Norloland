# Testing the prototype

## Local testing (Windows)

1. Install **Godot 4.7.2 .NET** and the **.NET 8 SDK**.
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

## Version 0.1.2: autosave and slot inventory

- Dig across a chunk seam, collect stones/sticks, craft a tool, rearrange and split stacks (right click). Close and restart: seed, holes, consumed resources, counts and slot arrangement must remain.
- With `WorldSeed = 0`, restart resumes the last seed. An explicit different seed uses its own save; returning to the first seed restores its changes.
- Drag onto an empty slot, a different item, and a nearly full identical stack. Quantities must be conserved; maximum stack size is 99. Collect and craft after rearranging.
- Grow/harvest a bush, leave/reload its chunk and restart. Harvest state must persist; growth checkpoints occur every five seconds and at normal shutdown.
- Back up saves before testing damaged/unwritable saves. Verify the HUD reports SPEICHERFEHLER and an unreadable existing save is not overwritten.
- Compare rendering and interaction on the laptop. Build/runtime validation is still required; no successful build is claimed by this change.

Saves are local to this machine under Godot `user://worlds`, not committed to GitHub. World mutations and inventory actions write snapshots synchronously; very large saves may cause disk-related frame spikes.

## Version 0.1.4: Godot 4.7.2 .NET

The C# SDK and CI editor/export templates target 4.7.2. Desktop target remains net8.0, matching the upstream 4.7.2 project generator. Use the .NET edition of the editor. Restart the editor after pulling and rebuild the solution. CI now also builds pushes to the performance branch. A successful build/export and runtime test must be confirmed separately.

## Version 0.1.5: visible harvesting and persistent drops

- Aim at a stone deposit with the pickaxe selected and hold left click. The bar above the target must advance over all five hits. Switching targets or releasing cancels progress for the current hit; completed hits persist.
- On the final hit, fragments animate and one large plus one small collectible stone appear. Inventory only increases after pressing E on each drop. Restart before collecting: drops must survive, the original deposit must stay absent.
- A green bush is distinct from bare branches. One left click breaks it into 1–4 forked wooden sticks. E collects each stick; E on the intact bush only explains the controls.
- Leave/reload the chunk and restart before/after collecting branches: no original bush or collected drop may reappear. Existing saves without Drops/Health must load.
- Open the inventory while mining; the progress bar must hide and mining must stop.

This is a short procedural fragment effect, not a physics-based fracture simulation.
