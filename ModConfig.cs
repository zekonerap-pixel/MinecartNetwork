namespace MinecartNetwork;

public sealed class ModConfig
{
    public const string MenuStyleStardew = "Stardew";
    public const string MenuStyleBasic = "Basic";

    // Station artwork is intentionally split into three independent choices (option B).
    // For now these stay on LegacyCurrent until real vanilla source tiles are identified;
    // no fake/approximated vanilla options are exposed.
    public const string StationVisualLegacyCurrent = "LegacyCurrent";

    public bool EnableDebugCommands { get; set; } = true;
    public bool PlayWarpSound { get; set; } = true;
    public bool AutoCategorizeNewStations { get; set; } = true;
    public string DefaultCategory { get; set; } = "Other";
    public string MenuStyle { get; set; } = MenuStyleStardew;

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
        => StationVisualLegacyCurrent;
}
