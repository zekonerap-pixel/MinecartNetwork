using System.Text;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace MinecartNetwork.Rendering;

/// <summary>
/// Development helper used to inspect the exact vanilla map tiles around the game's minecart stops.
/// This lets Minecart Network reuse real vanilla sprites without guessing atlas coordinates.
/// </summary>
public sealed class VanillaStationVisualScanner
{
    private const int ScanRadius = 4;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly VanillaMinecartService vanillaMinecarts;

    public VanillaStationVisualScanner(
        IModHelper helper,
        IMonitor monitor,
        VanillaMinecartService vanillaMinecarts)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.vanillaMinecarts = vanillaMinecarts;
    }

    public void Scan()
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save before scanning vanilla minecart visuals.", LogLevel.Warn);
            return;
        }

        IReadOnlyList<Models.VanillaMinecartDestination> destinations =
            this.vanillaMinecarts.GetAvailableDestinations(VanillaMinecartService.DefaultNetworkId);

        if (destinations.Count == 0)
        {
            this.monitor.Log("No unlocked vanilla minecart destinations were found to scan.", LogLevel.Warn);
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("Minecart Network - vanilla visual scan");
        report.AppendLine($"Generated: {DateTime.Now:O}");
        report.AppendLine();

        foreach (Models.VanillaMinecartDestination destination in destinations)
            this.AppendDestination(report, destination);

        string outputPath = Path.Combine(this.helper.DirectoryPath, "vanilla-visual-scan.txt");

        try
        {
            File.WriteAllText(outputPath, report.ToString(), Encoding.UTF8);
            this.monitor.Log(
                $"Vanilla minecart visual scan written to '{outputPath}'.",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            this.monitor.Log(
                $"Couldn't write vanilla visual scan file. Printing the report to the SMAPI log instead. {ex.Message}",
                LogLevel.Warn
            );
            this.monitor.Log(report.ToString(), LogLevel.Info);
        }
    }

    private void AppendDestination(
        StringBuilder report,
        Models.VanillaMinecartDestination destination)
    {
        GameLocation? location = Game1.getLocationFromName(destination.TargetLocation);
        if (location?.Map is null)
        {
            report.AppendLine($"[{destination.Id}] {destination.TargetLocation}: location/map unavailable");
            report.AppendLine();
            return;
        }

        int targetX = destination.TargetTileX;
        int targetY = destination.TargetTileY;

        report.AppendLine($"[{destination.Id}] {destination.Name}");
        report.AppendLine(
            $"Location={destination.TargetLocation} Target={targetX},{targetY} Direction={destination.TargetDirection}"
        );

        int minX = Math.Max(0, targetX - ScanRadius);
        int minY = Math.Max(0, targetY - ScanRadius);

        foreach (Layer layer in location.Map.Layers)
        {
            int maxX = Math.Min(layer.LayerWidth - 1, targetX + ScanRadius);
            int maxY = Math.Min(layer.LayerHeight - 1, targetY + ScanRadius);

            report.AppendLine($"  Layer: {layer.Id}");

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
                    int relativeX = x - targetX;
                    int relativeY = y - targetY;

                    report.Append(
                        $"    ({relativeX,+2},{relativeY,+2}) map={x},{y} sheet={sheetId} image={imageSource} index={tile.TileIndex}"
                    );

                    if (!string.IsNullOrWhiteSpace(properties))
                        report.Append($" props=[{properties}]");

                    report.AppendLine();
                }
            }
        }

        report.AppendLine();
    }
}
