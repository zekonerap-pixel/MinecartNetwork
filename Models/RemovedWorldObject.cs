namespace MinecartNetwork.Models;

public sealed class RemovedWorldObject
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string QualifiedItemId { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int Quality { get; set; }
    public bool WasSpawnedObject { get; set; }
}
