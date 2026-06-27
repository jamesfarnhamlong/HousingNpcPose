# Housing NPC Pose

Client-side Dalamud plugin for locally posing already-spawned housing NPCs in FFXIV apartments and houses.

**Stable/test checkpoint: v0.4.1 scene presets**

## Purpose

Housing NPC Pose is a local housing scene tool. It is intended for decorators, roleplayers, and screenshot users who want houses and apartments to feel more lived in.

The plugin lets existing housing NPCs hold local client-side poses, useful gestures, visual Y offsets, and optional hidden nameplates. It does not alter server-side housing data and does not make visitors see your posed scenes.

## Typical workflow

1. **Place a housing NPC**
   Use the normal in-game housing system to place a vendor, mender, mannequin, permit NPC, or other supported housing NPC.

2. **Glamour the NPC locally**
   Use Glamourer/Penumbra if desired to change the NPC appearance on your client. This plugin does not handle appearance changes itself.

3. **Position the NPC**
   Place the NPC near a bed, bath, counter, chair, bench, table, or scene area using the game housing tools.

4. **Pose the NPC**
   Open `/hnpcpose`, scan the room, choose the NPC from the **Actor** dropdown, then use the searchable **Pose browser** or quick scene-pose buttons to apply a pose such as Sit, Chair Sit, Doze, Lean, Study, Savour Tea, Guard, Scheme, or Reprimand.

5. **Adjust visual Y offset**
   Use the Y offset slider and nudge buttons to align sitting/lying poses with furniture or raised surfaces. The offset is visual/client-side only.

6. **Hide nameplates if needed**
   Enable nameplate hiding if the NPC nameplate covers a vertically offset character.

7. **Save pose + Y**
   Click **Apply + save pose/Y** or **Save current pose/Y**. With auto-apply enabled, the plugin will restore the local scene after you leave and re-enter the housing area.

8. **Capture a scene preset**
   Once several NPCs are saved, use **Scene presets** to save the current room state as a named scene. Loading a scene replaces the current area saved assignments with that preset, then applies them locally.

## Recommended companion tools

Housing NPC Pose works well alongside:

- **Glamourer** for local appearance changes.
- **Penumbra** for local modded assets.
- The game’s normal housing placement tools for positioning permitted NPCs and mannequins.

This plugin does not replace those tools. It only handles local pose/offset/nameplate scene dressing after the NPC exists.

## Current features

- Scans visible housing NPCs and pose candidates.
- Detects housing `EventNpc` actors.
- Blocks known non-humanoid / creature NPCs such as Namazu Mender.
- Applies confirmed local pose params from a central catalogue/crosswalk.
- Saves pose assignments by territory, NPC name, BaseId, and approximate position.
- Saves and reapplies optional visual Y offsets for furniture alignment.
- Saves and loads named room scene presets made from the current area saved pose assignments.
- Optionally hides nameplates for posed/saved NPCs.
- Auto-applies saved poses after leaving/re-entering housing, plugin load, and territory change.
- Keeps debug object scanning and custom pose-param discovery behind advanced UI sections.



## New in v0.4.1

- Adds named scene presets for the current housing area.
- A scene snapshots all current saved actor pose assignments, including pose, Y offset, matching identity/position, and enabled state.
- Adds a Scene presets section to save current room state as a new scene, load a scene, overwrite a scene from current saved poses, rename a scene, and delete a scene.
- Loading a scene replaces the current area saved pose assignments with the scene contents, then applies them locally.
- Keeps random scene loading for a later pass so manual scene save/load can be tested safely first.
- No changes to the underlying pose engine, actor matching, Y offsets, nameplate hiding, or pose catalogue.

## New in v0.4.0

- Redesigns the main window around a friendlier scene-editing workflow.
- Adds an actor dropdown so normal use no longer requires typing object indices.
- Adds a searchable pose browser with category filters and per-row Apply / Apply + save buttons.
- Adds quick scene-pose buttons for common housing uses such as chair sit, doze, lean, study, tea, guard, slump, scheme, and reprimand.
- Reworks visual Y offset into a slider plus precise nudge buttons.
- Moves the raw object table, manual index tools, and discovery logger further into Advanced so normal posing is less cluttered.
- Keeps the existing stable pose application, saved automation, Y offset, nameplate hiding, and catalogue data unchanged.

## New in v0.3.10

