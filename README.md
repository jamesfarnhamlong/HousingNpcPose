# Housing NPC Pose

Client-side Dalamud plugin for locally posing already-spawned housing NPCs in FFXIV apartments and houses.

**Stable/test checkpoint: v0.3.6 user guidance polish**

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
   Open `/hnpcpose`, scan the room, click **Edit** on the NPC, then choose a pose such as Sit, Chair Sit, Doze, Lean, Confirm, Scheme, Reprimand, Sweat, or Shiver.

5. **Adjust visual Y offset**
   Use the Y offset controls to align sitting/lying poses with furniture or raised surfaces. The offset is visual/client-side only.

6. **Hide nameplates if needed**
   Enable nameplate hiding if the NPC nameplate covers a vertically offset character.

7. **Save pose + Y**
   Click **Save selected pose + Y**. With auto-apply enabled, the plugin will restore the local scene after you leave and re-enter the housing area.

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
- Applies confirmed local pose params.
- Saves pose assignments by territory, NPC name, BaseId, and approximate position.
- Saves and reapplies optional visual Y offsets for furniture alignment.
- Optionally hides nameplates for posed/saved NPCs.
- Auto-applies saved poses after leaving/re-entering housing, plugin load, and territory change.
- Keeps debug object scanning and custom pose-param discovery behind advanced UI sections.

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
/hnpcpose offset <idx> <y>
/hnpcpose saveoffset <idx> <y>
```

## Confirmed useful pose params

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

## Known limitations

- Object indices are temporary and can change after reloads. Saved poses are matched using territory, NPC name, BaseId, and approximate position.
- Housing NPC limits are controlled by the game. In apartments, the practical cap appears to be small, which keeps the plugin target set manageable.
- Non-humanoid or creature NPCs may use incompatible skeletons and should remain blocked.
- Chair/bed/furniture interaction is not real server-side seating. Use Y offset to visually align poses.
- Visitors will not see your posed NPCs unless they use their own local tools/configuration.
