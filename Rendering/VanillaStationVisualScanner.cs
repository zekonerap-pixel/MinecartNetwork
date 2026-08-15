using System.Text;
using StardewModdingAPI;
using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace MinecartNetwork.Rendering;

/// <summary>
/// Development helper used to inspect the exact vanilla map tiles around a minecart stop.
/// Stand next to a vanilla station and run <c>mn visualscan</c> to print the real tilesheet
/// names, source tile indexes, layers, and properties used by the game.
/// </summary>
public sealed class VanillaStationVisualScanner
{
    private const int ScanRadius = 5;

    private readonly IMonitor monitor;

    public VanillaStationVisualScanner(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    public void Scan()
    {
        if (!Context.IsWorldReady || Game1.currentLocation?.Map is null)
        {
            this.monitor.Log("Load a save before scanning vanilla minecart visuals.", LogLevel.Warn);
            return;
        }

        GameLocation location = Game1.currentLocation;
        Point center = Game1.player.TilePoint;
        var report = new StringBuilder();

        report.AppendLine("Minecart Network - vanilla visual scan");
        report.AppendLine($"Location={location.NameOrUniqueName} PlayerTile={center.X},{center.Y}");
        report.AppendLine($"Radius={ScanRadius}");

        int minX = Math.Max(0, center.X - ScanRadius);
        int minY = Math.Max(0, center.Y - ScanRadius);

        foreach (Layer layer in location.Map.Layers)
        {
            int maxX = Math.Min(layer.LayerWidth - 1, center.X + ScanRadius);
            int maxY = Math.Min(layer.LayerHeight - 1, center.Y + ScanRadius);

            report.AppendLine($"Layer={layer.Id}");

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Tile? tile = layer.Tiles[x, y];
                    if (tile is null)
                        continue;

                    string properties = tile.Properties.Count == 0
                        ? ""
                        : string.Join(
                            "; ",
                            tile.Properties.Select(pair => $"{pair.Key}={pair.Value}")
                        );

                    string imageSource = tile.TileSheet?.ImageSource ?? "?";
                    string sheetId = tile.TileSheet?.Id ?? "?";
                    int relativeX = x - center.X;
                    int relativeY = y - center.Y;

                    report.Append(
                        $"  rel=({relativeX,+2},{relativeY,+2}) map={x},{y} sheet={sheetId} image={imageSource} index={tile.TileIndex}"
                    );

                    if (!string.IsNullOrWhiteSpace(properties))
                        report.Append($" props=[{properties}]");

                    report.AppendLine();
                }
            }
        }

        this.monitor.Log(report.ToString(), LogLevel.Info);
        this.monitor.Log(
            "Vanilla visual scan complete. Upload/share the SMAPI log so the cart, entrance, and rail tiles can be mapped exactly.",
            LogLevel.Info
        );
    }
}
