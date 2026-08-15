using Microsoft.Xna.Framework;
using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Patches;

internal static class StationCollisionPatch
{
    private static IMonitor? monitor;
    private static StationManager? stations;

    public static void Configure(IMonitor modMonitor, StationManager stationManager)
    {
        monitor = modMonitor;
        stations = stationManager;
    }

    public static void Postfix(
        GameLocation __instance,
        Rectangle position,
        bool isFarmer,
        ref bool __result)
    {
        if (__result || !isFarmer || stations is null || !Context.IsWorldReady)
            return;

        try
        {
            string locationName = __instance.NameOrUniqueName;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!station.IsEnabled
                    || !station.HasPhysicalMinecart
                    || !station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase))
                    continue;

                IReadOnlyList<Rectangle> collisionBounds = StationGeometry.GetCollisionBounds(
                    station.VisualTileX!.Value,
                    station.VisualTileY!.Value,
                    station.StationDirection,
                    station.TrackLength,
                    station.HasTracks,
                    station.HasWallHole
                );

                if (!collisionBounds.Any(bounds => bounds.Intersects(position)))
                    continue;

                __result = true;
                return;
            }
        }
        catch (Exception ex)
        {
            // Collision code must never break normal Stardew movement.
            monitor?.Log(
                $"Failed checking Minecart Network station collision; preserving the game's original result. {ex}",
                LogLevel.Error
            );
        }
    }
}
