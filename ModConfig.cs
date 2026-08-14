namespace MinecartNetwork;

public sealed class ModConfig
{
    public bool EnableDebugCommands { get; set; } = true;
    public bool PlayWarpSound { get; set; } = true;
    public bool AutoCategorizeNewStations { get; set; } = true;
    public string DefaultCategory { get; set; } = "Other";
}