- Expands the built-in pose catalogue using live-tested InPositionLoop observations from params 1-100.
- Adds clearer categories: Core, Gesture, Dance, Exercise, Performance, Prop, Experimental, and Unknown.
- Keeps the CSV fields as crosswalk metadata only; the small HousingNpcPose param remains the source of truth.
- Documents that standard General emotes such as /bow, /welcome, and /wave are not currently exposed through the small param route used by this plugin.
- Cleans the project file name from `HousingNpcPose.csproj` to `HousingNpcPose.csproj`.

## New in v0.3.9

- Added an Advanced pose discovery logger for mapping what params 0-255 visibly do.
- Added Previous / Current / Next buttons for stepping through params on a selected safe NPC.
- Added observation fields for name, category, confidence, and notes.
- Saves local tester observations into plugin configuration.
- Added an observation table and CSV copy/export button so results can be reconciled with the uploaded emote/action CSVs later.
- No changes to the core actor-pose, saved pose, Y offset, nameplate, or auto-apply logic.

## New in v0.3.8

- Reworked the pose catalogue into an explicit crosswalk between live-tested HousingNpcPose params and CSV emote/action metadata.
- Added confidence levels so confirmed live params are separated from likely/uncertain/experimental entries.
- Added observed behaviour, CSV section, CSV row/ordinal, CSV ID, emote_start, optional ActionTimeline hints, and notes.
- Updated the Advanced catalogue table to make the distinction clear: the small HousingNpcPose param is still the value applied by this plugin; CSV IDs are reference metadata only.
- No behavioural changes to saved poses, Y offsets, nameplate hiding, or auto-apply.

## New in v0.3.7

- Added a central pose catalogue/crosswalk for known HousingNpcPose params.
- The UI now reads core, gesture, dance, and experimental entries from the same catalogue.
- Saved pose display resolves friendly names from the catalogue where possible.
- Added an Advanced pose catalogue table showing param, friendly name, category, safety level, and optional CSV/ActionTimeline reference notes.
- Custom numeric params remain available for live discovery.

## New in v0.3.6

- Added user-facing workflow guidance in the main plugin window.
- Expanded README instructions around the intended housing/Glamourer/Penumbra workflow.
- Clarified the plugin identity as a local scene-dressing tool rather than a general emote tester.

## Safety scope

This plugin is local-client only.

It does not:

- send packets
- spawn objects
- move NPCs server-side
- affect other clients
- sync poses to visitors
- apply anything to other players
- change server-side housing state
- glamour NPCs or manage Penumbra/Glamourer state

## Commands

```text
/hnpcpose
/hnpcpose scan
/hnpcpose applysaved
/hnpcpose auto on
/hnpcpose auto off
/hnpcpose nameplates on
/hnpcpose nameplates off
/hnpcpose restore all
/hnpcpose poses
# Advanced UI: pose discovery logger for recording 0-255 behaviour
/hnpcpose offset <idx> <y>
/hnpcpose saveoffset <idx> <y>
```

## Pose catalogue

The plugin uses a small local pose-param catalogue. The param is the value used by HousingNpcPose's current `InPositionLoop` method; CSV/ActionTimeline IDs are reference metadata only and are not the same number system.

Core/useful confirmed entries:

```text
1  Sit / ground sit
2  Chair / bench sit
3  Doze / bed lie-down
42 Sweat
43 Shiver
47 Confirm
48 Scheme
51 Reprimand
55 Lean
```

Additional discovered entries, such as Step Dance, Harvest Dance, Ball Dance, Manderville Dance, Wasshoi, Lali-hop, Hildibrand/Omega-style animations, and unknown dance slots, are kept in the Advanced catalogue/discovery area rather than promoted as core scene poses.

The v0.3.9 discovery logger is for testing the remaining 0-255 values in a controlled way. Observations are saved as local notes first, then useful entries can be promoted into the built-in catalogue after verification.

## Known limitations

- Object indices are temporary and can change after reloads. Saved poses are matched using territory, NPC name, BaseId, and approximate position.
- Housing NPC limits are controlled by the game. In apartments, the practical cap appears to be small, which keeps the plugin target set manageable.
- Non-humanoid or creature NPCs may use incompatible skeletons and should remain blocked.
- Chair/bed/furniture interaction is not real server-side seating. Use Y offset to visually align poses.
- Visitors will not see your posed NPCs unless they use their own local tools/configuration.
