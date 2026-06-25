using System;
using System.Collections.Generic;
using System.Linq;

namespace HousingNpcPose;

public enum PoseCategory
{
    Core,
    Gesture,
    Dance,
    Experimental,
    Unknown,
}

public enum PoseSafety
{
    Safe,
    NeedsYOffset,
    NeedsFurniture,
    Experimental,
    GlitchyOrRestricted,
}

public sealed record PoseDefinition(
    byte Param,
    string Key,
    string DisplayName,
    PoseCategory Category,
    PoseSafety Safety,
    string[] Aliases,
    ushort? ActionTimelineId = null,
    string? CsvName = null,
    string? Notes = null)
{
    public string ParamText => $"{DisplayName} ({Param})";
}

public static class PoseCatalogue
{
    public static readonly IReadOnlyList<PoseDefinition> All = new List<PoseDefinition>
    {
        new(1,  "sit",          "Sit / ground sit",     PoseCategory.Core,         PoseSafety.Safe,             new[] { "sit", "groundsit", "ground", "floor" }, Notes: "Confirmed local pose param. Good default floor-sit pose."),
        new(2,  "chair",        "Chair / bench sit",    PoseCategory.Core,         PoseSafety.NeedsYOffset,     new[] { "bench", "chair", "chairsit", "sitbench", "seated" }, Notes: "Confirmed local pose param. Often needs Y offset to align with furniture."),
        new(3,  "doze",         "Doze / bed lie",       PoseCategory.Core,         PoseSafety.NeedsFurniture,   new[] { "doze", "bed", "lie", "liedown", "sleep", "sleeping" }, Notes: "Confirmed local pose param. Best when aligned with beds or raised surfaces."),
        new(55, "lean",         "Lean",                 PoseCategory.Core,         PoseSafety.Safe,             new[] { "lean", "leaning" }, Notes: "Confirmed local pose param. Useful for casual room dressing."),

        new(42, "sweat",        "Sweat",                PoseCategory.Gesture,      PoseSafety.Safe,             new[] { "sweat", "sweating" }, Notes: "Confirmed local pose param."),
        new(43, "shiver",       "Shiver",               PoseCategory.Gesture,      PoseSafety.Safe,             new[] { "shiver", "shivering" }, Notes: "Confirmed local pose param."),
        new(47, "confirm",      "Confirm",              PoseCategory.Gesture,      PoseSafety.Safe,             new[] { "confirm", "nod", "approval" }, Notes: "Confirmed local pose param."),
        new(48, "scheme",       "Scheme",               PoseCategory.Gesture,      PoseSafety.Safe,             new[] { "scheme", "scheming" }, Notes: "Confirmed local pose param."),
        new(51, "reprimand",    "Reprimand",            PoseCategory.Gesture,      PoseSafety.Safe,             new[] { "reprimand", "scold", "scolding" }, Notes: "Confirmed local pose param."),

        new(4,  "stepdance",    "Step Dance",           PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "stepdance", "step" }, CsvName: "Step Dance", Notes: "Observed param from the special/dance block."),
        new(5,  "harvestdance", "Harvest Dance",        PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "harvestdance", "harvest" }, CsvName: "Harvest Dance", Notes: "Observed param from the special/dance block."),
        new(6,  "balldance",    "Ball Dance",           PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "balldance", "ball" }, CsvName: "Ball Dance", Notes: "Observed param from the special/dance block."),
        new(7,  "manderville",  "Manderville Dance",    PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "mandervilledance", "manderville" }, CsvName: "Manderville Dance", Notes: "Observed param from the special/dance block."),
        new(10, "thavdance",    "Thavnairian Dance",    PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "thavnairiandance", "thavdance", "thavnairian" }, CsvName: "Thavnairian Dance", Notes: "Observed param from the special/dance block."),
        new(56, "wasshoi",      "Wasshoi / fan dance",  PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "wasshoi", "fan", "fandance" }, 7389, "Wasshoi", "Observed param; CSV/ActionTimeline link notes Wasshoi as 7389."),
        new(61, "lalihop",      "Lali-hop",             PoseCategory.Dance,        PoseSafety.Experimental,     new[] { "lalihop", "lali", "lalihopdance" }, CsvName: "Lali-hop", Notes: "Observed param; useful as a fun/dance entry, not a core furniture pose."),

        new(8,  "bombdance",    "Bomb Dance / unavailable slot", PoseCategory.Experimental, PoseSafety.GlitchyOrRestricted, new[] { "bombdance", "bomb" }, CsvName: "Bomb Dance", Notes: "Observed as missing/unavailable on the test character. Keep in advanced use only."),
        new(9,  "hildipose",    "Hildibrand / Most Gentlemanly pose", PoseCategory.Experimental, PoseSafety.GlitchyOrRestricted, new[] { "hildi", "hildibrand", "gentleman", "mostgentlemanly" }, 1026, "Hildi pose", "Observed as a restricted/glitchy hold on some races. ActionTimeline/animation reference noted as 1026."),
        new(41, "chickendance", "Chicken-style dance",  PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "chickendance", "chicken" }, Notes: "Observed but not fully identified."),
        new(44, "kickingdance", "Kicking dance",        PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "kickingdance", "kickdance" }, Notes: "Observed but not fully identified."),
        new(45, "pointdance",   "Pointing dance",       PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "pointdance", "pointingdance" }, Notes: "Observed but not fully identified."),
        new(46, "armsupdown",   "Arms up/down motion",  PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "armsupdown", "arms" }, Notes: "Observed but not fully identified."),
        new(49, "crazydance",   "Crazy dance",          PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "crazydance" }, Notes: "Observed but not fully identified."),
        new(50, "stepvariant",  "Step dance variant",   PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "stepvariant", "stepdancevariant" }, Notes: "Observed but not fully identified."),
        new(52, "mambo",        "Manderville Mambo-ish", PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "mambo", "mandervillemambo" }, Notes: "Observed but not fully identified."),
        new(53, "omegam",       "Omega M float / sword", PoseCategory.Experimental, PoseSafety.Experimental,     new[] { "omegam", "omega", "omegasword" }, Notes: "Observed as Omega M-like floating sword animation. Advanced only."),
        new(54, "omegaf",       "Omega F float / rod",   PoseCategory.Experimental, PoseSafety.Experimental,     new[] { "omegaf", "omegarod" }, Notes: "Observed as Omega F-like floating rod animation. Advanced only."),
        new(57, "unknowndance57", "Unknown dance 57",    PoseCategory.Unknown,      PoseSafety.Experimental,     new[] { "unknowndance57", "dance57" }, Notes: "Observed as another dance. Not fully identified."),
    };

    public static IEnumerable<PoseDefinition> ByCategory(PoseCategory category)
        => All.Where(definition => definition.Category == category);

    public static bool TryGetByAlias(string alias, out PoseDefinition definition)
    {
        var normalized = NormalizeAlias(alias);
        definition = All.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
            candidate.Aliases.Any(candidateAlias => string.Equals(NormalizeAlias(candidateAlias), normalized, StringComparison.OrdinalIgnoreCase)))!;

        return definition != null;
    }

    public static bool TryGetByParam(byte param, out PoseDefinition definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.Param == param)!;
        return definition != null;
    }

    public static string GetDisplayName(byte param)
    {
        return TryGetByParam(param, out var definition) ? definition.DisplayName : $"Custom Pos {param}";
    }

    public static string GetLabel(byte param, string prefix)
    {
        return TryGetByParam(param, out var definition) ? $"{prefix} {param}: {definition.DisplayName}" : $"{prefix} {param}";
    }

    public static string GetSavedDisplayName(byte param, string storedLabel)
    {
        if (TryGetByParam(param, out var definition))
            return definition.DisplayName;

        return string.IsNullOrWhiteSpace(storedLabel) ? $"Custom Pos {param}" : storedLabel;
    }

    public static string KnownCommandNames()
    {
        return string.Join(", ", All
            .Where(definition => definition.Category is PoseCategory.Core or PoseCategory.Gesture or PoseCategory.Dance)
            .Select(definition => definition.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
    }

    public static string NormalizeAlias(string alias)
    {
        return (alias ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("/", string.Empty);
    }
}
