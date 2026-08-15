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
/// Render placed stations around the local farmer. Entrance and rails stay in the background,
/// while the minecart uses its one-tile collision footprint as a furniture-style depth boundary:
/// the player covers it when standing in front, and the cart covers the player when standing behind.
/// </summary>
internal static class StationDepthRenderPatch
{
    private static IMonitor? monitor;
    private static StationManager? stations;
    private static PlacementManager? placement;
    private static MinecartRenderer? renderer;
    private static MethodInfo? drawStationMethod;
    private static MethodInfo? drawPlacementFootprintMethod;
    private static MethodInfo? getMinecartSpriteBoundsMethod;
    private static FieldInfo? visualAssetsField;
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
        getMinecartSpriteBoundsMethod = typeof(MinecartRenderer).GetMethod(
            "GetMinecartSpriteBounds",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        visualAssetsField = typeof(MinecartRenderer).GetField(
            "visualAssets",
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

        // Draw the complete station first. This keeps rails and the wall entrance behind the farmer,
        // and also provides the cart's background pass when the farmer is standing in front of it.
        DrawPlacedStations(__0);
    }

    public static void Postfix(Farmer __instance, SpriteBatch __0)
    {
        if (!ShouldHandleFarmer(__instance))
            return;

        // Furniture-style second pass: only redraw carts whose one-tile footprint is in front of
        // the farmer. Passing false/false renders just the cart, with no duplicate rails/entrance.
        DrawForegroundMinecarts(__0, __instance);
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

            // Placement is only a ghost preview, so furniture collision/depth rules do not apply yet.
            // Redraw just the cart with maximum layer depth so it remains visible even when the cursor
            // is directly over the farmer. Once placed, the normal furniture-style passes take over.
            DrawPlacementMinecartOverlay(
                e.SpriteBatch,
                tile.X,
                tile.Y,
                placement.StationDirection,
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

    private static void DrawPlacedStations(SpriteBatch batch)
    {
        if (stations is null || drawStationMethod is null)
            return;

        try
        {
            string locationName = Game1.currentLocation.NameOrUniqueName;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!IsVisibleStation(station, locationName))
                    continue;

                DrawStation(
                    batch,
                    station.VisualTileX!.Value,
                    station.VisualTileY!.Value,
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

    private static void DrawForegroundMinecarts(SpriteBatch batch, Farmer farmer)
    {
        if (stations is null || drawStationMethod is null)
            return;

        try
        {
            string locationName = Game1.currentLocation.NameOrUniqueName;
            int farmerDepth = farmer.GetBoundingBox().Bottom;

            foreach (MinecartStation station in stations.Stations)
            {
                if (!IsVisibleStation(station, locationName))
                    continue;

                Rectangle furnitureBounds = StationGeometry.GetCartCollisionBounds(
                    station.VisualTileX!.Value,
                    station.VisualTileY!.Value
                );

                // Same visual rule as a normal floor furniture piece: its base controls depth.
                // If the farmer's feet are above that base, the farmer is behind the cart.
                if (farmerDepth >= furnitureBounds.Bottom)
                    continue;

                DrawStation(
                    batch,
                    station.VisualTileX.Value,
                    station.VisualTileY.Value,
                    station.StationDirection,
                    station.TrackLength,
                    hasTracks: false,
                    hasWallHole: false,
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

    private static void DrawPlacementMinecartOverlay(
        SpriteBatch batch,
        int tileX,
        int tileY,
        int direction,
        float alpha,
        bool invalid)
    {
        if (renderer is null
            || getMinecartSpriteBoundsMethod is null
            || visualAssetsField is null)
        {
            return;
        }

        MinecartVisualAssets? assets = visualAssetsField.GetValue(renderer) as MinecartVisualAssets;
        Texture2D? texture = assets?.Minecart;
        if (texture is null)
            return;

        direction = StationGeometry.NormalizeDirection(direction);
        Rectangle world = StationGeometry.GetCartPixelBounds(tileX, tileY, direction);
        Vector2 screenOrigin = Game1.GlobalToLocal(
            Game1.viewport,
            new Vector2(world.X, world.Y)
        );
        Rectangle logicalScreenBounds = new(
            (int)screenOrigin.X,
            (int)screenOrigin.Y,
            world.Width,
            world.Height
        );

        object? boundsResult = getMinecartSpriteBoundsMethod.Invoke(
            renderer,
            new object[] { logicalScreenBounds, direction }
        );
        if (boundsResult is not Rectangle destination)
            return;

        Color tint = (invalid ? new Color(255, 105, 105) : Color.White) * alpha;

        batch.Draw(
            texture,
            destination,
            assets!.GetMinecartSourceRect(direction),
            tint,
            0f,
            Vector2.Zero,
            SpriteEffects.None,
            1f
        );
    }

    private static bool IsVisibleStation(MinecartStation station, string locationName)
    {
        return station.IsEnabled
            && station.HasPhysicalMinecart
            && station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase);
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
            $"Station furniture-style rendering failed; this error will only be logged once. {ex}",
            LogLevel.Error
        );
    }
}
