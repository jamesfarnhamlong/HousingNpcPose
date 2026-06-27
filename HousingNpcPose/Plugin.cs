using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ClientGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using HousingNpcPose.Windows;
using Lumina.Excel.Sheets;

namespace HousingNpcPose;

public sealed unsafe class Plugin : IDalamudPlugin
{
    private const string CommandName = "/hnpcpose";
    public const float MinLocalYOffset = -10.0f;
    public const float MaxLocalYOffset = 10.0f;

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("HousingNpcPose");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly Dictionary<ulong, ActorModeSnapshot> actorModeSnapshots = new();
    private readonly Dictionary<ulong, AppliedPoseRecord> appliedPoseRecords = new();
    private readonly Dictionary<ulong, float> appliedYOffsetRecords = new();

    private DateTime autoApplyUntilUtc = DateTime.MinValue;
    private DateTime nextAutoApplyAttemptUtc = DateTime.MinValue;

    private static readonly IReadOnlyDictionary<uint, string> BlockedPoseBaseIds = new Dictionary<uint, string>
    {
        // Known creature / non-player-skeleton housing NPCs. They can appear as EventNpc,
        // but they do not accept normal humanoid CharacterMode pose changes safely/reliably.
        [1026171] = "Blocked: non-humanoid / creature NPC (Namazu Mender)",
    };

    public IReadOnlyList<NpcScanResult> ScanResults { get; private set; } = Array.Empty<NpcScanResult>();
    public DateTime? LastScanTime { get; private set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Housing NPC Pose. Usage: /hnpcpose, /hnpcpose scan, /hnpcpose pose <idx> <catalogueName>, /hnpcpose save <idx> <poseName|param>, /hnpcpose applysaved, /hnpcpose poses, /hnpcpose auto on|off"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        Framework.Update += OnFrameworkUpdate;
        NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;

        ScheduleAutoApply("plugin load");

        Log.Information("HousingNpcPose loaded. v0.4.1 scene presets.");
    }

    public void Dispose()
    {
        // Try to restore anything this client-side build changed before unloading.
        RestoreAllActorModes(printResult: false);

        Framework.Update -= OnFrameworkUpdate;
        NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        ClientState.TerritoryChanged -= OnTerritoryChanged;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ToggleMainUi();
            return;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var subCommand = parts[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "scan":
                ScanObjects();
                MainWindow.IsOpen = true;
                break;

            case "clear":
                ClearScan();
                ChatGui.Print("Cleared scan results.", "HNpcPose");
                break;

            case "config":
            case "settings":
                ToggleConfigUi();
                break;

            case "test":
            case "sit":
            case "groundsit":
                ApplyNamedPoseFromCommand(parts, "sit");
                break;

            case "bench":
            case "chair":
            case "chairsit":
            case "sitbench":
                ApplyNamedPoseFromCommand(parts, "bench");
                break;

            case "doze":
            case "bed":
            case "lie":
            case "liedown":
                ApplyNamedPoseFromCommand(parts, "doze");
                break;

            case "stepdance":
                ApplyNamedPoseFromCommand(parts, "stepdance");
                break;

            case "harvestdance":
                ApplyNamedPoseFromCommand(parts, "harvestdance");
                break;

            case "balldance":
            case "ball":
                ApplyNamedPoseFromCommand(parts, "balldance");
                break;

            case "mandervilledance":
            case "manderville":
                ApplyNamedPoseFromCommand(parts, "manderville");
                break;

            case "thavdance":
            case "thavnairiandance":
            case "thavnairian":
                ApplyNamedPoseFromCommand(parts, "thavdance");
                break;

            case "sweat":
                ApplyNamedPoseFromCommand(parts, "sweat");
                break;

            case "shiver":
                ApplyNamedPoseFromCommand(parts, "shiver");
                break;

            case "confirm":
                ApplyNamedPoseFromCommand(parts, "confirm");
                break;

            case "scheme":
                ApplyNamedPoseFromCommand(parts, "scheme");
                break;

            case "reprimand":
                ApplyNamedPoseFromCommand(parts, "reprimand");
                break;

            case "lean":
                ApplyNamedPoseFromCommand(parts, "lean");
                break;

            case "pose":
                ApplyPoseByNameFromCommand(parts);
                break;

            case "pos":
            case "position":
            case "inposition":
                ApplyModeFromCommand(parts, CharacterModes.InPositionLoop, defaultParam: 1, labelPrefix: "InPositionLoop");
                break;

            case "loop":
            case "emoteloop":
                ApplyModeFromCommand(parts, CharacterModes.EmoteLoop, defaultParam: 1, labelPrefix: "EmoteLoop");
                break;

            case "normal":
                ApplyModeFromCommand(parts, CharacterModes.Normal, defaultParam: 0, labelPrefix: "Normal");
                break;

            case "mode":
                ApplyFlexibleModeFromCommand(parts);
                break;

            case "restore":
            case "reset":
                RestoreFromCommand(parts);
                break;

            case "applysaved":
            case "applyauto":
                ApplySavedPosesNow();
                break;

            case "offset":
            case "yoffset":
                ApplyYOffsetFromCommand(parts);
                break;

            case "saveoffset":
            case "savey":
            case "saveyoffset":
                SaveYOffsetFromCommand(parts);
                break;

            case "auto":
                SetAutoApplyFromCommand(parts);
                break;

            case "nameplates":
            case "nameplate":
                SetNameplatesFromCommand(parts);
                break;

            case "save":
            case "savepose":
                SavePoseFromCommand(parts);
                break;

            case "clearsaved":
            case "clearpose":
                ClearSavedFromCommand(parts);
                break;

            case "catalogue":
            case "catalog":
            case "poses":
                PrintPoseCatalogue();
                break;

            case "help":
                PrintHelp();
                break;

            default:
                ChatGui.PrintError($"Unknown command '{subCommand}'. Try /hnpcpose help.", "HNpcPose");
                break;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    public string GetTerritoryLabel()
    {
        var territoryId = ClientState.TerritoryType;

        if (DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
        {
            var placeName = territoryRow.PlaceName.Value.Name.ToString();
            return string.IsNullOrWhiteSpace(placeName)
                ? $"Territory {territoryId}"
                : $"{placeName} ({territoryId})";
        }

        return $"Territory {territoryId}";
    }

    public void ScanObjects()
    {
        var results = new List<NpcScanResult>();

        foreach (var obj in ObjectTable)
        {
            try
            {
                if (!obj.IsValid())
                    continue;

                if (!ShouldIncludeObject(obj))
                    continue;

                var note = ClassifyObject(obj);
                var canPose = IsPoseableCandidate(obj);
                var savedPose = FindSavedPoseForGameObject(obj);

                results.Add(NpcScanResult.FromGameObject(
                    obj,
                    note,
                    canPose,
                    actorModeSnapshots.ContainsKey(obj.GameObjectId) || appliedYOffsetRecords.ContainsKey(obj.GameObjectId),
                    GetPluginPoseDisplay(obj.GameObjectId),
                    savedPose?.DisplayText ?? "-"));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to inspect an object while scanning.");
            }
        }

        ScanResults = results
            .OrderBy(result => result.ObjectIndex)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        LastScanTime = DateTime.Now;

        ChatGui.Print(
            $"Scanned {ScanResults.Count} object(s) in {GetTerritoryLabel()}. v0.4.1 scene presets",
            "HNpcPose");
    }

    public void ClearScan()
    {
        ScanResults = Array.Empty<NpcScanResult>();
        LastScanTime = null;
    }

    private string GetPluginPoseDisplay(ulong gameObjectId)
    {
        var parts = new List<string>();

        if (appliedPoseRecords.TryGetValue(gameObjectId, out var appliedPose))
            parts.Add(appliedPose.DisplayText);

        if (appliedYOffsetRecords.TryGetValue(gameObjectId, out var offsetY) && Math.Abs(offsetY) > 0.001f)
            parts.Add($"Y{offsetY:+0.00;-0.00;0.00}");

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    public bool ApplyDefaultTestPose(ushort objectIndex)
    {
        return ApplyNamedPose(objectIndex, "Sit", "UI button");
    }

    public bool TryGetNamedPoseParam(string poseName, out byte param, out string label)
    {
        if (PoseCatalogue.TryGetByAlias(poseName, out var definition))
        {
            param = definition.Param;
            label = definition.DisplayName;
            return true;
        }

        param = 0;
        label = string.Empty;
        return false;
    }

    public bool ApplyNamedPose(ushort objectIndex, string poseName, string source = "named pose")
    {
        var key = NormalizePoseName(poseName);
        if (key == "normal")
            return ApplyActorMode(objectIndex, CharacterModes.Normal, 0, "Normal", source);

        return TryGetNamedPoseParam(poseName, out var param, out var label)
            ? ApplyActorMode(objectIndex, CharacterModes.InPositionLoop, param, label, source)
            : RefuseUnknownNamedPose(poseName);
    }

    public bool ApplyInPositionLoop(ushort objectIndex, byte param)
    {
        return ApplyActorMode(objectIndex, CharacterModes.InPositionLoop, param, GetInPositionLabel(param), "pose lab");
    }

    public bool ApplyEmoteLoop(ushort objectIndex, byte param)
    {
        return ApplyActorMode(objectIndex, CharacterModes.EmoteLoop, param, GetLoopLabel(param), "pose lab");
    }

    public bool ApplyNormalMode(ushort objectIndex)
    {
        return ApplyActorMode(objectIndex, CharacterModes.Normal, 0, "Normal", "pose lab");
    }

    public bool ApplyYOffsetByIndex(ushort objectIndex, float offsetY, string source = "Y offset")
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        return ApplyYOffsetToObject(obj, offsetY, source, printResult: true, rescan: true);
    }

    public bool SaveYOffsetForObjectIndex(ushort objectIndex, float offsetY)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        if (!IsPoseableCandidate(obj))
        {
            ChatGui.PrintError($"Refusing to save Y offset for {GetObjectLabel(obj)} because it is not a safe pose candidate.", "HNpcPose");
            return false;
        }

        var saved = FindSavedPoseForGameObject(obj);
        if (saved == null)
        {
            ChatGui.PrintError($"No saved pose found for {GetObjectLabel(obj)}. Save a pose first, then save/apply the Y offset.", "HNpcPose");
            return false;
        }

        saved.OffsetY = ClampYOffset(offsetY);
        Configuration.Save();

        var offsetText = Math.Abs(saved.OffsetY) > 0.001f ? saved.OffsetY.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) : "0.00";
        ChatGui.Print($"Saved Y offset {offsetText} for {GetObjectLabel(obj)}. Saved pose is now {saved.DisplayText}.", "HNpcPose");
        if (Configuration.HideNameplatesForPosedNpcs)
            NamePlateGui.RequestRedraw();
        ScanObjects();
        return true;
    }

    public bool RestoreActorModeByIndex(ushort objectIndex, bool printResult = true)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            if (printResult)
                ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        var restoredAny = false;

        if (actorModeSnapshots.TryGetValue(obj.GameObjectId, out var snapshot))
            restoredAny |= RestoreActorMode(obj, snapshot, printResult);

        if (appliedYOffsetRecords.ContainsKey(obj.GameObjectId))
            restoredAny |= RestoreYOffset(obj, printResult);

        if (!restoredAny)
        {
            if (printResult)
                ChatGui.PrintError($"No saved original mode or local Y offset for {GetObjectLabel(obj)}. Use /hnpcpose normal {objectIndex} as a manual fallback if needed.", "HNpcPose");
            return false;
        }

        if (printResult)
            ScanObjects();
        return true;
    }

