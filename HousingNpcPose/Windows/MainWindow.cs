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
    private int discoveryModeIndex;
    private int discoveryConfidenceIndex = 2;
    private string observationName = string.Empty;
    private string observationCategory = "Unknown";
    private string observationNotes = string.Empty;
    private int poseCategoryFilterIndex;
    private string poseSearch = string.Empty;
    private string selectedSceneId = string.Empty;
    private string sceneNameInput = "Scene 1";

    private static readonly string[] DiscoveryModes = { "InPositionLoop", "EmoteLoop" };
    private static readonly string[] DiscoveryConfidences = { "Confirmed", "Likely", "Uncertain", "Experimental", "ReferenceOnly" };
    private static readonly string[] PoseCategoryFilterNames =
    {
        "All",
        "Core",
        "Gesture",
        "Dance",
        "Exercise",
        "Performance",
        "Prop / food / drink",
        "Experimental",
        "Unknown",
    };

    private static readonly PoseCategory?[] PoseCategoryFilters =
    {
        null,
        PoseCategory.Core,
        PoseCategory.Gesture,
        PoseCategory.Dance,
        PoseCategory.Exercise,
        PoseCategory.Performance,
        PoseCategory.Prop,
        PoseCategory.Experimental,
        PoseCategory.Unknown,
    };

    private static readonly string[] QuickPoseKeys =
    {
        "chair",
        "sit",
        "doze",
        "lean",
        "study",
        "savortea",
        "guard",
        "slump",
        "scheme",
        "reprimand",
    };

    public MainWindow(Plugin plugin)
        : base("Housing NPC Pose v0.4.1###HousingNpcPoseMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(980, 640),
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
        DrawAutomationControls();

        ImGui.Spacing();
        DrawScenePresets();

        ImGui.Spacing();
        DrawMainSceneEditor();

        ImGui.Spacing();
        DrawRoomOverview();

        ImGui.Spacing();
        DrawQuickStartGuide();

        ImGui.Spacing();
        DrawAdvancedTools();
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted("Housing NPC Pose v0.4.1");
        ImGui.TextWrapped("Local housing scene editor for already-spawned housing NPCs. Pick an actor, browse a pose, adjust visual Y, save, then capture/load named room scenes. Client-side only: no packets, spawning, server movement, hooks, or cross-client sync.");
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
        if (ImGui.Button("Scan room"))
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
        ImGui.BulletText("Scan here, choose an actor from the dropdown, browse/search for a pose, then adjust visual Y offset if needed.");
        ImGui.BulletText("Use Apply + save pose/Y so the pose and height are restored when you return.");
        ImGui.BulletText("Enable nameplate hiding if the label covers a visually offset NPC.");
        ImGui.Spacing();
        ImGui.TextWrapped("All effects are local-client only. Visitors will not see these posed scenes unless they use their own local setup.");
    }

    private void DrawAutomationControls()
    {
        var currentTerritory = Plugin.ClientState.TerritoryType;
        var totalSaved = plugin.Configuration.SavedPoses.Count;
        var currentAreaSaved = plugin.Configuration.SavedPoses.Count(entry => entry.TerritoryType == currentTerritory);
        var currentAreaScenes = plugin.GetScenePresetsForCurrentTerritory().Count;

        ImGui.TextUnformatted($"Auto apply: {(plugin.Configuration.AutoApplySavedPoses ? "ON" : "OFF")} | Nameplates: {(plugin.Configuration.HideNameplatesForPosedNpcs ? "hidden for posed/saved" : "normal")} | Saved here: {currentAreaSaved} | Scenes here: {currentAreaScenes} | Total saved: {totalSaved}");

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


    private void DrawScenePresets()
    {
        ImGui.TextUnformatted("Scene presets");
        ImGui.Separator();
        ImGui.TextWrapped("A scene is a named snapshot of all saved actor poses/Y offsets for this room. Load replaces this area's current saved assignments with the scene contents, then applies them locally.");

        var scenes = plugin.GetScenePresetsForCurrentTerritory().ToArray();
        if (string.IsNullOrWhiteSpace(sceneNameInput))
            sceneNameInput = $"Scene {scenes.Length + 1}";

        var selectedScene = plugin.GetScenePresetById(selectedSceneId);
        if (selectedScene == null && scenes.Length > 0)
        {
            selectedScene = scenes[0];
            selectedSceneId = selectedScene.Id;
            if (string.IsNullOrWhiteSpace(sceneNameInput) || sceneNameInput.StartsWith("Scene ", StringComparison.OrdinalIgnoreCase))
                sceneNameInput = selectedScene.Name;
        }

        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Scene name", ref sceneNameInput, 80);

        ImGui.SameLine();
        if (ImGui.Button("Save current as new scene"))
        {
            var scene = plugin.SaveCurrentSavedPosesAsScene(sceneNameInput);
            if (scene != null)
            {
                selectedSceneId = scene.Id;
                sceneNameInput = scene.Name;
                selectedScene = scene;
            }
        }

        ImGui.SetNextItemWidth(420);
        var preview = selectedScene == null ? "No scene selected" : selectedScene.DisplayText;
        if (ImGui.BeginCombo("Saved scenes", preview))
        {
            if (scenes.Length == 0)
            {
                ImGui.TextDisabled("No scenes saved for this area yet.");
            }
            else
            {
                foreach (var scene in scenes)
                {
                    var selected = string.Equals(scene.Id, selectedSceneId, StringComparison.OrdinalIgnoreCase);
                    if (ImGui.Selectable(scene.DisplayText, selected))
                    {
                        selectedSceneId = scene.Id;
                        sceneNameInput = scene.Name;
                        selectedScene = scene;
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (selectedScene == null)
        {
            ImGui.TextWrapped("Save some actor poses first, then use 'Save current as new scene' to capture this room state.");
            return;
        }

        ImGui.TextUnformatted($"Selected scene: {selectedScene.Name} | {selectedScene.Poses.Count} actor(s) | Updated {FormatSceneDate(selectedScene.UpdatedAtUtc)}");

        if (ImGui.Button("Load scene"))
            plugin.LoadScenePreset(selectedScene.Id);

        ImGui.SameLine();

        if (ImGui.Button("Overwrite from current saved"))
            plugin.OverwriteScenePresetFromCurrentSavedPoses(selectedScene.Id, sceneNameInput);

        ImGui.SameLine();

        if (ImGui.Button("Rename"))
            plugin.RenameScenePreset(selectedScene.Id, sceneNameInput);

        ImGui.SameLine();

        if (ImGui.Button("Delete scene"))
        {
            if (plugin.DeleteScenePreset(selectedScene.Id))
                selectedSceneId = string.Empty;
        }

        DrawScenePresetContents(selectedScene);
    }

    private void DrawScenePresetContents(ScenePresetEntry scene)
    {
        using var node = ImRaii.TreeNode($"Scene contents##{scene.Id}");
        if (!node.Success)
            return;

        if (scene.Poses.Count == 0)
        {
            ImGui.TextDisabled("This scene has no saved actor poses.");
            return;
        }

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable($"HousingNpcPoseScenePresetTable{scene.Id}", 6, tableFlags))
            return;

        ImGui.TableSetupColumn("NPC");
        ImGui.TableSetupColumn("Pose");
        ImGui.TableSetupColumn("Y");
        ImGui.TableSetupColumn("Position");
        ImGui.TableSetupColumn("BaseId");
        ImGui.TableSetupColumn("Enabled");
        ImGui.TableHeadersRow();

        foreach (var pose in scene.Poses
            .OrderBy(pose => pose.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pose => pose.PositionX)
            .ThenBy(pose => pose.PositionZ))
        {
            ImGui.TableNextRow();
            TableText(string.IsNullOrWhiteSpace(pose.Name) ? "<no name>" : pose.Name);
            TableText($"{PoseCatalogue.GetSavedDisplayName(pose.PoseParam, pose.PoseLabel)} ({pose.PoseParam})");
            TableText(pose.OffsetY.ToString("+0.00;-0.00;0.00"));
            TableText($"{pose.PositionX:0.00}, {pose.PositionY:0.00}, {pose.PositionZ:0.00}");
            TableText(pose.BaseId.ToString());
            TableText(pose.Enabled ? "Yes" : "No");
        }

        ImGui.EndTable();
    }

    private static string FormatSceneDate(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private void DrawMainSceneEditor()
    {
        ImGui.TextUnformatted("Scene editor");
        ImGui.Separator();

        DrawActorSelector();

        ImGui.Spacing();

        if (!HasSelectedTarget())
        {
            ImGui.TextWrapped("Start by scanning the room, then choose a safe housing NPC from the Actor dropdown. Raw object-index entry is still available under Advanced / discovery tools.");
            if (ImGui.Button("Use first pose target"))
                SelectFirstPoseCandidate();
            return;
        }

        DrawSelectedTargetCard();
        ImGui.Spacing();
        DrawPoseBrowser();
        ImGui.Spacing();
        DrawYOffsetControls();
        ImGui.Spacing();
        DrawSaveApplyStrip();
    }

    private void DrawActorSelector()
    {
        var poseTargets = plugin.ScanResults.Where(result => result.CanPose).ToArray();
        var selectedResult = CurrentSelectedScanResult();
        var preview = selectedResult == null
            ? HasSelectedTarget() ? $"{labTargetName} [{labObjectIndex}]" : "Choose a scanned housing NPC..."
            : BuildActorComboLabel(selectedResult);

        ImGui.SetNextItemWidth(520);
        if (ImGui.BeginCombo("Actor", preview))
        {
            if (poseTargets.Length == 0)
            {
                ImGui.TextDisabled("No safe pose targets in the current scan.");
            }
            else
            {
                foreach (var result in poseTargets)
                {
                    var selected = result.ObjectIndex == labObjectIndex;
                    if (ImGui.Selectable(BuildActorComboLabel(result), selected))
                        SetLabTarget(result);

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("Scan"))
            plugin.ScanObjects();

        ImGui.SameLine();
        if (ImGui.Button("First target"))
            SelectFirstPoseCandidate();
    }

    private static string BuildActorComboLabel(NpcScanResult result)
    {
        var name = string.IsNullOrWhiteSpace(result.Name) ? "<no name>" : result.Name;
        var saved = result.SavedPoseText == "-" ? "unsaved" : $"saved: {result.SavedPoseText}";
        return $"{name} [{result.ObjectIndex}] — {saved}";
    }

    private void DrawSelectedTargetCard()
    {
        var selectedResult = CurrentSelectedScanResult();
        var status = selectedResult == null ? "Manual / not in current scan" : GetStatusText(selectedResult);
        var saved = selectedResult?.SavedPoseText ?? "-";
        var applied = selectedResult?.PluginPoseText ?? "-";

        ImGui.TextUnformatted($"Selected actor: {labTargetName} [{labObjectIndex}]");
        ImGui.TextUnformatted($"Status: {status} | Saved: {saved} | Applied this session: {applied}");
        ImGui.TextUnformatted($"BaseId {labTargetBaseId} | Position {labTargetPosition}");
        ImGui.TextUnformatted($"Pose selection: {labLastPoseLabel} ({labLastPoseParam}) | Y offset {labOffsetY:+0.00;-0.00;0.00}");
    }

    private void DrawPoseBrowser()
    {
        ImGui.TextUnformatted("Pose browser");
        ImGui.TextWrapped("Search or filter the live-tested catalogue, then apply a pose to the selected actor. The full table keeps the CSV crosswalk in Advanced; this view is for normal scene work.");

        ImGui.TextUnformatted("Quick scene poses:");
        var first = true;
        foreach (var key in QuickPoseKeys)
        {
            if (!PoseCatalogue.TryGetByAlias(key, out var definition))
                continue;

            if (!first)
                ImGui.SameLine();

            if (ImGui.SmallButton($"{definition.DisplayName}##quick{definition.Param}"))
                ApplyPoseDefinition(definition, saveAfterApply: false);

            first = false;
        }

        ImGui.Spacing();

        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Search", ref poseSearch, 120);
        ImGui.SameLine();
        DrawPoseCategoryFilterCombo();

        var definitions = PoseCatalogue.All
            .Where(PoseBrowserMatches)
            .OrderBy(definition => definition.Category)
            .ThenBy(definition => definition.Param)
            .ToArray();

        ImGui.TextUnformatted($"Showing {definitions.Length} pose(s)");

        using var child = ImRaii.Child("HousingNpcPoseBrowserChild", new Vector2(0, 250), true);
        if (!child.Success)
            return;

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseBrowserTable", 7, tableFlags))
            return;

        ImGui.TableSetupColumn("Param");
        ImGui.TableSetupColumn("Pose");
        ImGui.TableSetupColumn("Category");
        ImGui.TableSetupColumn("Scene fit");
        ImGui.TableSetupColumn("Confidence");
        ImGui.TableSetupColumn("Notes");
        ImGui.TableSetupColumn("Actions");
        ImGui.TableHeadersRow();

        foreach (var definition in definitions)
        {
            ImGui.TableNextRow();
            TableText(definition.Param.ToString());
            TableText(definition.DisplayName);
            TableText(definition.Category.ToString());
            TableText(GetSceneFitText(definition));
            TableText(definition.Confidence.ToString());
            TableText(string.IsNullOrWhiteSpace(definition.Notes) ? definition.ObservedText : definition.Notes);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Apply##poseApply{definition.Param}"))
                ApplyPoseDefinition(definition, saveAfterApply: false);

            ImGui.SameLine();

            if (ImGui.SmallButton($"Apply + save##poseSave{definition.Param}"))
                ApplyPoseDefinition(definition, saveAfterApply: true);
        }

        ImGui.EndTable();
    }

    private void DrawPoseCategoryFilterCombo()
    {
        ImGui.SetNextItemWidth(220);
        if (!ImGui.BeginCombo("Category", PoseCategoryFilterNames[Math.Clamp(poseCategoryFilterIndex, 0, PoseCategoryFilterNames.Length - 1)]))
            return;

        for (var i = 0; i < PoseCategoryFilterNames.Length; i++)
        {
            var selected = i == poseCategoryFilterIndex;
            if (ImGui.Selectable(PoseCategoryFilterNames[i], selected))
                poseCategoryFilterIndex = i;

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }

    private bool PoseBrowserMatches(PoseDefinition definition)
    {
        var categoryFilter = PoseCategoryFilters[Math.Clamp(poseCategoryFilterIndex, 0, PoseCategoryFilters.Length - 1)];
        if (categoryFilter is { } category && definition.Category != category)
            return false;

        if (string.IsNullOrWhiteSpace(poseSearch))
            return true;

        var search = poseSearch.Trim();
        return Contains(definition.DisplayName, search)
            || Contains(definition.ObservedText, search)
            || Contains(definition.Key, search)
            || Contains(definition.CsvText, search)
            || Contains(definition.Notes ?? string.Empty, search)
            || definition.Aliases.Any(alias => Contains(alias, search));
    }

    private static bool Contains(string source, string search)
    {
        return source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetSceneFitText(PoseDefinition definition)
    {
        return definition.Safety switch
        {
            PoseSafety.NeedsYOffset => "Furniture / adjust Y",
            PoseSafety.NeedsFurniture => "Needs furniture",
            PoseSafety.GlitchyOrRestricted => "Restricted / odd",
            PoseSafety.Experimental => definition.Category == PoseCategory.Dance ? "Fun / active" : "Experimental",
            _ => definition.Category switch
            {
                PoseCategory.Core => "Housing staple",
                PoseCategory.Prop => "Good scene prop",
                PoseCategory.Gesture => "Standing gesture",
                PoseCategory.Exercise => "Specific scene",
                PoseCategory.Performance => "Cheer / stage",
                _ => "General scene",
            }
        };
    }

    private void ApplyPoseDefinition(PoseDefinition definition, bool saveAfterApply)
    {
        labLastPoseParam = definition.Param;
        labLastPoseLabel = definition.DisplayName;

        if (!plugin.ApplyNamedPose(labObjectIndex, definition.Key, "pose browser"))
            return;

        if (saveAfterApply)
            plugin.SavePoseForObjectIndex(labObjectIndex, definition.DisplayName, definition.Param, labOffsetY);
    }

    private void DrawYOffsetControls()
    {
        ImGui.TextUnformatted("Visual Y offset");
        ImGui.TextWrapped("Slide for normal use, then use small nudges for precise furniture alignment. This is a local draw offset only and saves with the pose.");

        if (ImGui.SliderFloat("Y offset", ref labOffsetY, Plugin.MinLocalYOffset, Plugin.MaxLocalYOffset, "%+.2f"))
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

        if (ImGui.SmallButton("Reset Y"))
        {
            labOffsetY = 0.0f;
            plugin.ApplyYOffsetByIndex(labObjectIndex, 0.0f, "selected editor");
        }
    }

    private void DrawSaveApplyStrip()
    {
        if (ImGui.Button("Apply selected pose"))
        {
            if (PoseCatalogue.TryGetByParam(labLastPoseParam, out var definition))
                ApplyPoseDefinition(definition, saveAfterApply: false);
            else
                plugin.ApplyInPositionLoop(labObjectIndex, labLastPoseParam);
        }

        ImGui.SameLine();

        if (ImGui.Button("Apply + save pose/Y"))
        {
            if (PoseCatalogue.TryGetByParam(labLastPoseParam, out var definition))
                ApplyPoseDefinition(definition, saveAfterApply: true);
            else if (plugin.ApplyInPositionLoop(labObjectIndex, labLastPoseParam))
                plugin.SavePoseForObjectIndex(labObjectIndex, labLastPoseLabel, labLastPoseParam, labOffsetY);
        }

        ImGui.SameLine();

        if (ImGui.Button("Save current pose/Y"))
            plugin.SavePoseForObjectIndex(labObjectIndex, labLastPoseLabel, labLastPoseParam, labOffsetY);

        ImGui.SameLine();

        if (ImGui.Button("Apply saved"))
            plugin.ApplySavedPoseForObjectIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Restore actor"))
            plugin.RestoreActorModeByIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Clear saved"))
            plugin.ClearSavedPoseForObjectIndex(labObjectIndex);
    }

    private void DrawRoomOverview()
    {
        using var node = ImRaii.TreeNode($"Room actor overview ({plugin.ScanResults.Count})");
        if (!node.Success)
            return;

        DrawSceneTable(plugin.ScanResults);
    }

    private NpcScanResult? CurrentSelectedScanResult()
    {
        return plugin.ScanResults.FirstOrDefault(result => result.ObjectIndex == labObjectIndex);
    }

    private void DrawSceneTable(IReadOnlyList<NpcScanResult> results)
    {
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

        if (ImGui.SmallButton($"Select##edit{result.ObjectIndex}"))
            SetLabTarget(result);

        ImGui.SameLine();

        if (ImGui.SmallButton($"Apply saved##apply{result.ObjectIndex}"))
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

    private void DrawAdvancedTools()
    {
        using var node = ImRaii.TreeNode("Advanced / discovery tools");
        if (!node.Success)
            return;

        DrawCustomParamLab();
        ImGui.Spacing();
        DrawPoseObservationLogger();
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
        ImGui.TextWrapped("For systematic discovery, pick a safe NPC target, choose InPositionLoop, and step through 0-255. Record visible results in the logger below before moving on.");

        var labIndexAsInt = (int)labObjectIndex;
        if (ImGui.InputInt("Object index", ref labIndexAsInt))
        {
            labIndexAsInt = Math.Clamp(labIndexAsInt, 0, (int)ushort.MaxValue);
            labObjectIndex = (ushort)labIndexAsInt;
            labTargetName = "<manual index>";
            labTargetBaseId = 0;
            labTargetPosition = "-";
        }

        DrawDiscoveryModeCombo();

        if (ImGui.InputInt("Param ID", ref labParam))
            labParam = Math.Clamp(labParam, 0, 255);

        if (PoseCatalogue.TryGetByParam((byte)labParam, out var builtIn))
            ImGui.TextWrapped($"Built-in catalogue match: {builtIn.DisplayName} [{builtIn.Confidence}; {builtIn.Category}] | {builtIn.CsvText}");
        else
            ImGui.TextWrapped("Built-in catalogue match: none yet. This is a good candidate for observation logging.");

        if (ImGui.Button("Apply previous"))
            ApplyDiscoveryParam(-1);

        ImGui.SameLine();

        if (ImGui.Button("Apply current"))
            ApplyDiscoveryParam(0);

        ImGui.SameLine();

        if (ImGui.Button("Apply next"))
            ApplyDiscoveryParam(1);

        ImGui.SameLine();

        if (ImGui.Button("Restore target##discoveryRestore"))
            plugin.RestoreActorModeByIndex(labObjectIndex);

        ImGui.SameLine();

        if (ImGui.Button("Normal fallback##discoveryNormal"))
            plugin.ApplyNormalMode(labObjectIndex);

        using var debugNode = ImRaii.TreeNode("Catalogue quick-apply entries");
        if (debugNode.Success)
        {
            DrawPoseButtonsForCategory(PoseCategory.Dance);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Exercise);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Performance);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Prop);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Experimental);
            ImGui.Spacing();
            DrawPoseButtonsForCategory(PoseCategory.Unknown);
        }
    }

    private void DrawPoseObservationLogger()
    {
        using var node = ImRaii.TreeNode("Pose discovery logger");
        if (!node.Success)
            return;

        ImGui.TextWrapped("Use this to record what a live-tested param visibly does. These observations are local notes saved in your plugin config; later we can promote good entries into PoseCatalogue.cs and reconcile them against the CSVs.");
        ImGui.TextUnformatted($"Current test: {CurrentDiscoveryMode} {labParam} on {labTargetName} [{labObjectIndex}]");

        if (PoseCatalogue.TryGetByParam((byte)labParam, out var builtIn))
        {
            if (ImGui.SmallButton("Fill from built-in catalogue"))
            {
                observationName = builtIn.ObservedText;
                observationCategory = builtIn.Category.ToString();
                discoveryConfidenceIndex = Array.IndexOf(DiscoveryConfidences, builtIn.Confidence.ToString());
                if (discoveryConfidenceIndex < 0)
                    discoveryConfidenceIndex = 2;
                observationNotes = builtIn.Notes ?? string.Empty;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"Built-in: {builtIn.DisplayName} | {builtIn.CsvText}");
        }

        ImGui.InputText("Observed name", ref observationName, 160);
        ImGui.InputText("Category", ref observationCategory, 80);
        DrawDiscoveryConfidenceCombo();
        ImGui.InputText("Notes", ref observationNotes, 512);

        if (ImGui.Button("Save observation for current param"))
        {
            plugin.SavePoseObservation(
                (byte)labParam,
                CurrentDiscoveryMode,
                observationName,
                observationCategory,
                CurrentDiscoveryConfidence,
                observationNotes,
                labTargetName,
                labTargetBaseId);
        }

        ImGui.SameLine();

        if (ImGui.Button("Delete current observation"))
            plugin.DeletePoseObservation((byte)labParam, CurrentDiscoveryMode);

        ImGui.SameLine();

        if (ImGui.Button("Copy observations CSV"))
            ImGui.SetClipboardText(plugin.ExportPoseObservationsCsv());

        DrawObservationTable();
    }

    private void DrawObservationTable()
    {
        var observations = plugin.Configuration.PoseObservations ?? new List<PoseObservationEntry>();
        if (observations.Count == 0)
        {
            ImGui.TextWrapped("No observations saved yet. Apply a param, describe what you see, then save it here.");
            return;
        }

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollX |
            ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("HousingNpcPoseObservationTable", 10, tableFlags))
            return;

        ImGui.TableSetupColumn("Mode");
        ImGui.TableSetupColumn("Param");
        ImGui.TableSetupColumn("Observed");
        ImGui.TableSetupColumn("Category");
        ImGui.TableSetupColumn("Confidence");
        ImGui.TableSetupColumn("Built-in");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Area");
        ImGui.TableSetupColumn("Updated");
        ImGui.TableSetupColumn("Notes");
        ImGui.TableHeadersRow();

        foreach (var entry in observations
            .OrderBy(entry => entry.Mode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Param))
        {
            PoseCatalogue.TryGetByParam(entry.Param, out var builtIn);
            ImGui.TableNextRow();
            TableText(entry.Mode);
            TableText(entry.Param.ToString());
            TableText(entry.DisplayName);
            TableText(entry.Category);
            TableText(entry.Confidence);
            TableText(builtIn == null ? "-" : $"{builtIn.DisplayName} [{builtIn.Confidence}]");
            TableText(string.IsNullOrWhiteSpace(entry.TargetName) ? "-" : entry.TargetName);
            TableText(string.IsNullOrWhiteSpace(entry.TerritoryLabel) ? entry.TerritoryType.ToString() : entry.TerritoryLabel);
            TableText(string.IsNullOrWhiteSpace(entry.UpdatedAtUtc) ? "-" : entry.UpdatedAtUtc);
            TableText(string.IsNullOrWhiteSpace(entry.Notes) ? "-" : entry.Notes);
        }

        ImGui.EndTable();
    }

    private void ApplyDiscoveryParam(int delta)
    {
        labParam = Math.Clamp(labParam + delta, 0, 255);
        labLastPoseParam = (byte)labParam;
        labLastPoseLabel = PoseCatalogue.GetDisplayName(labLastPoseParam);

        if (CurrentDiscoveryMode == "EmoteLoop")
            plugin.ApplyEmoteLoop(labObjectIndex, labLastPoseParam);
        else
            plugin.ApplyInPositionLoop(labObjectIndex, labLastPoseParam);
    }

    private void DrawDiscoveryModeCombo()
    {
        if (!ImGui.BeginCombo("Discovery mode", CurrentDiscoveryMode))
            return;

        for (var i = 0; i < DiscoveryModes.Length; i++)
        {
            var selected = i == discoveryModeIndex;
            if (ImGui.Selectable(DiscoveryModes[i], selected))
                discoveryModeIndex = i;

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }

    private void DrawDiscoveryConfidenceCombo()
    {
        if (!ImGui.BeginCombo("Confidence", CurrentDiscoveryConfidence))
            return;

        for (var i = 0; i < DiscoveryConfidences.Length; i++)
        {
            var selected = i == discoveryConfidenceIndex;
            if (ImGui.Selectable(DiscoveryConfidences[i], selected))
                discoveryConfidenceIndex = i;

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }

    private string CurrentDiscoveryMode => DiscoveryModes[Math.Clamp(discoveryModeIndex, 0, DiscoveryModes.Length - 1)];

    private string CurrentDiscoveryConfidence => DiscoveryConfidences[Math.Clamp(discoveryConfidenceIndex, 0, DiscoveryConfidences.Length - 1)];

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

        ImGui.TextWrapped("This table reconciles HousingNpcPose's live-tested small pose params with CSV emote/action metadata. Param remains the source of truth; CSV rows are evidence/cross-reference only, not a direct formula. General one-shot emotes such as /bow and /welcome are not currently exposed by this small-param route.");

        var tableFlags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit |
            ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("HousingNpcPoseCatalogueTable", 10, tableFlags))
            return;

        ImGui.TableSetupColumn("Param");
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Observed");
        ImGui.TableSetupColumn("Category");
        ImGui.TableSetupColumn("Safety");
        ImGui.TableSetupColumn("Confidence");
        ImGui.TableSetupColumn("CSV / Timeline crosswalk");
        ImGui.TableSetupColumn("CSV row");
        ImGui.TableSetupColumn("Key");
        ImGui.TableSetupColumn("Notes");
        ImGui.TableHeadersRow();

        foreach (var definition in PoseCatalogue.All
            .OrderBy(definition => definition.Confidence)
            .ThenBy(definition => definition.Category)
            .ThenBy(definition => definition.Param))
        {
            ImGui.TableNextRow();
            TableText(definition.Param.ToString());
            TableText(definition.DisplayName);
            TableText(definition.ObservedText);
            TableText(definition.Category.ToString());
            TableText(definition.Safety.ToString());
            TableText(definition.Confidence.ToString());
            TableText(definition.CsvText);
            TableText(definition.CsvRowText);
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
