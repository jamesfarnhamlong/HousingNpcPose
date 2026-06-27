using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace HousingNpcPose;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 11;

    /// <summary>
    /// Discovery mode. When enabled, the scanner lists every non-player object rather than only likely NPC candidates.
    /// Useful for finding out how housing permits and interactables are represented in the object table.
    /// </summary>
    public bool ShowAllNonPlayerObjects { get; set; } = false;

    /// <summary>
    /// Include HousingEventObject rows such as orchestrions, armoires, bells, etc.
    /// These are useful for debugging but are not expected to be poseable.
    /// </summary>
    public bool IncludeHousingEventObjects { get; set; } = false;

    /// <summary>
    /// Include EventObj rows such as room exits/doors.
    /// These are useful for debugging but are not pose targets.
    /// </summary>
    public bool IncludeEventObjects { get; set; } = false;

    /// <summary>
    /// Some NPC-like actors may appear as Retainer. Keep this available but off unless needed.
    /// </summary>
    public bool IncludeRetainers { get; set; } = false;

    /// <summary>
    /// Some NPC-like actors might appear as BattleNpc. Keep this available but off unless needed.
    /// </summary>
    public bool IncludeBattleNpcs { get; set; } = false;

    /// <summary>
    /// Automatically reapply saved local poses for matching NPCs after entering/reloading an area.
    /// </summary>
    public bool AutoApplySavedPoses { get; set; } = false;

    /// <summary>
    /// Hide nameplate text/icons for NPCs that have a saved or currently applied local pose.
    /// This is frame-scoped UI modification only; it does not change object names or server data.
    /// </summary>
    public bool HideNameplatesForPosedNpcs { get; set; } = false;

    /// <summary>
    /// How long after zoning/plugin load to keep retrying saved-pose application while actors finish loading.
    /// </summary>
    public int AutoApplyRetrySeconds { get; set; } = 20;

    /// <summary>
    /// Max distance in yalms/metres-ish for matching a saved NPC position. Housing NPCs should be static,
    /// but a little tolerance helps with small float differences.
    /// </summary>
    public float SavedPosePositionTolerance { get; set; } = 0.75f;

    /// <summary>
    /// Saved local pose assignments. These are plugin-only records; they do not change server data.
    /// </summary>
    public List<SavedPoseEntry> SavedPoses { get; set; } = new();

    /// <summary>
    /// Local discovery notes for mapping 0-255 pose params to observed behaviour.
    /// These are user/tester notes only; they do not affect actor application unless promoted into PoseCatalogue.cs later.
    /// </summary>
    public List<PoseObservationEntry> PoseObservations { get; set; } = new();

    /// <summary>
    /// Named room scene presets. Each scene is a snapshot of saved pose assignments for one territory.
    /// Loading a scene replaces the current territory's saved assignments with the scene contents, then applies them locally.
    /// </summary>
    public List<ScenePresetEntry> ScenePresets { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public class SavedPoseEntry
{
    public bool Enabled { get; set; } = true;
    public uint TerritoryType { get; set; }
    public string TerritoryLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint BaseId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public string PoseLabel { get; set; } = string.Empty;
    public byte PoseParam { get; set; }

    /// <summary>
    /// Local draw Y offset applied after the pose. This is visual/client-side only and does not change server housing placement.
    /// </summary>
    public float OffsetY { get; set; } = 0.0f;

    public string DisplayText
    {
        get
        {
            var poseText = $"{PoseCatalogue.GetSavedDisplayName(PoseParam, PoseLabel)} ({PoseParam})";
            if (Math.Abs(OffsetY) > 0.001f)
                poseText += $" Y{OffsetY:+0.00;-0.00;0.00}";

            return Enabled ? poseText : $"Disabled: {poseText}";
        }
    }
}

[Serializable]
public class PoseObservationEntry
{
    public string Mode { get; set; } = "InPositionLoop";
    public byte Param { get; set; }
    public string ObservedName { get; set; } = string.Empty;
    public string Category { get; set; } = "Unknown";
    public string Confidence { get; set; } = "Uncertain";
    public string Notes { get; set; } = string.Empty;
    public uint TerritoryType { get; set; }
    public string TerritoryLabel { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public uint TargetBaseId { get; set; }
    public string UpdatedAtUtc { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(ObservedName) ? $"{Mode} {Param}" : ObservedName;
}



[Serializable]
public class ScenePresetEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Scene";
    public uint TerritoryType { get; set; }
    public string TerritoryLabel { get; set; } = string.Empty;
    public string CreatedAtUtc { get; set; } = string.Empty;
    public string UpdatedAtUtc { get; set; } = string.Empty;
    public List<SavedPoseEntry> Poses { get; set; } = new();

    public string DisplayText
    {
        get
        {
            var countText = Poses.Count == 1 ? "1 actor" : $"{Poses.Count} actors";
            return $"{Name} — {countText}";
        }
    }
}
