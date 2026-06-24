# Changelog

## v0.3.4 Offset range + nameplate hiding test

- Increased the local visual Y draw-offset clamp from +/-3.0 to +/-10.0.
- Added larger +/-0.25 Y adjustment buttons while keeping fine +/-0.01 and +/-0.05 controls.
- Added a setting to hide nameplates for posed/saved NPCs.
- Added `/hnpcpose nameplates on|off`.
- Nameplate hiding removes nameplate text/icons only for safe EventNpc targets with current plugin changes or enabled saved pose entries.

## v0.3.3a Y-offset save test

- Added explicit controls for saving Y offsets into existing saved pose entries.
- Added `/hnpcpose saveoffset <idx> <y>`.
- Added UI buttons: `Apply + save Y`, `Save Y only`, and `Reset + save Y`.
- Renamed the save button to make clear that it saves both pose and Y offset.

## v0.3.3 Y-offset test

- Added optional local visual Y draw offset per saved pose.
- Added Y offset controls to the pose lab.
- Added `/hnpcpose offset <idx> <y>` command.
- Saved pose entries now display Y offset when non-zero.
- Saved automation reapplies pose + Y offset together.
- Restore/reset clears the local draw offset along with plugin-applied pose snapshots.
- Increased default auto-apply retry window to 20 seconds.

## v0.3.2 stable checkpoint

- Polished UI labels and version text.
- Added saved-pose summary table.
- Moved object-discovery toggles into a debug section.
- Updated manifest and project metadata for GitHub checkpoint.
- Kept v0.3 saved automation behaviour unchanged.

## v0.3.1

- Fixed Dalamud SDK 15 build issues:
  - territory type handling uses `uint`;
  - `ImGui.SeparatorText` replaced with `Text` + `Separator`.
- Confirmed saved automation builds and runs.

## v0.3

- Added saved local pose assignments.
- Added manual apply saved poses.
- Added optional auto-apply after zoning/plugin load.
- Saved matching uses territory + NPC name + BaseId + approximate position.

## v0.2

- Added pose discovery lab and named pose buttons.
- Confirmed useful local pose params.

## v0.1.1

- Added non-humanoid / creature NPC safety blocklist.
- Confirmed Namazu Mender is detected but blocked.

## v0.1

- Confirmed local pose application and restore on humanoid housing EventNpc actors.

## v0.0

- Scanner-only prototype.
