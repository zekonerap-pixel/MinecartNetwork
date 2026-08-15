namespace MinecartNetwork.Rendering;

internal static class StationVisualSettings
{
    public static string MinecartStyle { get; private set; } = ModConfig.StationVisualLegacyCurrent;
    public static string EntranceStyle { get; private set; } = ModConfig.StationVisualLegacyCurrent;
    public static string TrackStyle { get; private set; } = ModConfig.StationVisualLegacyCurrent;

    public static void Apply(ModConfig config)
    {
        SetMinecartStyle(config.MinecartVisualStyle);
        SetEntranceStyle(config.EntranceVisualStyle);
        SetTrackStyle(config.TrackVisualStyle);
    }

    public static void SetMinecartStyle(string? value)
        => MinecartStyle = ModConfig.NormalizeStationVisualStyle(value);

    public static void SetEntranceStyle(string? value)
        => EntranceStyle = ModConfig.NormalizeStationVisualStyle(value);

    public static void SetTrackStyle(string? value)
        => TrackStyle = ModConfig.NormalizeStationVisualStyle(value);
}
