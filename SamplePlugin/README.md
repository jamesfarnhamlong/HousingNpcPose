# Housing NPC Pose

Client-side Dalamud plugin for applying local pose/emote-loop states to already-spawned housing NPCs in FFXIV housing interiors.

## Current checkpoint

**v0.3.2 stable checkpoint**

Confirmed working:

- Scan visible local housing NPC/object candidates.
- Pose safe humanoid `EventNpc` housing NPCs locally.
- Block known non-humanoid / creature NPCs, currently including Namazu Mender.
- Save pose assignments matched by territory, NPC name, BaseId, and approximate position.
- Manually apply saved poses.
- Optionally auto-apply saved poses after entering/reloading the housing area.

Confirmed useful pose params:

| Param | Label |
|---:|---|
| 1 | Sit / ground sit |
| 2 | Chair / bench sit |
| 3 | Doze / bed lie |
| 42 | Sweat |
| 43 | Shiver |
| 47 | Confirm |
| 48 | Scheme |
| 51 | Reprimand |
| 55 | Lean |

## Safety scope

This plugin is intentionally narrow.

It does **not**:

- send packets,
- spawn objects or actors,
- move NPCs,
- affect other clients,
- apply changes to other players,
- hook game functions,
- sync anything across clients.

All effects are local visual state changes on existing housing NPC actors already loaded by the client.

## Commands

```text
/hnpcpose
/hnpcpose scan
/hnpcpose config
/hnpcpose pose <idx> <sit|bench|doze|lean|confirm|scheme|reprimand|sweat|shiver|normal>
/hnpcpose pos <idx> <param>
/hnpcpose restore <idx|all>
/hnpcpose save <idx> <poseName|param>
/hnpcpose clearsaved <idx|area>
/hnpcpose applysaved
/hnpcpose auto on|off
```

## Development notes

Object indices are temporary. Saved automation does not use object index as the stable identifier; it uses area + name + BaseId + approximate position.
