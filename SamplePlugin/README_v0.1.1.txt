Housing NPC Pose v0.1.1 — safety filter patch

Replace these files in your fresh/current HousingNpcPose project:

- SamplePlugin.csproj
- HousingNpcPose.json
- Plugin.cs
- Configuration.cs
- Windows/ConfigWindow.cs
- Windows/MainWindow.cs

What changed from v0.1:

- Adds a blocked BaseId list for known non-humanoid / creature EventNpc rows.
- Blocks BaseId 1026171: Namazu Mender.
- UI now shows blocked EventNpc rows with a disabled action cell.
- Commands refuse to apply pose modes to blocked EventNpc rows.
- Restore still works for previously saved snapshots.

Commands remain:

/hnpcpose
/hnpcpose scan
/hnpcpose clear
/hnpcpose test <idx> [param]
/hnpcpose pos <idx> <param>
/hnpcpose loop <idx> <param>
/hnpcpose normal <idx>
/hnpcpose restore <idx|all>

This is still local-client only. It does not move NPCs, spawn objects, send packets, hook anything, or affect other clients.
