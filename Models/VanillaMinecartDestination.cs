namespace MinecartNetwork.Models;

public sealed class VanillaMinecartDestination
{
    public string NetworkId { get; init; } = "Default";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string TargetLocation { get; init; } = "";
    public int TargetTileX { get; init; }
    public int TargetTileY { get; init; }
    public int TargetDirection { get; init; } = 2;
}
