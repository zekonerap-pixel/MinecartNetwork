namespace MinecartNetwork;

public sealed class ModConfig
{
    public const string MenuStyleStardew = "Stardew";
    public const string MenuStyleBasic = "Basic";

    // Global/default station artwork. Any safe folder name under assets/styles can be
    // selected; the runtime catalog decides whether that folder is complete and valid.
    public const string StationVisualLegacyCurrent = "LegacyCurrent";
    public const string StationVisualIndustrial = "Industrial";
    public const string StationVisualRustic = "Rustic";
    public const string StationVisualMiner = "Miner";
    public const string StationVisualCopper = "Copper";
    public const string StationVisualDarkIron = "DarkIron";
    public const string StationVisualMoss = "Moss";
    public const string StationVisualCrystal = "Crystal";

    // Per-station visual mode. Existing stations default to the global GMCM styles.
    public const string StationVisualModeDefault = "Default";
    public const string StationVisualModeAutomatic = "Automatic";
    public const string StationVisualModeCustom = "Custom";

    public bool EnableDebugCommands { get; set; } = true;
    public bool PlayWarpSound { get; set; } = true;
    public bool AutoCategorizeNewStations { get; set; } = true;
    public string DefaultCategory { get; set; } = "Other";
    public string MenuStyle { get; set; } = MenuStyleStardew;

    // These three values are the global/default visual set. A station in Default mode
    // reads them directly; a station in Automatic mode only falls back to them when no
    // suitable regional set is available.
    public string MinecartVisualStyle { get; set; } = StationVisualLegacyCurrent;
    public string EntranceVisualStyle { get; set; } = StationVisualLegacyCurrent;
    public string TrackVisualStyle { get; set; } = StationVisualLegacyCurrent;

    public static string NormalizeMenuStyle(string? value)
    {
        return string.Equals(value, MenuStyleBasic, StringComparison.OrdinalIgnoreCase)
            ? MenuStyleBasic
            : MenuStyleStardew;
    }

    public static bool IsBasicMenuStyle(string? value)
        => NormalizeMenuStyle(value) == MenuStyleBasic;

    public static string NormalizeStationVisualStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return StationVisualLegacyCurrent;

        string trimmed = value.Trim();
        if (trimmed.Equals(StationVisualLegacyCurrent, StringComparison.OrdinalIgnoreCase))
            return StationVisualLegacyCurrent;

        return IsSafeStationVisualStyleName(trimmed)
            ? trimmed
            : StationVisualLegacyCurrent;
    }

    public static string NormalizeStationVisualMode(string? value)
    {
        if (value?.Equals(StationVisualModeAutomatic, StringComparison.OrdinalIgnoreCase) == true)
            return StationVisualModeAutomatic;
        if (value?.Equals(StationVisualModeCustom, StringComparison.OrdinalIgnoreCase) == true)
            return StationVisualModeCustom;

        return StationVisualModeDefault;
    }

    public static bool IsSafeStationVisualStyleName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }
}
