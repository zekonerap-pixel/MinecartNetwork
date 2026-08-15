using System.Reflection;
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
/// Draw placed custom stations immediately before or after the local farmer based on their
/// world-space base Y. This mimics Stardew's building-style front/back relationship instead of
/// drawing every station as a late RenderedWorld overlay on top of the player.
/// </summary>
internal static class StationDepthRenderPatch
{
    private static IMonitor? monitor;
    private static StationManager? stations;
    private static PlacementManager? placement;
    private static MinecartRenderer? renderer;
    private static MethodInfo? drawStationMethod;
    private static MethodInfo? drawPlacementFootprintMethod;
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

        drawStationMethod = typeof(MinecartRenderer).GetMethod(
            "DrawStation",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        drawPlacementFootprintMethod = typeof(MinecartRenderer).GetMethod(
            "DrawPlacementFootprint",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (drawStationMethod is not null && drawPlacementFootprintMethod is not null)
            return true;

        monitor.Log(
            "Couldn't access MinecartRenderer depth-render methods; station sprites will use the RenderedWorld fallback.",
            LogLevel.Warn
        );
        return false;
    }

    public static void Prefix(Farmer __instance, SpriteBatch __0)
    {
        if (!ShouldHandleFarmer(__instance))
            return;

        DrawPlacedStations(__0, __instance, drawAfterFarmer: false);
    }

    public static void Postfix(Farmer __instance, SpriteBatch __0)
    {
        if (!ShouldHandleFarmer(__instance))
            return;

        DrawPlacedStations(__0, __instance, drawAfterFarmer: true);
    }

    public static void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady
            || placement is null
            || renderer is null
            || drawStationMethod is null
            || drawPlacementFootprintMethod is null
            || !placement.IsPlacing
            || Game1.activeClickableMenu is not null)
        {
            return;
        }

        try
        {
            Point tile = placement.GetPreviewTile();
            bool valid = placement.CanPlaceAt(
                Game1.currentLocation,
                tile.X,
                tile.Y,
                out _
            );

            drawPlacementFootprintMethod.Invoke(
                renderer,
                new object[]
                {
                    e.SpriteBatch,
                    tile.X,
                    tile.Y,
                    valid
                }
            );

            DrawStation(
                e.SpriteBatch,
                tile.X,
                tile.Y,
                placement.StationDirection,
                placement.TrackLength,
                placement.HasTracks,
                placement.HasWallHole,
                0.62f,
                !valid
            );
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
            && drawStationMethod is not null
            && ReferenceEquals(farmer, Game1.player);
    }

    private static void DrawPlacedStations(
        SpriteBatch batch,
        Farmer farmer,
        bool drawAfterFarmer)
    {
        if (stations is null || drawStationMethod is null)
            return;

        try
        {
            string locationName = Game1.currentLocation.NameOrUniqueName;
            int farmerDepth = farmer.GetBoundingBox().Bottom;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!station.IsEnabled
                    || !station.HasPhysicalMinecart
                    || !station.LocationName.Equals(
                        locationName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int stationDepth = StationGeometry.GetCartCollisionBounds(
                    station.VisualTileX!.Value,
                    station.VisualTileY!.Value
                ).Bottom;

                bool stationShouldDrawAfterFarmer = stationDepth > farmerDepth;
                if (stationShouldDrawAfterFarmer != drawAfterFarmer)
                    continue;

                DrawStation(
                    batch,
                    station.VisualTileX.Value,
                    station.VisualTileY.Value,
                    station.StationDirection,
                    station.TrackLength,
                    station.HasTracks,
                    station.HasWallHole,
                    1f,
                    false
                );
            }
        }
        catch (Exception ex)
        {
            LogRenderError(ex);
        }
    }

    private static void DrawStation(
        SpriteBatch batch,
        int tileX,
        int tileY,
        int direction,
        int trackLength,
        bool hasTracks,
        bool hasWallHole,
        float alpha,
        bool invalid)
    {
        if (renderer is null || drawStationMethod is null)
            return;

        drawStationMethod.Invoke(
            renderer,
            new object[]
            {
                batch,
                tileX,
                tileY,
                direction,
                trackLength,
                hasTracks,
                hasWallHole,
                alpha,
                invalid
            }
        );
    }

    private static void LogRenderError(Exception ex)
    {
        if (renderErrorLogged)
            return;

        renderErrorLogged = true;
        monitor?.Log(
            $"Building-style station depth rendering failed; this error will only be logged once. {ex}",
            LogLevel.Error
        );
    }
}
