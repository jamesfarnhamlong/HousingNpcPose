# Changelog

## v0.4.0 Housing scene UI redesign

- Reworked the main window into a more human-friendly actor → pose → Y offset → save workflow.
- Added an actor dropdown so normal posing no longer requires typing object indices.
- Added a searchable pose browser with category filtering and Apply / Apply + save actions.
- Added quick scene-pose buttons for common housing choices such as chair sit, doze, lean, study, tea, guard, slump, scheme, and reprimand.
- Changed visual Y offset controls to a slider plus precision nudge buttons.
- Kept the room actor overview available but no longer made the table the primary workflow.
- Left raw object-index entry, scanner filters, pose discovery, observation logging, and catalogue crosswalks in Advanced.
- No changes to the underlying client-side pose application, saved pose matching, Y offset application, nameplate hiding, or auto-apply logic.

## v0.3.10 Expanded observed catalogue and project cleanup

- Expanded `PoseCatalogue.cs` from live-tested 1-100 InPositionLoop observations.
- Added clearer categories for dances, exercises, performance/cheer poses, prop/food/drink poses, experimental entries, and unknowns.
- Kept CSV emote/action IDs as crosswalk metadata only rather than pretending there is a direct formula.
- Documented that standard General emotes such as `/bow`, `/welcome`, and `/wave` are not currently surfaced through this small-param route.
- Renamed the project file from `SamplePlugin.csproj` to `HousingNpcPose.csproj` to remove the remaining template path/name.

## v0.3.9 Pose discovery logger

- Added an Advanced pose discovery logger for systematically testing params 0-255.
- Added Previous / Current / Next apply buttons for the selected discovery mode.
- Added local observation fields for observed name, category, confidence, and notes.
- Saves observations into plugin configuration without changing actor application logic.
- Added an observations table and CSV copy/export button for later reconciliation against the uploaded CSV data.
- Kept the built-in PoseCatalogue.cs as the curated source; observations are local tester notes until promoted.

## v0.3.8 Catalogue crosswalk cleanup

- Rebuilt the pose catalogue into an explicit live-param-to-CSV crosswalk.
- Added confidence levels for catalogue entries: Confirmed, Likely, Uncertain, Experimental, and ReferenceOnly.
- Added observed behaviour, CSV section, CSV row, CSV ordinal, CSV ID, emote_start, optional ActionTimeline hints, and notes where available.
- Updated the Advanced pose catalogue table to show observed behaviour and crosswalk metadata separately.
- Kept HousingNpcPose params as the source of truth; CSV data is reference evidence only, not a direct conversion formula.
- No changes to actor application, saved automation, visual Y offsets, or nameplate hiding.

## v0.3.7 Pose catalogue cleanup

- Added a central pose catalogue/crosswalk for known HousingNpcPose params.
- Replaced scattered hardcoded pose labels with catalogue lookups.
- Added an Advanced pose catalogue table showing param, friendly name, category, safety level, aliases, and optional CSV/ActionTimeline reference notes.
- Saved pose display now resolves friendly names from the catalogue where possible.
- Kept custom numeric param discovery available for unknown values.
- No changes to saved automation, visual Y offsets, nameplate hiding, or actor application logic.

## v0.3.6 User guidance polish

- Added a collapsible quick-start workflow section to the main window.
- Expanded README usage instructions for the intended housing + Glamourer/Penumbra workflow.
- Clarified the purpose as local housing scene dressing for roleplay/decor/screenshot use.
- No behavioural changes to pose saving, Y offsets, nameplate hiding, or auto-apply.

## v0.3.5 UI polish

- Reworked the main window into a cleaner scene-editor layout.
- Added a compact NPC scene table for normal use.
- Added a selected-NPC editor with core pose buttons, useful gesture buttons, Y-offset controls, and save/apply actions.
- Moved raw object-table data, scanner filters, saved assignment details, and custom param discovery into Advanced / discovery tools.
- Kept v0.3.4 behaviour intact: saved automation, visual Y offsets, and nameplate hiding.

## v0.3.4 Offset range + nameplate hiding test

- Increased local visual Y offset range to -10 / +10.
- Added medium Y adjustment buttons.
- Added optional nameplate hiding for posed/saved NPCs.
- Added `/hnpcpose nameplates on|off`.

## v0.3.3a Save Y offset

- Added explicit saving of Y offsets into existing saved pose entries.
- Added `/hnpcpose saveoffset <idx> <y>`.
- Added UI buttons for applying/saving/resetting Y offsets.

## v0.3.3 Y offset test

- Added optional visual Y offsets using local draw offsets.
- Saved Y offset with pose assignments.
- Auto-applied pose plus Y offset together.

## v0.3.2 Stable checkpoint

- Saved local pose automation confirmed working.
- Pushed and tagged as stable checkpoint.
