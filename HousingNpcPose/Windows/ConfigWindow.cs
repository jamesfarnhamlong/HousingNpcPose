using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HousingNpcPose.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("Housing NPC Pose Settings###HousingNpcPoseConfig")
    {
        Size = new Vector2(580, 380);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextWrapped("v0.3.10. Saved local pose assignments and optional visual Y offsets can be auto-applied after entering/reloading an area. Nameplate hiding can remove labels from posed/saved NPCs. Everything is client-side only.");

        ImGui.Spacing();
        ImGui.Text("Intended workflow");
        ImGui.Separator();
        ImGui.TextWrapped("Place a housing NPC with the game housing tools, glamour it locally if desired, then use /hnpcpose to apply pose, visual Y offset, nameplate hiding, and saved auto-apply. This plugin does not create, move, glamour, or sync NPCs; it only adds local scene dressing on top of existing housing actors.");

        ImGui.Spacing();
        ImGui.Text("Automation");
        ImGui.Separator();
        ImGui.TextUnformatted($"Saved pose entries: {configuration.SavedPoses.Count} | Discovery observations: {configuration.PoseObservations?.Count ?? 0}");

        var autoApply = configuration.AutoApplySavedPoses;
        if (ImGui.Checkbox("Auto-apply saved poses after zoning/plugin load", ref autoApply))
        {
            configuration.AutoApplySavedPoses = autoApply;
            configuration.Save();
        }

        var retrySeconds = configuration.AutoApplyRetrySeconds;
        if (ImGui.InputInt("Auto-apply retry seconds", ref retrySeconds))
        {
            configuration.AutoApplyRetrySeconds = Math.Clamp(retrySeconds, 1, 60);
            configuration.Save();
        }

        var tolerance = configuration.SavedPosePositionTolerance;
        if (ImGui.InputFloat("Saved NPC position tolerance", ref tolerance, 0.05f, 0.25f, "%.2f"))
        {
            configuration.SavedPosePositionTolerance = Math.Clamp(tolerance, 0.05f, 5.0f);
            configuration.Save();
        }

        var hideNameplates = configuration.HideNameplatesForPosedNpcs;
        if (ImGui.Checkbox("Hide nameplates for posed/saved NPCs", ref hideNameplates))
        {
            configuration.HideNameplatesForPosedNpcs = hideNameplates;
            configuration.Save();
            Plugin.NamePlateGui.RequestRedraw();
        }

        ImGui.TextWrapped($"Y offsets use the local draw offset only. They are visual/client-side and are reapplied with saved poses; they do not change server housing placement. Current allowed range: {Plugin.MinLocalYOffset:0.##} to +{Plugin.MaxLocalYOffset:0.##}.");

        ImGui.Spacing();
        ImGui.Text("Scanner visibility");
        ImGui.Separator();

        var showAll = configuration.ShowAllNonPlayerObjects;
        if (ImGui.Checkbox("Show all non-player objects", ref showAll))
        {
            configuration.ShowAllNonPlayerObjects = showAll;
            configuration.Save();
        }

        var includeHousingEventObjects = configuration.IncludeHousingEventObjects;
        if (ImGui.Checkbox("Include HousingEventObject rows", ref includeHousingEventObjects))
        {
            configuration.IncludeHousingEventObjects = includeHousingEventObjects;
            configuration.Save();
        }

        var includeEventObjects = configuration.IncludeEventObjects;
        if (ImGui.Checkbox("Include EventObj rows / exits", ref includeEventObjects))
        {
            configuration.IncludeEventObjects = includeEventObjects;
            configuration.Save();
        }

        var includeRetainers = configuration.IncludeRetainers;
        if (ImGui.Checkbox("Include Retainer candidates", ref includeRetainers))
        {
            configuration.IncludeRetainers = includeRetainers;
            configuration.Save();
        }

        var includeBattleNpcs = configuration.IncludeBattleNpcs;
        if (ImGui.Checkbox("Include BattleNpc candidates", ref includeBattleNpcs))
        {
            configuration.IncludeBattleNpcs = includeBattleNpcs;
            configuration.Save();
        }
    }
}
