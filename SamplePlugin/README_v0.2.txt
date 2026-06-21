Housing NPC Pose v0.2 - pose discovery patch

Replace these files in the current template/project:
- SamplePlugin.csproj
- HousingNpcPose.json
- Plugin.cs
- Configuration.cs
- Windows/ConfigWindow.cs
- Windows/MainWindow.cs

Known-safe baseline:
- /hnpcpose sit <idx>
- /hnpcpose restore <idx|all>

Pose discovery:
- /hnpcpose pos <idx> <param>
- /hnpcpose loop <idx> <param>
- /hnpcpose mode <idx> <pos|loop|normal> <param>

UI:
- Use Lab on a pose candidate row to select that actor.
- Use Sit as the confirmed baseline.
- Use Apply Pos param and Apply Loop param to test values 0-255.
- Restore after each test if the state is not useful.

Safety:
- EventNpc only.
- Known non-humanoid / creature NPCs are blocked by BaseId.
- Namazu Mender BaseId 1026171 is blocked.
- No packets, no spawning, no movement, no hooks, no other-client effects.