    public int RestoreAllActorModes(bool printResult = true)
    {
        var trackedIds = actorModeSnapshots.Keys
            .Concat(appliedYOffsetRecords.Keys)
            .Distinct()
            .ToArray();

        var restored = 0;

        foreach (var gameObjectId in trackedIds)
        {
            var obj = ObjectTable.SearchById(gameObjectId);
            if (obj == null || !obj.IsValid())
                continue;

            var restoredThisObject = false;

            if (actorModeSnapshots.TryGetValue(gameObjectId, out var snapshot))
                restoredThisObject |= RestoreActorMode(obj, snapshot, printResult: false);

            if (appliedYOffsetRecords.ContainsKey(gameObjectId))
                restoredThisObject |= RestoreYOffset(obj, printResult: false);

            if (restoredThisObject)
                restored++;
        }

        if (printResult)
        {
            ChatGui.Print($"Restored {restored} actor local pose/offset target(s).", "HNpcPose");
            ScanObjects();
        }

        return restored;
    }

    public void ScheduleAutoApply(string reason)
    {
        if (!Configuration.AutoApplySavedPoses || Configuration.SavedPoses.Count == 0)
            return;

        var now = DateTime.UtcNow;
        autoApplyUntilUtc = now.AddSeconds(Math.Clamp(Configuration.AutoApplyRetrySeconds, 1, 60));
        nextAutoApplyAttemptUtc = now.AddMilliseconds(750);
        Log.Debug($"Scheduled saved-pose auto-apply for {Configuration.AutoApplyRetrySeconds}s because {reason}.");
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!Configuration.HideNameplatesForPosedNpcs)
            return;

