using Microsoft.Xna.Framework;
using StardewValley;

namespace MinecartNetwork.Models;

public static class StationGeometry
{
    public const int MinTrackLength = 0;
    public const int MaxTrackLength = 8;
    public const int DefaultTrackLength = 2;

    public static int NormalizeDirection(int direction)
        => ((direction % 4) + 4) % 4;

    public static Point GetForwardVector(int direction)
    {
        return NormalizeDirection(direction) switch
        {
            0 => new Point(0, -1),
            1 => new Point(1, 0),
            2 => new Point(0, 1),
            3 => new Point(-1, 0),
            _ => Point.Zero
        };
    }

    /// <summary>
    /// Get the logical minecart footprint. The cart always occupies exactly one tile;
    /// its artwork may overhang that tile slightly without affecting placement/collision.
    /// </summary>
    public static IReadOnlyList<Point> GetCartTiles(int tileX, int tileY, int direction)
        => new[] { new Point(tileX, tileY) };

    /// <summary>Get the only tile which must remain clear: directly in front of the cart.</summary>
    public static Point GetArrivalTile(int tileX, int tileY, int direction)
    {
        Point forward = GetForwardVector(direction);
        return new Point(tileX + forward.X, tileY + forward.Y);
    }

    /// <summary>Get the one-tile-wide rail corridor between tunnel and cart.</summary>
    public static IReadOnlyList<Point> GetTrackTiles(int tileX, int tileY, int direction, int trackLength)
    {
        var result = new List<Point>();
        trackLength = Math.Clamp(trackLength, MinTrackLength, MaxTrackLength);

        Point forward = GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);

        for (int segment = 1; segment <= trackLength; segment++)
        {
            result.Add(new Point(
                tileX + back.X * segment,
                tileY + back.Y * segment
            ));
        }

        return result;
    }

    /// <summary>Get the logical tunnel/portal tile at the back of the rail corridor.</summary>
    public static IReadOnlyList<Point> GetHoleTiles(int tileX, int tileY, int direction, int trackLength)
    {
        trackLength = Math.Clamp(trackLength, MinTrackLength, MaxTrackLength);

        Point forward = GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);
        int offset = trackLength + 1;

        return new[]
        {
            new Point(
                tileX + back.X * offset,
                tileY + back.Y * offset
            )
        };
    }

    /// <summary>
    /// Get the station construction corridor. This is intentionally one tile wide:
    /// tunnel -> N rail tiles -> cart. The arrival tile is excluded and validated separately.
    /// </summary>
    public static IReadOnlyList<Point> GetConstructionTiles(
        int tileX,
        int tileY,
        int direction,
        int trackLength,
        bool hasTracks,
        bool hasWallHole)
    {
        var result = new HashSet<Point>(GetCartTiles(tileX, tileY, direction));

        if (hasTracks)
        {
            foreach (Point tile in GetTrackTiles(tileX, tileY, direction, trackLength))
                result.Add(tile);
        }

        if (hasWallHole)
        {
            int effectiveLength = hasTracks ? trackLength : 0;
            foreach (Point tile in GetHoleTiles(tileX, tileY, direction, effectiveLength))
                result.Add(tile);
        }

        return result.ToList();
    }

    public static Rectangle GetCartPixelBounds(int tileX, int tileY, int direction)
        => new(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize);

    public static Rectangle GetTilePixelBounds(Point tile)
        => new(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
}
