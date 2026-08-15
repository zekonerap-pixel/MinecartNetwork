namespace MinecartNetwork;

public sealed class ModConfig
{
    public const string MenuStyleStardew = "Stardew";
    public const string MenuStyleBasic = "Basic";

    public bool EnableDebugCommands { get; set; } = true;
    public bool PlayWarpSound { get; set; } = true;
    public bool AutoCategorizeNewStations { get; set; } = true;
    public string DefaultCategory { get; set; } = "Other";
    public string MenuStyle { get; set; } = MenuStyleStardew;

    public static string NormalizeMenuStyle(string? value)
    {
        return string.Equals(value, MenuStyleBasic, StringComparison.OrdinalIgnoreCase)
            ? MenuStyleBasic
            : MenuStyleStardew;
    }

    public static bool IsBasicMenuStyle(string? value)
        => NormalizeMenuStyle(value) == MenuStyleBasic;
}
