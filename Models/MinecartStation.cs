namespace MinecartNetwork.Models;

public sealed class MinecartStation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Minecart";
    public string LocationName { get; set; } = "";
    public string Category { get; set; } = "Other";
    public bool UseAutomaticCategory { get; set; }

    public int TileX { get; set; }
    public int TileY { get; set; }
    public int FacingDirection { get; set; } = 0;

    public int? VisualTileX { get; set; }
    public int? VisualTileY { get; set; }

    // Direction from the tunnel through the cart toward the arrival tile.
    // 0 = up, 1 = right, 2 = down, 3 = left.
    // Down preserves the geometry used by stations created before alpha.9.
    public int StationDirection { get; set; } = 2;

    // Number of full track sections between the cart and the wall opening.
    public int TrackLength { get; set; }

    public bool HasTracks { get; set; } = true;
    public bool HasWallHole { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long CreatedByPlayerId { get; set; }

    // Visual mode:
    // Default   -> use the three global GMCM styles.
    // Automatic -> choose a complete visual set from the station's region.
    // Custom    -> use the three component styles stored below.
    // This default keeps old saves fully compatible.
    public string VisualStyleMode { get; set; } = "Default";
    public string? MinecartVisualStyle { get; set; }
    public string? EntranceVisualStyle { get; set; }
    public string? TrackVisualStyle { get; set; }

    // Simple world clutter removed by this station and safe to reconstruct later.
    public List<RemovedWorldObject> ClearedObjects { get; set; } = new();

    public bool HasPhysicalMinecart => this.VisualTileX.HasValue && this.VisualTileY.HasValue;
}
