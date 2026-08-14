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

    /// <summary>The MinecartNetwork station ID when this native destination is one of our mirrored stations.</summary>
    public string? CustomStationId { get; init; }

    public bool IsCustomStation => !string.IsNullOrWhiteSpace(this.CustomStationId);
}
