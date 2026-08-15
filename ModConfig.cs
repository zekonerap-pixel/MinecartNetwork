namespace MinecartNetwork;

public sealed class ModConfig
{
    public const string MenuStyleStardew = "Stardew";
    public const string MenuStyleBasic = "Basic";

    // Station artwork is split into three independent choices (option B).
    // All generated variants keep the exact source atlas dimensions.
    public const string StationVisualLegacyCurrent = "LegacyCurrent";
    public const string StationVisualRustic = "Rustic";
    public const string StationVisualCopper = "Copper";
    public const string StationVisualDarkIron = "DarkIron";
    public const string StationVisualMoss = "Moss";
    public const string StationVisualCrystal = "Crystal";

    public static readonly string[] StationVisualStyles =
    {
        StationVisualLegacyCurrent,
        StationVisualRustic,
        StationVisualCopper,
        StationVisualDarkIron,
        StationVisualMoss,
        StationVisualCrystal
    };

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
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (string style in StationVisualStyles)
            {
                if (style.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return style;
            }
        }

        return StationVisualLegacyCurrent;
    }
}
