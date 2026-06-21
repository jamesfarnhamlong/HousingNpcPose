using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace HousingNpcPose.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private ushort labObjectIndex;
    private int labParam = 1;
    private byte labLastPoseParam = 1;
    private string labLastPoseLabel = "Sit / ground sit";
    private string labTargetName = "<none>";
    private uint labTargetBaseId;
    private string labTargetPosition = "-";

    public MainWindow(Plugin plugin)
        : base("Housing NPC Pose v0.3.2###HousingNpcPoseMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(980, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Housing NPC Pose v0.3.2 — stable saved local pose automation");
        ImGui.TextWrapped("This stable checkpoint reads the local object table, blocks known non-humanoid / creature NPCs, applies confirmed local pose params, and can save/reapply poses for matching NPCs in the same area. It does not move NPCs, spawn anything, hook anything, send packets, or affect other clients.");

        ImGui.Spacing();

        ImGui.TextUnformatted($"Current area: {plugin.GetTerritoryLabel()}");

        if (plugin.LastScanTime is { } lastScan)
            ImGui.TextUnformatted($"Last scan: {lastScan:HH:mm:ss}");
        else
            ImGui.TextUnformatted("Last scan: not scanned yet");

        ImGui.Spacing();

        if (ImGui.Button("Scan visible objects"))
            plugin.ScanObjects();

        ImGui.SameLine();

        if (ImGui.Button("Clear"))
            plugin.ClearScan();

        ImGui.SameLine();

        if (ImGui.Button("Restore all"))
            plugin.RestoreAllActorModes();

        ImGui.SameLine();

        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        ImGui.Spacing();

        DrawAutomationControls();

        ImGui.Spacing();

        DrawSavedPoseSummary();

        ImGui.Spacing();

        DrawInlineScannerToggles();

        ImGui.Spacing();
        DrawPoseLab();
        ImGui.Spacing();

        var results = plugin.ScanResults;
        ImGui.TextUnformatted($"Results: {results.Count}");

        using var child = ImRaii.Child("HousingNpcPoseResults", Vector2.Zero, true);
        if (!child.Success)
            return;

        if (results.Count == 0)
        {
            ImGui.TextWrapped("No scan results yet. Enter your house/apartment/private chamber, then click Scan visible objects or run /hnpcpose scan.");
            return;
        }

        DrawResultsTable(results);
    }

    private void DrawAutomationControls()
    {
        var currentTerritory = Plugin.ClientState.TerritoryType;
        var totalSaved = plugin.Configuration.SavedPoses.Count;
        var currentAreaSaved = plugin.Configuration.SavedPoses.Count(entry => entry.TerritoryType == currentTerritory);

        ImGui.TextUnformatted($"Automation: {(plugin.Configuration.AutoApplySavedPoses ? "ON" : "OFF")} | Saved in this area: {currentAreaSaved} | Total saved: {totalSaved}");

        var autoApply = plugin.Configuration.AutoApplySavedPoses;
        if (ImGui.Checkbox("Auto-apply saved poses", ref autoApply))
        {
            plugin.Configuration.AutoApplySavedPoses = autoApply;
            plugin.Configuration.Save();
            if (autoApply)
                plugin.ScheduleAutoApply("UI toggle");
        }

        ImGui.SameLine();

        if (ImGui.SmallButton("Apply saved now"))
            plugin.ApplySavedPosesNow();

        ImGui.SameLine();

        if (ImGui.SmallButton("Clear saved for this area"))
            plugin.ClearAllSavedPosesForCurrentTerritory();
    }

    private void DrawSavedPoseSummary()
    {
        using var node = ImRaii.TreeNode("Saved pose assignments");
        if (!node.Success)
            return;

        var currentTerritory = Plugin.ClientState.TerritoryType;
        var entries = plugin.Configuration.SavedPoses
            .OrderByDescending(entry => entry.TerritoryType == currentTerritory)
            .ThenBy(entry => entry.TerritoryLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entries.Length == 0)
        {
            ImGui.TextWrapped("No saved pose assignments yet. Set a target, apply a pose, then click Save selected pose for target.");
            return;
        }

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseSavedPoseTable", 7, tableFlags))
            return;

        ImGui.TableSetupColumn("Area");
        ImGui.TableSetupColumn("NPC");
        ImGui.TableSetupColumn("Pose");
        ImGui.TableSetupColumn("BaseId");
        ImGui.TableSetupColumn("Position");
        ImGui.TableSetupColumn("Enabled");
        ImGui.TableSetupColumn("Scope");
        ImGui.TableHeadersRow();

        foreach (var entry in entries)
        {
            ImGui.TableNextRow();
            TableText(string.IsNullOrWhiteSpace(entry.TerritoryLabel) ? entry.TerritoryType.ToString() : entry.TerritoryLabel);
            TableText(string.IsNullOrWhiteSpace(entry.Name) ? "<no name>" : entry.Name);
            TableText($"{entry.PoseLabel} ({entry.PoseParam})");
            TableText(entry.BaseId.ToString());
            TableText($"{entry.PositionX:0.00}, {entry.PositionY:0.00}, {entry.PositionZ:0.00}");
            TableText(entry.Enabled ? "Yes" : "No");
            TableText(entry.TerritoryType == currentTerritory ? "Current area" : "Other area");
        }

        ImGui.EndTable();
    }

    private void DrawInlineScannerToggles()
    {
        using var node = ImRaii.TreeNode("Debug scanner filters");
        if (!node.Success)
            return;

        ImGui.TextWrapped("Default scan shows safe EventNpc pose candidates. Enable these only when discovering object-table behaviour.");

        var showAll = plugin.Configuration.ShowAllNonPlayerObjects;
        if (ImGui.Checkbox("Show all non-player objects", ref showAll))
        {
            plugin.Configuration.ShowAllNonPlayerObjects = showAll;
            plugin.Configuration.Save();
        }

        var includeHousingEventObjects = plugin.Configuration.IncludeHousingEventObjects;
        if (ImGui.Checkbox("Include HousingEventObject rows", ref includeHousingEventObjects))
        {
            plugin.Configuration.IncludeHousingEventObjects = includeHousingEventObjects;
            plugin.Configuration.Save();
        }

        var includeEventObjects = plugin.Configuration.IncludeEventObjects;
        if (ImGui.Checkbox("Include EventObj rows / exits", ref includeEventObjects))
        {
            plugin.Configuration.IncludeEventObjects = includeEventObjects;
            plugin.Configuration.Save();
        }

        var includeRetainers = plugin.Configuration.IncludeRetainers;
        if (ImGui.Checkbox("Include Retainer candidates", ref includeRetainers))
        {
            plugin.Configuration.IncludeRetainers = includeRetainers;
            plugin.Configuration.Save();
        }

        var includeBattleNpcs = plugin.Configuration.IncludeBattleNpcs;
        if (ImGui.Checkbox("Include BattleNpc candidates", ref includeBattleNpcs))
        {
            plugin.Configuration.IncludeBattleNpcs = includeBattleNpcs;
            plugin.Configuration.Save();
        }
    }

    private void DrawPoseLab()
    {
        using var node = ImRaii.TreeNode("Pose target + custom param lab", ImGuiTreeNodeFlags.DefaultOpen);
        if (!node.Success)
            return;

        ImGui.TextWrapped("Click Set Target beside a safe EventNpc row, apply or choose a pose, then save it. Saved entries are matched by area + NPC name + BaseId + approximate position, not by volatile ObjectIndex.");
        ImGui.TextUnformatted($"Lab target: {labTargetName} | current idx {labObjectIndex} | BaseId {labTargetBaseId} | Pos {labTargetPosition}");
        ImGui.TextUnformatted($"Selected pose to save: {labLastPoseLabel} ({labLastPoseParam})");

        var labIndexAsInt = (int)labObjectIndex;
        if (ImGui.InputInt("Object index", ref labIndexAsInt))
        {
            labIndexAsInt = Math.Clamp(labIndexAsInt, 0, (int)ushort.MaxValue);
            labObjectIndex = (ushort)labIndexAsInt;
            labTargetName = "<manual index>";
            labTargetBaseId = 0;
            labTargetPosition = "-";
        }

        ImGui.SameLine();

        if (ImGui.Button("Use first pose candidate"))
        {
            foreach (var result in plugin.ScanResults)
            {
                if (!result.CanPose)
                    continue;

                SetLabTarget(result);
                break;
            }
        }

        if (ImGui.InputInt("Param ID", ref labParam))
            labParam = Math.Clamp(labParam, 0, 255);

        if (ImGui.Button("Apply custom Param ID"))
        {
            labLastPoseParam = (byte)labParam;
            labLastPoseLabel = $"Custom Pos {labLastPoseParam}";
            plugin.ApplyInPositionLoop(labObjectIndex, labLastPoseParam);
        }

        ImGui.SameLine();

        if (ImGui.Button("Restore target"))
            plugin.RestoreActorModeByIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Normal fallback"))
            plugin.ApplyNormalMode(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Save selected pose for target"))
            plugin.SavePoseForObjectIndex(labObjectIndex, labLastPoseLabel, labLastPoseParam);

        ImGui.SameLine();

        if (ImGui.Button("Apply saved to target"))
            plugin.ApplySavedPoseForObjectIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Clear saved for target"))
            plugin.ClearSavedPoseForObjectIndex(labObjectIndex);

        ImGui.Spacing();
        ImGui.TextUnformatted("Core posing:");

        DrawPoseButton("Sit", "sit");
        ImGui.SameLine();
        DrawPoseButton("Chair Sit", "bench");
        ImGui.SameLine();
        DrawPoseButton("Doze", "doze");
        ImGui.SameLine();
        DrawPoseButton("Lean", "lean");

        ImGui.TextUnformatted("Useful expressions / gestures:");

        DrawPoseButton("Confirm", "confirm");
        ImGui.SameLine();
        DrawPoseButton("Scheme", "scheme");
        ImGui.SameLine();
        DrawPoseButton("Reprimand", "reprimand");
        ImGui.SameLine();
        DrawPoseButton("Sweat", "sweat");
        ImGui.SameLine();
        DrawPoseButton("Shiver", "shiver");

        using var debugNode = ImRaii.TreeNode("Known fun/test emotes");
        if (debugNode.Success)
        {
            DrawPoseButton("Stepdance", "stepdance");
            ImGui.SameLine();
            DrawPoseButton("Harvestdance", "harvestdance");
            ImGui.SameLine();
            DrawPoseButton("Ball Dance", "balldance");
            ImGui.SameLine();
            DrawPoseButton("Manderville", "manderville");
            ImGui.SameLine();
            DrawPoseButton("Thavnairian", "thavdance");
        }
    }

    private void DrawPoseButton(string label, string poseName)
    {
        if (ImGui.Button(label))
        {
            if (plugin.TryGetNamedPoseParam(poseName, out var param, out var poseLabel))
            {
                labLastPoseParam = param;
                labLastPoseLabel = poseLabel;
            }

            plugin.ApplyNamedPose(labObjectIndex, poseName, "pose lab");
        }
    }

    private void SetLabTarget(NpcScanResult result)
    {
        labObjectIndex = result.ObjectIndex;
        labTargetName = string.IsNullOrWhiteSpace(result.Name) ? "<no name>" : result.Name;
        labTargetBaseId = result.BaseId;
        labTargetPosition = result.PositionText;
    }

    private void DrawResultsTable(IReadOnlyList<NpcScanResult> results)
    {
        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollX |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseScanTable", 16, tableFlags))
            return;

        ImGui.TableSetupColumn("Idx");
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Kind");
        ImGui.TableSetupColumn("Sub");
        ImGui.TableSetupColumn("BaseId");
        ImGui.TableSetupColumn("EntityId");
        ImGui.TableSetupColumn("GameObjectId");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Position");
        ImGui.TableSetupColumn("Rot");
        ImGui.TableSetupColumn("Address");
        ImGui.TableSetupColumn("Saved auto");
        ImGui.TableSetupColumn("Snapshot");
        ImGui.TableSetupColumn("Plugin pose");
        ImGui.TableSetupColumn("Actions");
        ImGui.TableSetupColumn("Note");
        ImGui.TableHeadersRow();

        foreach (var result in results)
        {
            ImGui.TableNextRow();

            TableText(result.ObjectIndex.ToString());
            TableText(string.IsNullOrWhiteSpace(result.Name) ? "<no name>" : result.Name);
            TableText(result.Kind.ToString());
            TableText(result.SubKind.ToString());
            TableText(result.BaseId.ToString());
            TableText(result.EntityIdText);
            TableText(result.GameObjectIdText);
            TableText(result.IsTargetable ? "Yes" : "No");
            TableText(result.PositionText);
            TableText(result.RotationText);
            TableText(result.AddressText);
            TableText(result.SavedPoseText);
            TableText(result.HasSavedModeSnapshot ? "Yes" : "No");
            TableText(result.PluginPoseText);

            ImGui.TableNextColumn();
            if (result.CanPose)
            {
                if (ImGui.SmallButton($"Set Target##target{result.ObjectIndex}"))
                    SetLabTarget(result);

                ImGui.SameLine();

                if (ImGui.SmallButton($"Apply Saved##applysaved{result.ObjectIndex}"))
                    plugin.ApplySavedPoseForObjectIndex(result.ObjectIndex);

                ImGui.SameLine();

                if (ImGui.SmallButton($"Clear Saved##clearsaved{result.ObjectIndex}"))
                    plugin.ClearSavedPoseForObjectIndex(result.ObjectIndex);

                ImGui.SameLine();

                if (ImGui.SmallButton($"Sit##sit{result.ObjectIndex}"))
                    plugin.ApplyNamedPose(result.ObjectIndex, "sit", "UI button");

                ImGui.SameLine();

                if (ImGui.SmallButton($"Lean##lean{result.ObjectIndex}"))
                    plugin.ApplyNamedPose(result.ObjectIndex, "lean", "UI button");

                ImGui.SameLine();

                if (ImGui.SmallButton($"Doze##doze{result.ObjectIndex}"))
                    plugin.ApplyNamedPose(result.ObjectIndex, "doze", "UI button");

                ImGui.SameLine();

                if (ImGui.SmallButton($"Restore##restore{result.ObjectIndex}"))
                    plugin.RestoreActorModeByIndex(result.ObjectIndex);
            }
            else if (result.Kind == ObjectKind.EventNpc)
            {
                ImGui.TextDisabled("Blocked");
            }
            else
            {
                ImGui.TextDisabled("-");
            }

            TableText(result.Note);
        }

        ImGui.EndTable();
    }

    private static void TableText(string text)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(text);
    }
}
