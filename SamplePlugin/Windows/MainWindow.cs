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
    private float labOffsetY = 0.0f;
    private byte labLastPoseParam = 1;
    private string labLastPoseLabel = "Sit / ground sit";
    private string labTargetName = "<none>";
    private uint labTargetBaseId;
    private string labTargetPosition = "-";

    public MainWindow(Plugin plugin)
        : base("Housing NPC Pose v0.3.7###HousingNpcPoseMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawHeader();
        DrawToolbar();

        ImGui.Spacing();
        DrawQuickStartGuide();

        ImGui.Spacing();
        DrawAutomationControls();

        ImGui.Spacing();
        DrawSceneTable(plugin.ScanResults);

        ImGui.Spacing();
        DrawSelectedTargetEditor();

        ImGui.Spacing();
        DrawAdvancedTools();
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted("Housing NPC Pose v0.3.7");
        ImGui.TextWrapped("Client-side housing scene editor for already-spawned housing NPCs. Saves local pose params, visual Y offsets, and optional nameplate hiding. No packets, spawning, server movement, hooks, or cross-client sync.");
        ImGui.Spacing();

        ImGui.TextUnformatted($"Current area: {plugin.GetTerritoryLabel()}");
        ImGui.SameLine();
        if (plugin.LastScanTime is { } lastScan)
            ImGui.TextUnformatted($"| Last scan: {lastScan:HH:mm:ss}");
        else
            ImGui.TextUnformatted("| Last scan: not scanned yet");
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Scan"))
            plugin.ScanObjects();

        ImGui.SameLine();

        if (ImGui.Button("Apply saved now"))
            plugin.ApplySavedPosesNow();

        ImGui.SameLine();

        if (ImGui.Button("Restore all"))
            plugin.RestoreAllActorModes();

        ImGui.SameLine();

        if (ImGui.Button("Clear scan"))
            plugin.ClearScan();

        ImGui.SameLine();

        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();
    }

    private void DrawQuickStartGuide()
    {
        using var node = ImRaii.TreeNode("Quick start / intended workflow");
        if (!node.Success)
            return;

        ImGui.TextWrapped("Housing NPC Pose is intended as a local housing scene tool. It works best with the normal housing system plus appearance tools such as Glamourer/Penumbra.");
        ImGui.Spacing();
        ImGui.BulletText("Place a housing NPC, vendor, mender, mannequin, or other supported housing actor.");
        ImGui.BulletText("Use Glamourer/Penumbra if you want to change how that NPC looks locally.");
        ImGui.BulletText("Use the normal housing tools to place the NPC near furniture, a bath, bed, counter, or scene area.");
        ImGui.BulletText("Scan here, click Edit on the NPC, choose a pose, then adjust visual Y offset if needed.");
        ImGui.BulletText("Use Save selected pose + Y so the pose and height are restored when you return.");
        ImGui.BulletText("Enable nameplate hiding if the label covers a visually offset NPC.");
        ImGui.Spacing();
        ImGui.TextWrapped("All effects are local-client only. Visitors will not see these posed scenes unless they use their own local setup.");
    }

    private void DrawAutomationControls()
    {
        var currentTerritory = Plugin.ClientState.TerritoryType;
        var totalSaved = plugin.Configuration.SavedPoses.Count;
        var currentAreaSaved = plugin.Configuration.SavedPoses.Count(entry => entry.TerritoryType == currentTerritory);

        ImGui.TextUnformatted($"Auto apply: {(plugin.Configuration.AutoApplySavedPoses ? "ON" : "OFF")} | Nameplates: {(plugin.Configuration.HideNameplatesForPosedNpcs ? "hidden for posed/saved" : "normal")} | Saved here: {currentAreaSaved} | Total saved: {totalSaved}");

        var autoApply = plugin.Configuration.AutoApplySavedPoses;
        if (ImGui.Checkbox("Auto-apply saved poses", ref autoApply))
        {
            plugin.Configuration.AutoApplySavedPoses = autoApply;
            plugin.Configuration.Save();
            if (autoApply)
                plugin.ScheduleAutoApply("UI toggle");
        }

        ImGui.SameLine();

        var hideNameplates = plugin.Configuration.HideNameplatesForPosedNpcs;
        if (ImGui.Checkbox("Hide nameplates for posed/saved NPCs", ref hideNameplates))
            plugin.SetHideNameplatesForPosedNpcs(hideNameplates);

        ImGui.SameLine();

        if (ImGui.SmallButton("Clear saved in this area"))
            plugin.ClearAllSavedPosesForCurrentTerritory();
    }

    private void DrawSceneTable(IReadOnlyList<NpcScanResult> results)
    {
        ImGui.TextUnformatted($"Housing scene NPCs: {results.Count}");

        using var child = ImRaii.Child("HousingNpcPoseSceneTableChild", new Vector2(0, 210), true);
        if (!child.Success)
            return;

        if (results.Count == 0)
        {
            ImGui.TextWrapped("No scan results yet. Enter your house/apartment/private chamber, then click Scan or run /hnpcpose scan.");
            return;
        }

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseSceneTable", 8, tableFlags))
            return;

        ImGui.TableSetupColumn("Idx");
        ImGui.TableSetupColumn("NPC");
        ImGui.TableSetupColumn("Status");
        ImGui.TableSetupColumn("Saved");
        ImGui.TableSetupColumn("Current");
        ImGui.TableSetupColumn("Position");
        ImGui.TableSetupColumn("Actions");
        ImGui.TableSetupColumn("Note");
        ImGui.TableHeadersRow();

        foreach (var result in results)
        {
            ImGui.TableNextRow();

            TableText(result.ObjectIndex.ToString());
            TableText(string.IsNullOrWhiteSpace(result.Name) ? "<no name>" : result.Name);
            TableText(GetStatusText(result));
            TableText(result.SavedPoseText);
            TableText(result.PluginPoseText);
            TableText(result.PositionText);

            ImGui.TableNextColumn();
            DrawRowActions(result);

            TableText(result.Note);
        }

        ImGui.EndTable();
    }

    private void DrawRowActions(NpcScanResult result)
    {
        if (!result.CanPose)
        {
            ImGui.TextDisabled(result.Kind == ObjectKind.EventNpc ? "Blocked" : "-");
            return;
        }

        if (ImGui.SmallButton($"Edit##edit{result.ObjectIndex}"))
            SetLabTarget(result);

        ImGui.SameLine();

        if (ImGui.SmallButton($"Apply##apply{result.ObjectIndex}"))
            plugin.ApplySavedPoseForObjectIndex(result.ObjectIndex);

        ImGui.SameLine();

        if (ImGui.SmallButton($"Clear##clear{result.ObjectIndex}"))
            plugin.ClearSavedPoseForObjectIndex(result.ObjectIndex);

        ImGui.SameLine();

        if (ImGui.SmallButton($"Restore##restore{result.ObjectIndex}"))
            plugin.RestoreActorModeByIndex(result.ObjectIndex);
    }

    private static string GetStatusText(NpcScanResult result)
    {
        if (!result.CanPose)
            return result.Kind == ObjectKind.EventNpc ? "Blocked" : "Debug";

        if (result.SavedPoseText != "-" && result.PluginPoseText != "-")
            return "Saved + applied";

        if (result.SavedPoseText != "-")
            return "Saved";

        if (result.PluginPoseText != "-")
            return "Applied";

        return "Pose target";
    }

    private void DrawSelectedTargetEditor()
    {
        ImGui.TextUnformatted("Selected NPC editor");
        ImGui.Separator();

        if (!HasSelectedTarget())
        {
            ImGui.TextWrapped("Select a safe EventNpc row with Edit, or use the first available pose target.");

            if (ImGui.Button("Use first pose target"))
                SelectFirstPoseCandidate();

            return;
        }

        ImGui.TextUnformatted($"Editing: {labTargetName} | current idx {labObjectIndex} | BaseId {labTargetBaseId} | Pos {labTargetPosition}");
        ImGui.TextUnformatted($"Selected pose: {labLastPoseLabel} ({labLastPoseParam}) | Y offset {labOffsetY:+0.00;-0.00;0.00}");

        ImGui.Spacing();
        ImGui.TextUnformatted("Core poses");
        DrawPoseButtonsForCategory(PoseCategory.Core);
        ImGui.SameLine();
        if (ImGui.Button("Restore target"))
            plugin.RestoreActorModeByIndex(labObjectIndex);

        ImGui.TextUnformatted("Useful gestures");
        DrawPoseButtonsForCategory(PoseCategory.Gesture);

        ImGui.Spacing();
        DrawYOffsetControls();

        ImGui.Spacing();
        if (ImGui.Button("Save selected pose + Y"))
            plugin.SavePoseForObjectIndex(labObjectIndex, labLastPoseLabel, labLastPoseParam, labOffsetY);

        ImGui.SameLine();

        if (ImGui.Button("Apply saved to target"))
            plugin.ApplySavedPoseForObjectIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Clear saved target"))
            plugin.ClearSavedPoseForObjectIndex(labObjectIndex);
    }

    private void DrawYOffsetControls()
    {
        ImGui.TextUnformatted("Visual Y offset");
        ImGui.TextWrapped("Use this to align sitting/lying poses with furniture. It is a local draw offset only and saves with the pose.");

        if (ImGui.InputFloat("Y offset", ref labOffsetY, 0.01f, 0.25f, "%+.2f"))
            labOffsetY = Math.Clamp(labOffsetY, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        if (ImGui.SmallButton("-0.25"))
            labOffsetY = Math.Clamp(labOffsetY - 0.25f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("-0.05"))
            labOffsetY = Math.Clamp(labOffsetY - 0.05f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("-0.01"))
            labOffsetY = Math.Clamp(labOffsetY - 0.01f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("+0.01"))
            labOffsetY = Math.Clamp(labOffsetY + 0.01f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("+0.05"))
            labOffsetY = Math.Clamp(labOffsetY + 0.05f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("+0.25"))
            labOffsetY = Math.Clamp(labOffsetY + 0.25f, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset);

        ImGui.SameLine();

        if (ImGui.SmallButton("Apply Y"))
            plugin.ApplyYOffsetByIndex(labObjectIndex, labOffsetY, "selected editor");

        ImGui.SameLine();

        if (ImGui.SmallButton("Apply + save Y"))
        {
            if (plugin.ApplyYOffsetByIndex(labObjectIndex, labOffsetY, "selected editor"))
                plugin.SaveYOffsetForObjectIndex(labObjectIndex, labOffsetY);
        }

        ImGui.SameLine();

        if (ImGui.SmallButton("Save Y only"))
            plugin.SaveYOffsetForObjectIndex(labObjectIndex, labOffsetY);

        ImGui.SameLine();

        if (ImGui.SmallButton("Reset + save Y"))
        {
            labOffsetY = 0.0f;
            if (plugin.ApplyYOffsetByIndex(labObjectIndex, 0.0f, "selected editor"))
                plugin.SaveYOffsetForObjectIndex(labObjectIndex, 0.0f);
        }
    }

    private void DrawAdvancedTools()
    {
        using var node = ImRaii.TreeNode("Advanced / discovery tools");
        if (!node.Success)
            return;

        DrawCustomParamLab();
        ImGui.Spacing();
        DrawPoseCatalogueSummary();
        ImGui.Spacing();
        DrawSavedPoseSummary();
        ImGui.Spacing();
        DrawInlineScannerToggles();
        ImGui.Spacing();
        DrawRawResultsTable(plugin.ScanResults);
    }

    private void DrawCustomParamLab()
    {
        ImGui.TextUnformatted("Custom pose param lab");

        var labIndexAsInt = (int)labObjectIndex;
        if (ImGui.InputInt("Object index", ref labIndexAsInt))
        {
            labIndexAsInt = Math.Clamp(labIndexAsInt, 0, (int)ushort.MaxValue);
            labObjectIndex = (ushort)labIndexAsInt;
            labTargetName = "<manual index>";
            labTargetBaseId = 0;
            labTargetPosition = "-";
        }

        if (ImGui.InputInt("Param ID", ref labParam))
            labParam = Math.Clamp(labParam, 0, 255);

        if (ImGui.Button("Apply custom Param ID"))
        {
            labLastPoseParam = (byte)labParam;
            labLastPoseLabel = PoseCatalogue.GetDisplayName(labLastPoseParam);
            plugin.ApplyInPositionLoop(labObjectIndex, labLastPoseParam);
        }

        ImGui.SameLine();

        if (ImGui.Button("Save custom Param + Y"))
        {
            labLastPoseParam = (byte)labParam;
            labLastPoseLabel = PoseCatalogue.GetDisplayName(labLastPoseParam);
            plugin.SavePoseForObjectIndex(labObjectIndex, labLastPoseLabel, labLastPoseParam, labOffsetY);
        }

        ImGui.SameLine();

        if (ImGui.Button("Normal fallback"))
            plugin.ApplyNormalMode(labObjectIndex);

        using var debugNode = ImRaii.TreeNode("Known dance / experimental catalogue entries");
        if (debugNode.Success)
        {
            DrawPoseButtonsForCategory(PoseCategory.Dance);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Experimental);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Unknown);
        }
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
            ImGui.TextWrapped("No saved pose assignments yet. Select a target, apply a pose/Y offset, then save it.");
            return;
        }

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseSavedPoseTable", 8, tableFlags))
            return;

        ImGui.TableSetupColumn("Area");
        ImGui.TableSetupColumn("NPC");
        ImGui.TableSetupColumn("Pose");
        ImGui.TableSetupColumn("BaseId");
        ImGui.TableSetupColumn("Position");
        ImGui.TableSetupColumn("Y Offset");
        ImGui.TableSetupColumn("Enabled");
        ImGui.TableSetupColumn("Scope");
        ImGui.TableHeadersRow();

        foreach (var entry in entries)
        {
            ImGui.TableNextRow();
            TableText(string.IsNullOrWhiteSpace(entry.TerritoryLabel) ? entry.TerritoryType.ToString() : entry.TerritoryLabel);
            TableText(string.IsNullOrWhiteSpace(entry.Name) ? "<no name>" : entry.Name);
            TableText($"{PoseCatalogue.GetSavedDisplayName(entry.PoseParam, entry.PoseLabel)} ({entry.PoseParam})");
            TableText(entry.BaseId.ToString());
            TableText($"{entry.PositionX:0.00}, {entry.PositionY:0.00}, {entry.PositionZ:0.00}");
            TableText(entry.OffsetY.ToString("+0.00;-0.00;0.00"));
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

    private void DrawRawResultsTable(IReadOnlyList<NpcScanResult> results)
    {
        using var node = ImRaii.TreeNode("Raw scan details");
        if (!node.Success)
            return;

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollX |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseRawScanTable", 16, tableFlags))
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
        ImGui.TableSetupColumn("Can pose");
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
            TableText(result.CanPose ? "Yes" : "No");
            TableText(result.Note);
        }

        ImGui.EndTable();
    }

    private void DrawPoseButtonsForCategory(PoseCategory category)
    {
        var first = true;
        foreach (var definition in PoseCatalogue.ByCategory(category))
        {
            if (!first)
                ImGui.SameLine();

            DrawPoseButton(definition);
            first = false;
        }
    }

    private void DrawPoseButton(PoseDefinition definition)
    {
        if (ImGui.Button(definition.DisplayName))
        {
            labLastPoseParam = definition.Param;
            labLastPoseLabel = definition.DisplayName;
            plugin.ApplyNamedPose(labObjectIndex, definition.Key, "selected editor");
        }
    }

    private void DrawPoseButton(string label, string poseName)
    {
        if (PoseCatalogue.TryGetByAlias(poseName, out var definition))
        {
            DrawPoseButton(definition);
            return;
        }

        if (ImGui.Button(label))
        {
            if (plugin.TryGetNamedPoseParam(poseName, out var param, out var poseLabel))
            {
                labLastPoseParam = param;
                labLastPoseLabel = poseLabel;
            }

            plugin.ApplyNamedPose(labObjectIndex, poseName, "selected editor");
        }
    }

    private void DrawPoseCatalogueSummary()
    {
        using var node = ImRaii.TreeNode("Pose catalogue crosswalk");
        if (!node.Success)
            return;

        ImGui.TextWrapped("This table bridges HousingNpcPose's small local pose params with friendly names and optional CSV/ActionTimeline metadata. The param is still the source of truth for this plugin; CSV timeline IDs are reference notes only.");

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseCatalogueTable", 7, tableFlags))
            return;

        ImGui.TableSetupColumn("Param");
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Category");
        ImGui.TableSetupColumn("Safety");
        ImGui.TableSetupColumn("CSV / Timeline");
        ImGui.TableSetupColumn("Key");
        ImGui.TableSetupColumn("Notes");
        ImGui.TableHeadersRow();

        foreach (var definition in PoseCatalogue.All.OrderBy(definition => definition.Category).ThenBy(definition => definition.Param))
        {
            ImGui.TableNextRow();
            TableText(definition.Param.ToString());
            TableText(definition.DisplayName);
            TableText(definition.Category.ToString());
            TableText(definition.Safety.ToString());
            TableText(definition.ActionTimelineId is { } timelineId
                ? $"{definition.CsvName ?? definition.DisplayName} / {timelineId}"
                : definition.CsvName ?? "-");
            TableText(definition.Key);
            TableText(definition.Notes ?? "-");
        }

        ImGui.EndTable();
    }

    private bool HasSelectedTarget()
    {
        return labTargetName != "<none>";
    }

    private void SelectFirstPoseCandidate()
    {
        foreach (var result in plugin.ScanResults)
        {
            if (!result.CanPose)
                continue;

            SetLabTarget(result);
            return;
        }
    }

    private void SetLabTarget(NpcScanResult result)
    {
        labObjectIndex = result.ObjectIndex;
        labTargetName = string.IsNullOrWhiteSpace(result.Name) ? "<no name>" : result.Name;
        labTargetBaseId = result.BaseId;
        labTargetPosition = result.PositionText;

        var saved = plugin.FindSavedPoseForScanResult(result);
        if (saved != null)
        {
            labLastPoseParam = saved.PoseParam;
            labLastPoseLabel = saved.PoseLabel;
            labOffsetY = saved.OffsetY;
        }
        else
        {
            labLastPoseParam = 1;
            labLastPoseLabel = "Sit / ground sit";
            labOffsetY = 0.0f;
        }
    }

    private static void TableText(string text)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(text);
    }
}
