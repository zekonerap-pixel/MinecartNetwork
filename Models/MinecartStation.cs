namespace MinecartNetwork.Models;

public sealed class MinecartStation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Minecart";
    public string LocationName { get; set; } = "";
    public string Category { get; set; } = "Other";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int FacingDirection { get; set; } = 2;
    public bool HasTracks { get; set; } = true;
    public bool HasWallHole { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long CreatedByPlayerId { get; set; }
}
