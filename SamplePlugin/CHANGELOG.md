# Changelog

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
