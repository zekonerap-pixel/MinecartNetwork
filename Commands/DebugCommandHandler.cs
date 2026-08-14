using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Commands;

public sealed class DebugCommandHandler
{
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly TeleportService teleport;
    private readonly ModConfig config;

    public DebugCommandHandler(IMonitor monitor, StationManager stations, TeleportService teleport, ModConfig config)
    {
        this.monitor = monitor;
        this.stations = stations;
        this.teleport = teleport;
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
            case "list":
                this.List();
                break;
            case "goto":
                this.GoTo(args.Skip(1).ToArray());
                break;
            case "remove":
                this.Remove(args.Skip(1).ToArray());
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
        string category = args.Length > 1 ? args[1] : this.config.DefaultCategory;
        MinecartStation station = this.stations.AddAtPlayer(name, category);

        this.monitor.Log(
            $"Created station '{station.Name}' [{station.Id[..8]}] in {station.LocationName} at {station.TileX},{station.TileY} (category: {station.Category}).",
            LogLevel.Info
        );
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
                     .OrderBy(station => station.Category)
                     .ThenBy(station => station.Name)
                     .GroupBy(station => station.Category))
        {
            this.monitor.Log($"[{group.Key}]", LogLevel.Info);
            foreach (MinecartStation station in group)
            {
                this.monitor.Log(
                    $"  {station.Name} | {station.Id[..8]} | {station.LocationName} {station.TileX},{station.TileY}",
                    LogLevel.Info
                );
            }
        }
    }

    private void GoTo(string[] args)
    {
        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn goto <name-or-id>", LogLevel.Info);
            return;
        }

        string target = string.Join(' ', args);
        MinecartStation? station = this.stations.Find(target);
        if (station is null)
        {
            this.monitor.Log($"Station '{target}' was not found.", LogLevel.Warn);
            return;
        }

        if (!this.teleport.TryWarp(station, out string? error))
            this.monitor.Log(error ?? "The warp failed.", LogLevel.Error);
    }

    private void Remove(string[] args)
    {
        if (args.Length == 0)
        {
            this.monitor.Log("Usage: mn remove <name-or-id>", LogLevel.Info);
            return;
        }

        string target = string.Join(' ', args);
        bool removed = this.stations.Remove(target);
        this.monitor.Log(removed ? $"Removed station '{target}'." : $"Station '{target}' was not found.", removed ? LogLevel.Info : LogLevel.Warn);
    }

    private void PrintHelp()
    {
        this.monitor.Log("Minecart Network test commands:", LogLevel.Info);
        this.monitor.Log("  mn addhere <name> [category]  - create a station at your current tile", LogLevel.Info);
        this.monitor.Log("  mn list                       - list saved stations", LogLevel.Info);
        this.monitor.Log("  mn goto <name-or-id>          - warp to a station", LogLevel.Info);
        this.monitor.Log("  mn remove <name-or-id>        - delete a station", LogLevel.Info);
    }
}
