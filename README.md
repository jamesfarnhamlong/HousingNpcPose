# Housing NPC Pose

Client-side Dalamud plugin for locally posing housing NPCs in FFXIV apartments and houses.

## Current status

Stable checkpoint: v0.3.2

Confirmed features:

- Scans visible housing NPCs.
- Detects housing `EventNpc` actors.
- Blocks known non-humanoid / creature NPCs such as Namazu Mender.
- Applies local-only pose parameters.
- Saves pose assignments by territory, NPC name, BaseId, and approximate position.
- Auto-applies saved poses after leaving and re-entering the apartment/house.

## Safety scope

This plugin is local-client only.

It does not:

- send packets
- spawn objects
- move NPCs
- affect other clients
- sync poses to visitors
- apply anything to other players
- change server-side housing state

## Commands

```text
/hnpcpose
/hnpcpose scan
/hnpcpose applysaved
/hnpcpose auto on
/hnpcpose auto off
/hnpcpose restore all