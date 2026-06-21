Housing NPC Pose v0.3 — saved pose automation
================================================

Replace these files in the current HousingNpcPose project:

- SamplePlugin.csproj
- HousingNpcPose.json
- Plugin.cs
- Configuration.cs
- Windows/ConfigWindow.cs
- Windows/MainWindow.cs

New v0.3 features:

- Save selected pose for a selected safe EventNpc.
- Saved entries match by current territory + NPC name + BaseId + approximate position.
- Apply saved poses manually with the UI or /hnpcpose applysaved.
- Optional auto-apply saved poses after zoning/plugin load.
- Auto-apply retries gently once per second for a configurable short window while actors load.

Commands:

/hnpcpose
/hnpcpose scan
/hnpcpose save <idx> <poseName|param>
/hnpcpose clearsaved <idx|area>
/hnpcpose applysaved
/hnpcpose auto on|off
/hnpcpose restore <idx|all>

Known useful pose names:

sit, bench, doze, lean, confirm, scheme, reprimand, sweat, shiver

Safety:

- EventNpc only.
- Known non-humanoid / creature NPCs are blocked by BaseId.
- Namazu Mender BaseId 1026171 is blocked.
- No packets, spawning, movement, hooks, or other-client effects.
