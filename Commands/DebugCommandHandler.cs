using MinecartNetwork.Models;
using MinecartNetwork.Rendering;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Commands;

public sealed class DebugCommandHandler
{
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly LocationRegionService regions;
    private readonly TeleportService teleport;
    private readonly PlacementManager placement;
    private readonly ModConfig config;

    public DebugCommandHandler(
        IMonitor monitor,
        StationManager stations,
        LocationRegionService regions,
        TeleportService teleport,
        PlacementManager placement,
        ModConfig config)
    {
        this.monitor = monitor;
        this.stations = stations;
        this.regions = regions;
        this.teleport = teleport;
        this.placement = placement;
        this.config = config;
    }

    public void Handle(string command, string[] args)
    {
        if (!this.config.EnableDebugCommands)
        {
            this.monitor.Log("Minecart Network debug commands are disabled in config.json.", LogLevel.Warn);
            return;
        }

        if (args.Length == 0)
        {
            this.PrintHelp();
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "addhere":
                this.AddHere(args.Skip(1).ToArray());
                break;
            case "place":
                this.Place(args.Skip(1).ToArray());
                break;
            case "list":
                this.List();
                break;
            case "goto":
                this.GoTo(args.Skip(1).ToArray());
                break;
            case "remove":
                this.Remove(args.Skip(1).ToArray());
                break;
            case "visualscan":
                new VanillaStationVisualScanner(this.monitor).Scan();
                break;
            default:
                this.PrintHelp();
                break;
        }
    }

    private void AddHere(string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save first.", LogLevel.Warn);
            return;
        }

        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn addhere <name> [category]", LogLevel.Info);
            return;
        }

        string name = args[0];
        string? manualCategory = args.Length > 1 ? string.Join(' ', args.Skip(1)) : null;
        bool automatic = string.IsNullOrWhiteSpace(manualCategory) && this.config.AutoCategorizeNewStations;
        string category = automatic
            ? this.regions.GetCategoryForLocation(Game1.currentLocation.NameOrUniqueName)
            : string.IsNullOrWhiteSpace(manualCategory) ? this.config.DefaultCategory : manualCategory.Trim();

        MinecartStation station = this.stations.AddAtPlayer(
            name,
            category,
            useAutomaticCategory: automatic
        );

        this.monitor.Log(
            $"Created station '{station.Name}' [{station.Id[..8]}] in {station.LocationName} at {station.TileX},{station.TileY} (category: {this.regions.GetStationCategory(station)}{(station.UseAutomaticCategory ? ", auto" : "")}).",
            LogLevel.Info
        );
    }

    private void Place(string[] args)
    {
        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn place <name> [category]", LogLevel.Info);
            return;
        }

        string name = args[0];
        string? category = args.Length > 1 ? string.Join(' ', args.Skip(1)) : null;
        this.placement.Begin(name, category);
    }

    private void List()
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save first.", LogLevel.Warn);
            return;
        }

        if (this.stations.Stations.Count == 0)
        {
            this.monitor.Log("There are no custom minecart stations yet.", LogLevel.Info);
            return;
        }

        foreach (IGrouping<string, MinecartStation> group in this.stations.Stations
                     .OrderBy(station => this.regions.GetStationCategory(station))
                     .ThenBy(station => station.Name)
                     .GroupBy(station => this.regions.GetStationCategory(station)))
        {
            this.monitor.Log($"[{group.Key}]", LogLevel.Info);
            foreach (MinecartStation station in group)
            {
                string physical = station.HasPhysicalMinecart
                    ? $" | cart {station.VisualTileX},{station.VisualTileY} | dir {this.GetDirectionName(station.StationDirection)} | track length {station.TrackLength} | cleared {station.ClearedObjects.Count}"
                    : " | no physical cart";
                string mode = station.UseAutomaticCategory ? " | auto category" : "";

                this.monitor.Log(
                    $"  {station.Name} | {station.Id[..8]} | {station.LocationName} arrival {station.TileX},{station.TileY}{physical}{mode}",
                    LogLevel.Info
                );
            }
        }
    }

    private void GoTo(string[] args)
    {
        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn goto <name-or-id> OR mn goto <category> <name>", LogLevel.Info);
            return;
        }

        string target = string.Join(' ', args);
        IReadOnlyList<MinecartStation> matches = this.stations.FindMatches(target);

        if (matches.Count == 0)
        {
            this.monitor.Log($"Station '{target}' was not found.", LogLevel.Warn);
            return;
        }

        if (matches.Count > 1)
        {
            this.monitor.Log($"Station '{target}' is ambiguous. Use category + name or an ID prefix:", LogLevel.Warn);
            foreach (MinecartStation match in matches)
                this.monitor.Log($"  {this.regions.GetStationCategory(match)} -> {match.Name} [{match.Id[..8]}]", LogLevel.Info);
            return;
        }

        if (!this.teleport.TryWarp(matches[0], out string? error))
            this.monitor.Log(error ?? "The warp failed.", LogLevel.Error);
    }

    private void Remove(string[] args)
    {
        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn remove <name-or-id> OR mn remove <category> <name>", LogLevel.Info);
            return;
        }

        string target = string.Join(' ', args);
        IReadOnlyList<MinecartStation> matches = this.stations.FindMatches(target);

        if (matches.Count == 0)
        {
            this.monitor.Log($"Station '{target}' was not found.", LogLevel.Warn);
            return;
        }

        if (matches.Count > 1)
        {
            this.monitor.Log($"Station '{target}' is ambiguous. Use category + name or an ID prefix:", LogLevel.Warn);
            foreach (MinecartStation match in matches)
                this.monitor.Log($"  {this.regions.GetStationCategory(match)} -> {match.Name} [{match.Id[..8]}]", LogLevel.Info);
            return;
        }

        MinecartStation station = matches[0];
        bool removed = this.stations.Remove(station.Id);
        this.monitor.Log(
            removed
                ? $"Removed station '{station.Name}'."
                : $"Station '{station.Name}' couldn't be removed safely; check the previous warning for environment restoration details.",
            removed ? LogLevel.Info : LogLevel.Warn
        );
    }

    private string GetDirectionName(int direction)
    {
        return StationGeometry.NormalizeDirection(direction) switch
        {
            0 => "up",
            1 => "right",
            2 => "down",
            3 => "left",
            _ => "down"
        };
    }

    private void PrintHelp()
    {
        this.monitor.Log("Minecart Network test commands:", LogLevel.Info);
        this.monitor.Log("  mn addhere <name> [category]   - create a non-physical station; category is automatic when omitted", LogLevel.Info);
        this.monitor.Log("  mn place <name> [category]     - place a physical minecart; category is automatic when omitted", LogLevel.Info);
        this.monitor.Log("  mn list                        - list saved stations, geometry, and cleared environment", LogLevel.Info);
        this.monitor.Log("  mn goto <name-or-id>           - warp to a station", LogLevel.Info);
        this.monitor.Log("  mn goto <category> <name>      - warp using category + name", LogLevel.Info);
        this.monitor.Log("  mn remove <name-or-id>         - delete a station and restore reversible cleared objects", LogLevel.Info);
        this.monitor.Log("  mn visualscan                  - inspect the real vanilla tiles around the current minecart stop", LogLevel.Info);
    }
}
