using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class TeleportService
{
    private readonly IMonitor monitor;
    private readonly ModConfig config;

    public TeleportService(IMonitor monitor, ModConfig config)
    {
        this.monitor = monitor;
        this.config = config;
    }

    public bool TryWarp(MinecartStation station, out string? error)
    {
        error = null;

        if (!Context.IsWorldReady)
        {
            error = "No save is currently loaded.";
            return false;
        }

        if (!station.IsEnabled)
        {
            error = $"Station '{station.Name}' is disabled.";
            return false;
        }

        GameLocation? destination = Game1.getLocationFromName(station.LocationName);
        if (destination is null)
        {
            error = $"Location '{station.LocationName}' no longer exists.";
            return false;
        }

        try
        {
            if (this.config.PlayWarpSound)
                Game1.currentLocation.playSound("dwarvishSentry");

            Game1.warpFarmer(station.LocationName, station.TileX, station.TileY, station.FacingDirection);
            this.monitor.Log($"Warped to station '{station.Name}' ({station.LocationName} {station.TileX},{station.TileY}).", LogLevel.Trace);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            this.monitor.Log($"Failed to warp to station '{station.Name}': {ex}", LogLevel.Error);
            return false;
        }
    }
}
