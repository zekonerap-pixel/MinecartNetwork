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

    public static IReadOnlyList<Point> GetCartTiles(int tileX, int tileY, int direction)
    {
        direction = NormalizeDirection(direction);

        return direction is 0 or 2
            ? new[] { new Point(tileX, tileY), new Point(tileX + 1, tileY) }
            : new[] { new Point(tileX, tileY), new Point(tileX, tileY + 1) };
    }

    public static Point GetArrivalTile(int tileX, int tileY, int direction)
    {
        direction = NormalizeDirection(direction);

        return direction switch
        {
            0 => new Point(tileX, tileY - 1),
            1 => new Point(tileX + 1, tileY),
            2 => new Point(tileX, tileY + 1),
            3 => new Point(tileX - 1, tileY),
            _ => new Point(tileX, tileY + 1)
        };
    }

    public static IReadOnlyList<Point> GetTrackTiles(int tileX, int tileY, int direction, int trackLength)
    {
        var result = new List<Point>();
        direction = NormalizeDirection(direction);
        trackLength = Math.Clamp(trackLength, MinTrackLength, MaxTrackLength);

        Point forward = GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);

        for (int segment = 1; segment <= trackLength; segment++)
        {
            int anchorX = tileX + back.X * segment;
            int anchorY = tileY + back.Y * segment;
            result.AddRange(GetCrossSectionTiles(anchorX, anchorY, direction));
        }

        return result;
    }

    public static IReadOnlyList<Point> GetHoleTiles(int tileX, int tileY, int direction, int trackLength)
    {
        direction = NormalizeDirection(direction);
        trackLength = Math.Clamp(trackLength, MinTrackLength, MaxTrackLength);

        Point forward = GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);
        int offset = trackLength + 1;
        int anchorX = tileX + back.X * offset;
        int anchorY = tileY + back.Y * offset;

        return GetCrossSectionTiles(anchorX, anchorY, direction);
    }

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
    {
        direction = NormalizeDirection(direction);

        return direction is 0 or 2
            ? new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize * 2, Game1.tileSize)
            : new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize * 2);
    }

    public static Rectangle GetTilePixelBounds(Point tile)
        => new(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

    private static IReadOnlyList<Point> GetCrossSectionTiles(int anchorX, int anchorY, int direction)
    {
        return direction is 0 or 2
            ? new[] { new Point(anchorX, anchorY), new Point(anchorX + 1, anchorY) }
            : new[] { new Point(anchorX, anchorY), new Point(anchorX, anchorY + 1) };
    }
}