        foreach (var handler in handlers)
        {
            try
            {
                var obj = handler.GameObject;
                if (obj == null || !obj.IsValid())
                    continue;

                if (!ShouldHideNamePlateForObject(obj))
                    continue;

                // Remove only the nameplate text/icons for NPCs this plugin is actively managing or has saved for this room.
                // The handler is frame-scoped; do not store it.
                handler.RemoveName();
                handler.DisplayTitle = false;
                handler.NameIconId = -1;
                handler.MarkerIconId = 0;
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed while trying to hide a posed NPC nameplate: {ex.Message}");
            }
        }
    }

    private bool ShouldHideNamePlateForObject(IGameObject obj)
    {
        if (!Configuration.HideNameplatesForPosedNpcs || !IsPoseableCandidate(obj))
            return false;

        if (appliedPoseRecords.ContainsKey(obj.GameObjectId) || appliedYOffsetRecords.ContainsKey(obj.GameObjectId))
            return true;

        var saved = FindSavedPoseForGameObject(obj);
        return saved != null && saved.Enabled;
    }

    public void SetHideNameplatesForPosedNpcs(bool enabled)
    {
        Configuration.HideNameplatesForPosedNpcs = enabled;
        Configuration.Save();

        try
        {
            NamePlateGui.RequestRedraw();
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to request nameplate redraw: {ex.Message}");
        }
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        ScheduleAutoApply($"territory changed to {territoryType}");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Configuration.AutoApplySavedPoses)
            return;

        var now = DateTime.UtcNow;
        if (now > autoApplyUntilUtc || now < nextAutoApplyAttemptUtc)
            return;

        nextAutoApplyAttemptUtc = now.AddSeconds(1);
        ApplySavedPosesInternal(printSummary: false);
    }

    public int ApplySavedPosesNow()
    {
        var applied = ApplySavedPosesInternal(printSummary: true);
        ScanObjects();
        return applied;
    }

    private int ApplySavedPosesInternal(bool printSummary)
    {
        var applied = 0;
        var currentTerritory = ClientState.TerritoryType;
        var currentSaved = Configuration.SavedPoses
            .Where(entry => entry.Enabled && entry.TerritoryType == currentTerritory)
            .ToArray();

        if (currentSaved.Length == 0)
        {
            if (printSummary)
                ChatGui.Print("No saved poses for the current territory.", "HNpcPose");
            return 0;
        }

        foreach (var obj in ObjectTable)
        {
            if (obj == null || !obj.IsValid() || !IsPoseableCandidate(obj))
                continue;

            var saved = FindSavedPoseForGameObject(obj);
            if (saved == null || !saved.Enabled)
                continue;

            if (ApplySavedPoseToObject(obj, saved, "saved auto-apply", printResult: false, rescan: false))
                applied++;
        }

        if (printSummary)
            ChatGui.Print($"Applied {applied} saved local pose(s) in {GetTerritoryLabel()}.", "HNpcPose");

        return applied;
    }

    public bool SavePoseForObjectIndex(ushort objectIndex, string poseLabel, byte poseParam, float offsetY = 0.0f)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        if (!IsPoseableCandidate(obj))
        {
            ChatGui.PrintError($"Refusing to save pose for {GetObjectLabel(obj)} because it is not a safe pose candidate.", "HNpcPose");
            return false;
        }

        var position = obj.Position;
        var entry = new SavedPoseEntry
        {
            Enabled = true,
            TerritoryType = ClientState.TerritoryType,
            TerritoryLabel = GetTerritoryLabel(),
            Name = obj.Name.ToString(),
            BaseId = obj.BaseId,
            PositionX = position.X,
            PositionY = position.Y,
            PositionZ = position.Z,
            PoseLabel = string.IsNullOrWhiteSpace(poseLabel) ? $"Custom Pos {poseParam}" : poseLabel,
            PoseParam = poseParam,
            OffsetY = ClampYOffset(offsetY),
        };

        RemoveSavedPoseForObject(obj, saveAfterRemove: false);
        Configuration.SavedPoses.Add(entry);
        Configuration.Save();

        ChatGui.Print($"Saved {entry.DisplayText} for {GetObjectLabel(obj)} in {entry.TerritoryLabel}.", "HNpcPose");
        if (Configuration.HideNameplatesForPosedNpcs)
            NamePlateGui.RequestRedraw();
        ScanObjects();
        return true;
    }

    public bool ApplySavedPoseForObjectIndex(ushort objectIndex)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        if (!IsPoseableCandidate(obj))
        {
            ChatGui.PrintError($"Refusing to apply saved pose to {GetObjectLabel(obj)} because it is not a safe pose candidate.", "HNpcPose");
            return false;
        }

        var saved = FindSavedPoseForGameObject(obj);
        if (saved == null || !saved.Enabled)
        {
            ChatGui.PrintError($"No enabled saved pose found for {GetObjectLabel(obj)} in this area.", "HNpcPose");
            return false;
        }

        return ApplySavedPoseToObject(obj, saved, "saved target", printResult: true, rescan: true);
    }

    public bool ClearSavedPoseForObjectIndex(ushort objectIndex)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        var removed = RemoveSavedPoseForObject(obj, saveAfterRemove: true);
        if (removed > 0 && Configuration.HideNameplatesForPosedNpcs)
            NamePlateGui.RequestRedraw();
        ChatGui.Print(removed > 0
            ? $"Cleared {removed} saved pose entry/entries for {GetObjectLabel(obj)}."
            : $"No saved pose found for {GetObjectLabel(obj)}.", "HNpcPose");
        ScanObjects();
        return removed > 0;
    }

    public void ClearAllSavedPosesForCurrentTerritory()
    {
        var territory = ClientState.TerritoryType;
        var removed = Configuration.SavedPoses.RemoveAll(entry => entry.TerritoryType == territory);
        Configuration.Save();
        if (removed > 0 && Configuration.HideNameplatesForPosedNpcs)
            NamePlateGui.RequestRedraw();
        ChatGui.Print($"Cleared {removed} saved pose entry/entries for {GetTerritoryLabel()}.", "HNpcPose");
        ScanObjects();
    }



    public IReadOnlyList<ScenePresetEntry> GetScenePresetsForCurrentTerritory()
    {
        var territory = ClientState.TerritoryType;
        return (Configuration.ScenePresets ??= new List<ScenePresetEntry>())
            .Where(scene => scene.TerritoryType == territory)
            .OrderBy(scene => scene.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scene => scene.UpdatedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ScenePresetEntry? GetScenePresetById(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            return null;

        return (Configuration.ScenePresets ??= new List<ScenePresetEntry>())
            .FirstOrDefault(scene => string.Equals(scene.Id, sceneId, StringComparison.OrdinalIgnoreCase));
    }

    public ScenePresetEntry? SaveCurrentSavedPosesAsScene(string name)
    {
        var currentSaved = GetCurrentTerritorySavedPoseCopies();
        if (currentSaved.Count == 0)
        {
            ChatGui.PrintError("No saved poses in this area yet. Save at least one actor pose before creating a scene.", "HNpcPose");
            return null;
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var sceneName = CleanSceneName(name, (Configuration.ScenePresets?.Count ?? 0) + 1);
        var scene = new ScenePresetEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = sceneName,
            TerritoryType = ClientState.TerritoryType,
            TerritoryLabel = GetTerritoryLabel(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Poses = currentSaved,
        };

        (Configuration.ScenePresets ??= new List<ScenePresetEntry>()).Add(scene);
        Configuration.Save();
        ChatGui.Print($"Saved scene '{scene.Name}' with {scene.Poses.Count} actor(s) for {scene.TerritoryLabel}.", "HNpcPose");
        return scene;
    }

    public bool OverwriteScenePresetFromCurrentSavedPoses(string sceneId, string name)
    {
        var scene = GetScenePresetById(sceneId);
        if (scene == null)
        {
            ChatGui.PrintError("No scene selected to overwrite.", "HNpcPose");
            return false;
        }

        if (scene.TerritoryType != ClientState.TerritoryType)
        {
            ChatGui.PrintError($"Scene '{scene.Name}' belongs to {scene.TerritoryLabel}, not the current area.", "HNpcPose");
            return false;
        }

        var currentSaved = GetCurrentTerritorySavedPoseCopies();
        if (currentSaved.Count == 0)
        {
            ChatGui.PrintError("No saved poses in this area yet. Save at least one actor pose before overwriting a scene.", "HNpcPose");
            return false;
        }

        scene.Name = CleanSceneName(name, 1, scene.Name);
        scene.TerritoryLabel = GetTerritoryLabel();
        scene.UpdatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        scene.Poses = currentSaved;
        Configuration.Save();
        ChatGui.Print($"Overwrote scene '{scene.Name}' with {scene.Poses.Count} actor(s).", "HNpcPose");
        return true;
    }

    public bool RenameScenePreset(string sceneId, string name)
    {
        var scene = GetScenePresetById(sceneId);
        if (scene == null)
        {
            ChatGui.PrintError("No scene selected to rename.", "HNpcPose");
            return false;
        }

        scene.Name = CleanSceneName(name, 1, scene.Name);
        scene.UpdatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Configuration.Save();
        ChatGui.Print($"Renamed scene to '{scene.Name}'.", "HNpcPose");
        return true;
    }

    public bool DeleteScenePreset(string sceneId)
    {
        var scenes = Configuration.ScenePresets ??= new List<ScenePresetEntry>();
        var scene = GetScenePresetById(sceneId);
        if (scene == null)
        {
            ChatGui.PrintError("No scene selected to delete.", "HNpcPose");
            return false;
        }

        var removed = scenes.RemoveAll(entry => string.Equals(entry.Id, sceneId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            Configuration.Save();
            ChatGui.Print($"Deleted scene '{scene.Name}'.", "HNpcPose");
            return true;
        }

        ChatGui.PrintError($"Could not delete scene '{scene.Name}'.", "HNpcPose");
        return false;
    }

    public bool LoadScenePreset(string sceneId)
    {
        var scene = GetScenePresetById(sceneId);
        if (scene == null)
        {
            ChatGui.PrintError("No scene selected to load.", "HNpcPose");
            return false;
        }

        if (scene.TerritoryType != ClientState.TerritoryType)
        {
            ChatGui.PrintError($"Scene '{scene.Name}' belongs to {scene.TerritoryLabel}, not the current area.", "HNpcPose");
            return false;
        }

        Configuration.SavedPoses.RemoveAll(entry => entry.TerritoryType == ClientState.TerritoryType);
        foreach (var pose in scene.Poses.Select(CloneSavedPoseEntry))
        {
            pose.TerritoryType = ClientState.TerritoryType;
            pose.TerritoryLabel = GetTerritoryLabel();
            Configuration.SavedPoses.Add(pose);
        }

        Configuration.Save();
        ChatGui.Print($"Loaded scene '{scene.Name}' with {scene.Poses.Count} saved actor pose(s). Applying now...", "HNpcPose");
        ApplySavedPosesNow();
        if (Configuration.HideNameplatesForPosedNpcs)
            NamePlateGui.RequestRedraw();
        return true;
    }

    private List<SavedPoseEntry> GetCurrentTerritorySavedPoseCopies()
    {
        var currentTerritory = ClientState.TerritoryType;
        return Configuration.SavedPoses
            .Where(entry => entry.TerritoryType == currentTerritory)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.PositionX)
            .ThenBy(entry => entry.PositionY)
            .ThenBy(entry => entry.PositionZ)
            .Select(CloneSavedPoseEntry)
            .ToList();
    }

    private static SavedPoseEntry CloneSavedPoseEntry(SavedPoseEntry source)
    {
        return new SavedPoseEntry
        {
            Enabled = source.Enabled,
            TerritoryType = source.TerritoryType,
            TerritoryLabel = source.TerritoryLabel ?? string.Empty,
            Name = source.Name ?? string.Empty,
            BaseId = source.BaseId,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            PoseLabel = source.PoseLabel ?? string.Empty,
            PoseParam = source.PoseParam,
            OffsetY = source.OffsetY,
        };
    }

    private static string CleanSceneName(string name, int fallbackNumber, string? existingName = null)
    {
        var cleaned = (name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(cleaned))
            return cleaned.Length <= 80 ? cleaned : cleaned[..80];

        if (!string.IsNullOrWhiteSpace(existingName))
            return existingName!;

        return $"Scene {fallbackNumber}";
    }

    public void SavePoseObservation(byte param, string mode, string observedName, string category, string confidence, string notes, string targetName, uint targetBaseId)
    {
        var normalizedMode = NormalizeObservationMode(mode);
        var observations = Configuration.PoseObservations ??= new List<PoseObservationEntry>();
        var existing = observations.FirstOrDefault(entry => entry.Param == param && string.Equals(entry.Mode, normalizedMode, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            existing = new PoseObservationEntry
            {
                Mode = normalizedMode,
                Param = param,
            };
            observations.Add(existing);
        }

        existing.ObservedName = string.IsNullOrWhiteSpace(observedName) ? $"{normalizedMode} {param}" : observedName.Trim();
        existing.Category = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
        existing.Confidence = string.IsNullOrWhiteSpace(confidence) ? "Uncertain" : confidence.Trim();
        existing.Notes = notes?.Trim() ?? string.Empty;
        existing.TerritoryType = ClientState.TerritoryType;
        existing.TerritoryLabel = GetTerritoryLabel();
        existing.TargetName = string.IsNullOrWhiteSpace(targetName) ? "<unknown>" : targetName.Trim();
        existing.TargetBaseId = targetBaseId;
        existing.UpdatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        Configuration.Save();
        ChatGui.Print($"Saved observation: {existing.Mode} {existing.Param} = {existing.DisplayName} [{existing.Confidence}; {existing.Category}].", "HNpcPose");
    }

    public bool DeletePoseObservation(byte param, string mode)
    {
        var normalizedMode = NormalizeObservationMode(mode);
        var observations = Configuration.PoseObservations ??= new List<PoseObservationEntry>();
        var removed = observations.RemoveAll(entry => entry.Param == param && string.Equals(entry.Mode, normalizedMode, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            Configuration.Save();

        ChatGui.Print(removed > 0
            ? $"Deleted observation for {normalizedMode} {param}."
            : $"No observation found for {normalizedMode} {param}.", "HNpcPose");
        return removed > 0;
    }

    public string ExportPoseObservationsCsv()
    {
        var observations = Configuration.PoseObservations ?? new List<PoseObservationEntry>();
        var builder = new StringBuilder();
        builder.AppendLine("Mode,Param,ObservedName,Category,Confidence,Notes,TerritoryType,TerritoryLabel,TargetName,TargetBaseId,UpdatedAtUtc,BuiltInCatalogueName,BuiltInConfidence,BuiltInCsv");

        foreach (var entry in observations
            .OrderBy(entry => entry.Mode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Param))
        {
            PoseCatalogue.TryGetByParam(entry.Param, out var builtIn);
            builder.Append(Csv(entry.Mode));
            builder.Append(',');
            builder.Append(entry.Param.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Csv(entry.ObservedName));
            builder.Append(',');
            builder.Append(Csv(entry.Category));
            builder.Append(',');
            builder.Append(Csv(entry.Confidence));
            builder.Append(',');
            builder.Append(Csv(entry.Notes));
            builder.Append(',');
            builder.Append(entry.TerritoryType.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Csv(entry.TerritoryLabel));
            builder.Append(',');
            builder.Append(Csv(entry.TargetName));
            builder.Append(',');
            builder.Append(entry.TargetBaseId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Csv(entry.UpdatedAtUtc));
            builder.Append(',');
            builder.Append(Csv(builtIn?.DisplayName ?? string.Empty));
            builder.Append(',');
            builder.Append(Csv(builtIn?.Confidence.ToString() ?? string.Empty));
            builder.Append(',');
            builder.Append(Csv(builtIn?.CsvText ?? string.Empty));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string NormalizeObservationMode(string mode)
    {
        if (string.Equals(mode, "loop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "emoteloop", StringComparison.OrdinalIgnoreCase))
            return "EmoteLoop";

        return "InPositionLoop";
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public SavedPoseEntry? FindSavedPoseForScanResult(NpcScanResult result)
    {
        return Configuration.SavedPoses.FirstOrDefault(entry => SavedPoseMatches(entry, result));
    }

    private SavedPoseEntry? FindSavedPoseForGameObject(IGameObject obj)
    {
        var position = obj.Position;
        return Configuration.SavedPoses.FirstOrDefault(entry => SavedPoseMatches(entry, ClientState.TerritoryType, obj.Name.ToString(), obj.BaseId, position.X, position.Y, position.Z));
    }

    private int RemoveSavedPoseForObject(IGameObject obj, bool saveAfterRemove)
    {
        var position = obj.Position;
        var removed = Configuration.SavedPoses.RemoveAll(entry => SavedPoseMatches(entry, ClientState.TerritoryType, obj.Name.ToString(), obj.BaseId, position.X, position.Y, position.Z));
        if (saveAfterRemove && removed > 0)
            Configuration.Save();
        return removed;
    }

    private bool SavedPoseMatches(SavedPoseEntry entry, NpcScanResult result)
    {
        return SavedPoseMatches(entry, ClientState.TerritoryType, result.Name, result.BaseId, result.PositionX, result.PositionY, result.PositionZ);
    }

    private bool SavedPoseMatches(SavedPoseEntry entry, uint territoryType, string name, uint baseId, float x, float y, float z)
    {
        if (entry.TerritoryType != territoryType || entry.BaseId != baseId)
            return false;

        if (!string.Equals(NormalizeObjectName(entry.Name), NormalizeObjectName(name), StringComparison.OrdinalIgnoreCase))
            return false;

        var dx = entry.PositionX - x;
        var dy = entry.PositionY - y;
        var dz = entry.PositionZ - z;
        var distanceSquared = dx * dx + dy * dy + dz * dz;
        var tolerance = Math.Clamp(Configuration.SavedPosePositionTolerance, 0.05f, 5.0f);
        return distanceSquared <= tolerance * tolerance;
    }

    private static string NormalizeObjectName(string name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant();
    }

    private bool ShouldIncludeObject(IGameObject obj)
    {
        if (obj.ObjectKind is ObjectKind.None or ObjectKind.Pc)
            return false;

        if (Configuration.ShowAllNonPlayerObjects)
            return true;

        return obj.ObjectKind switch
        {
            ObjectKind.EventNpc => true,
            ObjectKind.Retainer => Configuration.IncludeRetainers,
            ObjectKind.BattleNpc => Configuration.IncludeBattleNpcs,
            ObjectKind.HousingEventObject => Configuration.IncludeHousingEventObjects,
            ObjectKind.EventObj => Configuration.IncludeEventObjects,
            _ => false
        };
    }

    private static string ClassifyObject(IGameObject obj)
    {
        if (obj.ObjectKind == ObjectKind.EventNpc && TryGetPoseBlockReason(obj, out var blockReason))
            return blockReason;

        return obj.ObjectKind switch
        {
            ObjectKind.EventNpc => "Pose candidate",
            ObjectKind.Retainer => "Retainer candidate; verify manually",
            ObjectKind.BattleNpc => "BattleNpc candidate; verify manually",
            ObjectKind.HousingEventObject => "Housing event object; ignore for posing",
            ObjectKind.EventObj => "Event object / door; ignore for posing",
            ObjectKind.Companion => "Companion/minion; excluded in normal mode",
            ObjectKind.Mount => "Mount; excluded in normal mode",
            ObjectKind.AreaObject => "Area object",
            ObjectKind.ReactionEventObject => "Reaction event object",
            ObjectKind.CardStand => "Card stand",
            ObjectKind.Ornament => "Ornament",
            _ => "Non-player object"
        };
    }

    private static bool IsPoseableCandidate(IGameObject obj)
    {
        return obj.ObjectKind == ObjectKind.EventNpc && !TryGetPoseBlockReason(obj, out _);
    }

    private static bool TryGetPoseBlockReason(IGameObject obj, out string reason)
    {
        if (BlockedPoseBaseIds.TryGetValue(obj.BaseId, out var blockedReason))
        {
            reason = blockedReason;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static string NormalizePoseName(string poseName)
    {
        var normalized = PoseCatalogue.NormalizeAlias(poseName);
        return normalized is "normal" or "default" or "restorepose" ? "normal" : normalized;
    }

    private static string GetInPositionLabel(byte param)
    {
        return PoseCatalogue.GetLabel(param, "Pos");
    }

    private static string GetLoopLabel(byte param)
    {
        return PoseCatalogue.GetLabel(param, "Loop");
    }

    private static bool RefuseUnknownNamedPose(string poseName)
    {
        ChatGui.PrintError($"Unknown named pose '{poseName}'. Known catalogue entries: {PoseCatalogue.KnownCommandNames()}, normal. Custom numeric params are still available via /hnpcpose pos <idx> <param> or the Advanced lab.", "HNpcPose");
        return false;
    }

    private void ApplyNamedPoseFromCommand(IReadOnlyList<string> parts, string poseName)
    {
        if (!TryParseObjectIndex(parts, out var objectIndex))
            return;

        ApplyNamedPose(objectIndex, poseName, "command");
    }

    private void ApplyPoseByNameFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 3)
        {
            ChatGui.PrintError("Usage: /hnpcpose pose <idx> <sit|bench|doze|lean|confirm|scheme|reprimand|sweat|shiver|normal>", "HNpcPose");
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        ApplyNamedPose(objectIndex, parts[2], "command");
    }

    private void ApplyModeFromCommand(IReadOnlyList<string> parts, CharacterModes mode, byte defaultParam, string labelPrefix)
    {
        if (!TryParseObjectIndex(parts, out var objectIndex))
            return;

        var param = defaultParam;
        if (parts.Count >= 3 && !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out param))
        {
            ChatGui.PrintError($"Invalid mode parameter '{parts[2]}'. Expected 0-255.", "HNpcPose");
            return;
        }

        var label = mode switch
        {
            CharacterModes.InPositionLoop => GetInPositionLabel(param),
            CharacterModes.EmoteLoop => GetLoopLabel(param),
            CharacterModes.Normal => $"Normal {param}",
            _ => $"{labelPrefix} {param}"
        };

        ApplyActorMode(objectIndex, mode, param, label, "command");
    }

    private void ApplyFlexibleModeFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 4)
        {
            ChatGui.PrintError("Usage: /hnpcpose mode <idx> <pos|loop|normal> <param>", "HNpcPose");
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        var modeName = parts[2].ToLowerInvariant();
        if (!byte.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var param))
        {
            ChatGui.PrintError($"Invalid mode parameter '{parts[3]}'. Expected 0-255.", "HNpcPose");
            return;
        }

        switch (modeName)
        {
            case "pos":
            case "position":
            case "inposition":
                ApplyActorMode(objectIndex, CharacterModes.InPositionLoop, param, GetInPositionLabel(param), "command");
                break;

            case "loop":
            case "emote":
            case "emoteloop":
                ApplyActorMode(objectIndex, CharacterModes.EmoteLoop, param, GetLoopLabel(param), "command");
                break;

            case "normal":
            case "default":
                ApplyActorMode(objectIndex, CharacterModes.Normal, param, $"Normal {param}", "command");
                break;

            default:
                ChatGui.PrintError($"Unknown mode '{parts[2]}'. Use pos, loop, or normal.", "HNpcPose");
                break;
        }
    }

    private void RestoreFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 2)
        {
            ChatGui.PrintError("Usage: /hnpcpose restore <idx|all>", "HNpcPose");
            return;
        }

        if (parts[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            RestoreAllActorModes();
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        RestoreActorModeByIndex(objectIndex);
    }

    private void ApplyYOffsetFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 3)
        {
            ChatGui.PrintError("Usage: /hnpcpose offset <idx> <y>", "HNpcPose");
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
        {
            ChatGui.PrintError($"Invalid Y offset '{parts[2]}'. Expected a number like 0.25 or -0.10.", "HNpcPose");
            return;
        }

        ApplyYOffsetByIndex(objectIndex, offsetY, "command");
    }

    private void SaveYOffsetFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 3)
        {
            ChatGui.PrintError("Usage: /hnpcpose saveoffset <idx> <y>", "HNpcPose");
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
        {
            ChatGui.PrintError($"Invalid Y offset '{parts[2]}'. Expected a number like 0.25 or -0.10.", "HNpcPose");
            return;
        }

        SaveYOffsetForObjectIndex(objectIndex, offsetY);
    }

    private void SetNameplatesFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 2)
        {
            ChatGui.Print($"Hide nameplates for posed/saved NPCs is currently {(Configuration.HideNameplatesForPosedNpcs ? "ON" : "OFF")}. Use /hnpcpose nameplates on|off.", "HNpcPose");
            return;
        }

        var value = parts[1].ToLowerInvariant();
        if (value is "on" or "enable" or "enabled" or "true" or "1")
            SetHideNameplatesForPosedNpcs(true);
        else if (value is "off" or "disable" or "disabled" or "false" or "0")
            SetHideNameplatesForPosedNpcs(false);
        else
        {
            ChatGui.PrintError("Usage: /hnpcpose nameplates on|off", "HNpcPose");
            return;
        }

        ChatGui.Print($"Hide nameplates for posed/saved NPCs is now {(Configuration.HideNameplatesForPosedNpcs ? "ON" : "OFF")}.", "HNpcPose");
    }

    private void SetAutoApplyFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 2)
        {
            ChatGui.Print($"Auto-apply saved poses is currently {(Configuration.AutoApplySavedPoses ? "ON" : "OFF")}. Use /hnpcpose auto on|off.", "HNpcPose");
            return;
        }

        var value = parts[1].ToLowerInvariant();
        if (value is "on" or "enable" or "enabled" or "true" or "1")
            Configuration.AutoApplySavedPoses = true;
        else if (value is "off" or "disable" or "disabled" or "false" or "0")
            Configuration.AutoApplySavedPoses = false;
        else
        {
            ChatGui.PrintError("Usage: /hnpcpose auto on|off", "HNpcPose");
            return;
        }

        Configuration.Save();
        ChatGui.Print($"Auto-apply saved poses is now {(Configuration.AutoApplySavedPoses ? "ON" : "OFF")}.", "HNpcPose");
        if (Configuration.AutoApplySavedPoses)
            ScheduleAutoApply("auto enabled");
    }

    private void SavePoseFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 3)
        {
            ChatGui.PrintError("Usage: /hnpcpose save <idx> <poseName|param> [yOffset]", "HNpcPose");
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        var offsetY = 0.0f;
        if (parts.Count >= 4 && !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out offsetY))
        {
            ChatGui.PrintError($"Invalid Y offset '{parts[3]}'. Expected a number like 0.25 or -0.10.", "HNpcPose");
            return;
        }

        if (byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var customParam))
        {
            SavePoseForObjectIndex(objectIndex, PoseCatalogue.GetDisplayName(customParam), customParam, offsetY);
            return;
        }

        if (!TryGetNamedPoseParam(parts[2], out var param, out var label))
        {
            RefuseUnknownNamedPose(parts[2]);
            return;
        }

        SavePoseForObjectIndex(objectIndex, label, param, offsetY);
    }

    private void ClearSavedFromCommand(IReadOnlyList<string> parts)
    {
        if (parts.Count < 2)
        {
            ChatGui.PrintError("Usage: /hnpcpose clearsaved <idx|area>", "HNpcPose");
            return;
        }

        if (parts[1].Equals("area", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("territory", StringComparison.OrdinalIgnoreCase))
        {
            ClearAllSavedPosesForCurrentTerritory();
            return;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return;
        }

        ClearSavedPoseForObjectIndex(objectIndex);
    }

    private static bool TryParseObjectIndex(IReadOnlyList<string> parts, out ushort objectIndex)
    {
        objectIndex = 0;

        if (parts.Count < 2)
        {
            ChatGui.PrintError("Usage: /hnpcpose sit <idx>, /hnpcpose pose <idx> <name>, /hnpcpose pos <idx> <param>, or /hnpcpose loop <idx> <param>", "HNpcPose");
            return false;
        }

        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out objectIndex))
        {
            ChatGui.PrintError($"Invalid object index '{parts[1]}'.", "HNpcPose");
            return false;
        }

        return true;
    }

    private bool ApplyActorMode(ushort objectIndex, CharacterModes mode, byte modeParam, string poseLabel, string source)
    {
        var obj = ObjectTable[objectIndex];
        if (obj == null || !obj.IsValid())
        {
            ChatGui.PrintError($"No valid object found at index {objectIndex}.", "HNpcPose");
            return false;
        }

        return ApplyActorModeToObject(obj, mode, modeParam, poseLabel, source, printResult: true, rescan: true);
    }

    private bool ApplySavedPoseToObject(IGameObject obj, SavedPoseEntry saved, string source, bool printResult, bool rescan)
    {
        var appliedPose = ApplyActorModeToObject(obj, CharacterModes.InPositionLoop, saved.PoseParam, saved.PoseLabel, source, printResult: false, rescan: false);
        if (!appliedPose)
            return false;

        if (Math.Abs(saved.OffsetY) > 0.001f)
            ApplyYOffsetToObject(obj, saved.OffsetY, source, printResult: false, rescan: false);
        else if (appliedYOffsetRecords.ContainsKey(obj.GameObjectId))
            RestoreYOffset(obj, printResult: false);

        if (printResult)
        {
            var offsetText = Math.Abs(saved.OffsetY) > 0.001f ? $" with Y offset {saved.OffsetY:+0.00;-0.00;0.00}" : string.Empty;
            ChatGui.Print($"Applied saved {saved.PoseLabel} ({saved.PoseParam}){offsetText} to {GetObjectLabel(obj)} via {source}.", "HNpcPose");
        }

        if (rescan)
            ScanObjects();

        return true;
    }

    private bool ApplyYOffsetToObject(IGameObject obj, float offsetY, string source, bool printResult, bool rescan)
    {
        if (obj.ObjectKind != ObjectKind.EventNpc)
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to offset {GetObjectLabel(obj)} because it is {obj.ObjectKind}, not EventNpc.", "HNpcPose");
            return false;
        }

        if (TryGetPoseBlockReason(obj, out var blockReason))
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to offset {GetObjectLabel(obj)}. {blockReason}.", "HNpcPose");
            return false;
        }

        if (obj.Address == nint.Zero)
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to offset {GetObjectLabel(obj)} because its address is null.", "HNpcPose");
            return false;
        }

        try
        {
            var clampedOffsetY = ClampYOffset(offsetY);
            var gameObject = (ClientGameObject*)obj.Address;
            if (gameObject == null)
            {
                if (printResult)
                    ChatGui.PrintError($"Refusing to offset {GetObjectLabel(obj)} because GameObject* was null.", "HNpcPose");
                return false;
            }

            gameObject->SetDrawOffset(0.0f, clampedOffsetY, 0.0f);

            if (Math.Abs(clampedOffsetY) > 0.001f)
                appliedYOffsetRecords[obj.GameObjectId] = clampedOffsetY;
            else
                appliedYOffsetRecords.Remove(obj.GameObjectId);

            if (printResult)
                ChatGui.Print($"Applied local visual Y offset {clampedOffsetY:+0.00;-0.00;0.00} to {GetObjectLabel(obj)} via {source}. Use /hnpcpose restore {obj.ObjectIndex} to reset.", "HNpcPose");

            if (Configuration.HideNameplatesForPosedNpcs)
                NamePlateGui.RequestRedraw();

            if (rescan)
                ScanObjects();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to apply Y offset {offsetY} to object index {obj.ObjectIndex}.");
            if (printResult)
                ChatGui.PrintError($"Failed to apply Y offset to {GetObjectLabel(obj)}. Check /xllog.", "HNpcPose");
            return false;
        }
    }

    private bool RestoreYOffset(IGameObject obj, bool printResult)
    {
        if (obj.Address == nint.Zero)
        {
            if (printResult)
                ChatGui.PrintError($"Could not reset Y offset for {GetObjectLabel(obj)}; current object address is null.", "HNpcPose");
            return false;
        }

        try
        {
            var gameObject = (ClientGameObject*)obj.Address;
            if (gameObject == null)
                return false;

            gameObject->SetDrawOffset(0.0f, 0.0f, 0.0f);
            appliedYOffsetRecords.Remove(obj.GameObjectId);
            if (Configuration.HideNameplatesForPosedNpcs)
                NamePlateGui.RequestRedraw();

            if (printResult)
                ChatGui.Print($"Reset local visual Y offset for {GetObjectLabel(obj)}.", "HNpcPose");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to reset Y offset for {GetObjectLabel(obj)}.");
            if (printResult)
                ChatGui.PrintError($"Failed to reset Y offset for {GetObjectLabel(obj)}. Check /xllog.", "HNpcPose");
            return false;
        }
    }

    private static float ClampYOffset(float offsetY)
    {
        return Math.Clamp(offsetY, MinLocalYOffset, MaxLocalYOffset);
    }

    private bool ApplyActorModeToObject(IGameObject obj, CharacterModes mode, byte modeParam, string poseLabel, string source, bool printResult, bool rescan)
    {
        if (obj.ObjectKind != ObjectKind.EventNpc)
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to pose {GetObjectLabel(obj)} because it is {obj.ObjectKind}, not EventNpc.", "HNpcPose");
            return false;
        }

        if (TryGetPoseBlockReason(obj, out var blockReason))
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to pose {GetObjectLabel(obj)}. {blockReason}.", "HNpcPose");
            return false;
        }

        if (obj.Address == nint.Zero)
        {
            if (printResult)
                ChatGui.PrintError($"Refusing to pose {GetObjectLabel(obj)} because its address is null.", "HNpcPose");
            return false;
        }

        try
        {
            var character = (Character*)obj.Address;
            if (character == null)
            {
                if (printResult)
                    ChatGui.PrintError($"Refusing to pose {GetObjectLabel(obj)} because Character* was null.", "HNpcPose");
                return false;
            }

            if (!actorModeSnapshots.ContainsKey(obj.GameObjectId))
            {
                actorModeSnapshots[obj.GameObjectId] = new ActorModeSnapshot(
                    obj.GameObjectId,
                    obj.ObjectIndex,
                    obj.Name.ToString(),
                    character->Mode,
                    character->ModeParam);
            }

            character->SetMode(mode, modeParam);
            appliedPoseRecords[obj.GameObjectId] = new AppliedPoseRecord(poseLabel, mode, modeParam);

            if (printResult)
                ChatGui.Print($"Applied local {poseLabel} ({mode} param {modeParam}) to {GetObjectLabel(obj)} via {source}. Use /hnpcpose restore {obj.ObjectIndex} to undo.", "HNpcPose");

            if (Configuration.HideNameplatesForPosedNpcs)
                NamePlateGui.RequestRedraw();

            if (rescan)
                ScanObjects();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to apply mode {mode} param {modeParam} to object index {obj.ObjectIndex}.");
            if (printResult)
                ChatGui.PrintError($"Failed to apply mode to {GetObjectLabel(obj)}. Check /xllog.", "HNpcPose");
            return false;
        }
    }

    private bool RestoreActorMode(IGameObject obj, ActorModeSnapshot snapshot, bool printResult)
    {
        if (obj.Address == nint.Zero)
        {
            if (printResult)
                ChatGui.PrintError($"Could not restore {snapshot.Name}; current object address is null.", "HNpcPose");
            return false;
        }

        try
        {
            var character = (Character*)obj.Address;
            if (character == null)
                return false;

            character->SetMode(snapshot.Mode, snapshot.ModeParam);
            actorModeSnapshots.Remove(snapshot.GameObjectId);
            appliedPoseRecords.Remove(snapshot.GameObjectId);
            if (Configuration.HideNameplatesForPosedNpcs)
                NamePlateGui.RequestRedraw();

            if (printResult)
                ChatGui.Print($"Restored {snapshot.Name} to {snapshot.Mode} param {snapshot.ModeParam}.", "HNpcPose");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to restore actor mode for {snapshot.Name}.");
            if (printResult)
                ChatGui.PrintError($"Failed to restore {snapshot.Name}. Check /xllog.", "HNpcPose");
            return false;
        }
    }

    private static string GetObjectLabel(IGameObject obj)
    {
        var name = obj.Name.ToString();
        return string.IsNullOrWhiteSpace(name)
            ? $"object {obj.ObjectIndex}"
            : $"{name} [{obj.ObjectIndex}]";
    }

    private static void PrintPoseCatalogue()
    {
        var lines = PoseCatalogue.All
            .OrderBy(definition => definition.Confidence)
            .ThenBy(definition => definition.Category)
            .ThenBy(definition => definition.Param)
            .Select(definition => $"{definition.Param}: {definition.DisplayName} [{definition.Confidence}; {definition.Category}; {definition.CsvText}]");

        ChatGui.Print("Known pose catalogue/crosswalk: " + string.Join(" | ", lines), "HNpcPose");
    }

    private static void PrintHelp()
    {
        ChatGui.Print("HousingNpcPose v0.4.1 commands:", "HNpcPose");
        ChatGui.Print("/hnpcpose - open/close scanner window", "HNpcPose");
        ChatGui.Print("/hnpcpose scan - scan visible NPC candidates", "HNpcPose");
        ChatGui.Print("/hnpcpose clear - clear the current scan results", "HNpcPose");
        ChatGui.Print("/hnpcpose config - open settings", "HNpcPose");
        ChatGui.Print("/hnpcpose sit <idx> - apply confirmed Pos 1 Sit / ground sit", "HNpcPose");
        ChatGui.Print("/hnpcpose test <idx> - alias for /hnpcpose sit <idx>", "HNpcPose");
        ChatGui.Print("/hnpcpose lean <idx> - apply confirmed Pos 55 Lean", "HNpcPose");
        ChatGui.Print("/hnpcpose confirm|scheme|reprimand|sweat|shiver <idx> - apply confirmed useful emote poses", "HNpcPose");
        ChatGui.Print("/hnpcpose pose <idx> <catalogueName|normal> - apply a named catalogue pose", "HNpcPose");
        ChatGui.Print("/hnpcpose pos <idx> <param> - apply CharacterModes.InPositionLoop with param 0-255", "HNpcPose");
        ChatGui.Print("/hnpcpose loop <idx> <param> - apply CharacterModes.EmoteLoop with param 0-255", "HNpcPose");
        ChatGui.Print("/hnpcpose mode <idx> <pos|loop|normal> <param> - pose discovery command", "HNpcPose");
        ChatGui.Print("Advanced UI includes a discovery logger for recording what params 0-255 visibly do.", "HNpcPose");
        ChatGui.Print("/hnpcpose normal <idx> - force CharacterModes.Normal param 0", "HNpcPose");
        ChatGui.Print("/hnpcpose restore <idx|all> - restore original mode snapshot(s)", "HNpcPose");
        ChatGui.Print("/hnpcpose save <idx> <poseName|param> [yOffset] - save a local pose and optional visual Y offset", "HNpcPose");
        ChatGui.Print("/hnpcpose clearsaved <idx|area> - clear saved pose assignment(s)", "HNpcPose");
        ChatGui.Print("/hnpcpose poses - print the current built-in pose catalogue", "HNpcPose");
        ChatGui.Print("/hnpcpose applysaved - apply saved poses for the current area now", "HNpcPose");
        ChatGui.Print("/hnpcpose offset <idx> <y> - apply local visual Y draw offset to a safe NPC", "HNpcPose");
        ChatGui.Print("/hnpcpose saveoffset <idx> <y> - save Y draw offset into an existing saved pose", "HNpcPose");
        ChatGui.Print("/hnpcpose auto on|off - toggle auto-apply saved poses after zoning/plugin load", "HNpcPose");
        ChatGui.Print("/hnpcpose nameplates on|off - hide nameplates for posed/saved NPCs", "HNpcPose");
        ChatGui.Print("Only unblocked EventNpc targets are accepted. Known non-humanoid / creature NPCs are refused. This is local-client experimental mode only.", "HNpcPose");
    }
}

