HousingNpcPose v0.1 combined patch
==================================

This patch combines:
- v0.0 scanner cleanup
- one tiny experimental local-only pose test path

Copy these files over the current project:
- SamplePlugin.csproj
- HousingNpcPose.json
- Plugin.cs
- Configuration.cs
- Windows/ConfigWindow.cs
- Windows/MainWindow.cs

Important:
- The manifest must be named HousingNpcPose.json because the AssemblyName is HousingNpcPose.
- If SamplePlugin.json still exists in the project, it can be removed or ignored.
- The csproj enables AllowUnsafeBlocks because the pose test casts EventNpc addresses to FFXIVClientStructs Character*.

Commands:
- /hnpcpose
- /hnpcpose scan
- /hnpcpose clear
- /hnpcpose config
- /hnpcpose test <idx> [param]
- /hnpcpose pos <idx> <param>
- /hnpcpose loop <idx> <param>
- /hnpcpose normal <idx>
- /hnpcpose restore <idx|all>

Suggested first test:
1. Enter apartment/house.
2. /hnpcpose scan
3. Pick a normal EventNpc, e.g. Estate Maidservant.
4. /hnpcpose test 2
5. If nothing visible happens, try:
   /hnpcpose pos 2 2
   /hnpcpose pos 2 3
   /hnpcpose loop 2 1
6. Restore:
   /hnpcpose restore 2
   or /hnpcpose restore all

This build refuses to pose anything that is not ObjectKind.EventNpc.
It does not send packets, spawn objects, move NPCs, or affect other clients.
