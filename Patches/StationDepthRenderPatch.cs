using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecartNetwork.Models;
using MinecartNetwork.Rendering;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Patches;

/// <summary>
/// Draw placed stations in the normal world pass, then add the cart again at its
/// furniture-style layer depth so Stardew's own sorting controls player front/back order.
/// </summary>
internal static class StationDepthRenderPatch
{
    private static IMonitor? monitor;
    private static StationManager? stations;
    private static PlacementManager? placement;
    private static MinecartRenderer? renderer;
    private static bool renderErrorLogged;

    public static bool Configure(
        IMonitor modMonitor,
        StationManager stationManager,
        PlacementManager placementManager,
        MinecartRenderer minecartRenderer)
    {
        monitor = modMonitor;
        stations = stationManager;
        placement = placementManager;
        renderer = minecartRenderer;
        return true;
    }

    public static void Prefix(Farmer __instance, SpriteBatch __0)
    {
        if (!ShouldHandleFarmer(__instance))
            return;

        DrawPlacedStations(__0);
        DrawFurnitureMinecarts(__0);
    }

    public static void Postfix(Farmer __instance, SpriteBatch __0)
    {
        // Intentionally empty. SpriteBatch layer depth handles player/cart ordering.
    }

    public static void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady
            || placement is null
            || renderer is null
            || !placement.IsPlacing
            || Game1.activeClickableMenu is not null)
        {
            return;
        }

        try
        {
            renderer.DrawPlacementPreview(e.SpriteBatch);
        }
        catch (Exception ex)
        {
            LogRenderError(ex);
        }
    }

    private static bool ShouldHandleFarmer(Farmer farmer)
    {
        return Context.IsWorldReady
            && stations is not null
            && renderer is not null
            && ReferenceEquals(farmer, Game1.player);
    }

    private static void DrawPlacedStations(SpriteBatch batch)
    {
        if (stations is null || renderer is null)
            return;

        try
        {
            string locationName = Game1.currentLocation.NameOrUniqueName;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!IsVisibleStation(station, locationName))
                    continue;

                renderer.DrawStationForStation(batch, station, 1f, false);
            }
        }
        catch (Exception ex)
        {
            LogRenderError(ex);
        }
    }

    private static void DrawFurnitureMinecarts(SpriteBatch batch)
    {
        if (stations is null || renderer is null)
            return;

        try
        {
            string locationName = Game1.currentLocation.NameOrUniqueName;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!IsVisibleStation(station, locationName))
                    continue;

                Rectangle footprint = StationGeometry.GetCartCollisionBounds(
                    station.VisualTileX!.Value,
                    station.VisualTileY!.Value
                );

                renderer.DrawMinecartForStation(
                    batch,
                    station,
                    1f,
                    false,
                    GetFurnitureLayerDepth(footprint.Bottom)
                );
            }
        }
        catch (Exception ex)
        {
            LogRenderError(ex);
        }
    }

    private static float GetFurnitureLayerDepth(int worldBaseY)
    {
        return Math.Clamp((worldBaseY + 1) / 10000f, 0.0001f, 0.999f);
    }

    private static bool IsVisibleStation(MinecartStation station, string locationName)
    {
        return station.IsEnabled
            && station.HasPhysicalMinecart
            && station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogRenderError(Exception ex)
    {
        if (renderErrorLogged)
            return;

        renderErrorLogged = true;
        monitor?.Log(
            $"Station furniture-style rendering failed; this error will only be logged once. {ex}",
            LogLevel.Error
        );
    }
}
