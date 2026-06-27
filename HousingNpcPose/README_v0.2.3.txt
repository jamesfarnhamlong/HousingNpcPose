HousingNpcPose v0.2.3 target selector + useful named poses

Replace:
- Plugin.cs
- Windows/MainWindow.cs
- HousingNpcPose.json

This patch is based on the last confirmed working v0.2.2b build.

New useful named poses:
- sit = Param 1
- bench/chair = Param 2
- doze = Param 3
- sweat = Param 42
- shiver = Param 43
- confirm = Param 47
- scheme = Param 48
- reprimand = Param 51
- lean = Param 55

Fun/test named poses remain available but are tucked under the debug section.
The UI now has Set Target per row, so the lab no longer assumes object index 2 remains the same after reloads.