public sealed record ActorModeSnapshot(
    ulong GameObjectId,
    ushort ObjectIndex,
    string Name,
    CharacterModes Mode,
    byte ModeParam);

public sealed record AppliedPoseRecord(
    string Label,
    CharacterModes Mode,
    byte ModeParam)
{
    public string DisplayText => $"{Label} ({Mode} {ModeParam})";
}

public sealed record NpcScanResult(
    ushort ObjectIndex,
    string Name,
    ObjectKind Kind,
    byte SubKind,
    uint BaseId,
    uint EntityId,
    ulong GameObjectId,
    bool IsTargetable,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    nint Address,
    string Note,
    bool CanPose,
    bool HasSavedModeSnapshot,
    string PluginPoseText,
    string SavedPoseText)
{
    public static NpcScanResult FromGameObject(IGameObject obj, string note, bool canPose, bool hasSavedModeSnapshot, string pluginPoseText, string savedPoseText)
    {
        var position = obj.Position;

        return new NpcScanResult(
            obj.ObjectIndex,
            obj.Name.ToString(),
            obj.ObjectKind,
            obj.SubKind,
            obj.BaseId,
            obj.EntityId,
            obj.GameObjectId,
            obj.IsTargetable,
            position.X,
            position.Y,
            position.Z,
            obj.Rotation,
            obj.Address,
            note,
            canPose,
            hasSavedModeSnapshot,
            pluginPoseText,
            savedPoseText);
    }

    public string PositionText =>
        string.Create(CultureInfo.InvariantCulture, $"{PositionX:0.00}, {PositionY:0.00}, {PositionZ:0.00}");

    public string RotationText =>
        Rotation.ToString("0.000", CultureInfo.InvariantCulture);

    public string EntityIdText =>
        $"0x{EntityId:X8}";

    public string GameObjectIdText =>
        $"0x{GameObjectId:X16}";

    public string AddressText =>
        $"0x{Address.ToInt64():X}";
}
