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
/// Draw placed stations in the normal world pass. Rails and entrance remain background visuals,
/// while the minecart gets an explicit furniture-style layer depth derived from the bottom of its
/// physical footprint. This lets Stardew's own SpriteBatch sorting decide whether the player is in
/// front of or behind the cart, instead of flipping between separate prefix/postfix redraw passes.
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

        if (drawStationMethod is not null
            && drawPlacementFootprintMethod is not null
            && getMinecartSpriteBoundsMethod is not null
            && visualAssetsField is not null)
        {
            return true;
        }

        monitor.Log(
            "Couldn't access MinecartRenderer furniture-render methods; station sprites will use the RenderedWorld fallback.",
            LogLevel.Warn
        );
        return false;
    }

    public static void Prefix(Farmer __instance, SpriteBatch __0)
    {
        if (!ShouldHandleFarmer(__instance))
            return;

        // Draw the complete station once as the background/world pass. DrawStation still includes
        // the cart, but that copy uses the renderer's background depth. We immediately add a second
        // cart copy at the same position with the correct furniture layer depth; the two are visually
        // identical, and only the depth-sorted copy participates in player front/back ordering.
        DrawPlacedStations(__0);
        DrawFurnitureMinecarts(__0);
    }

    public static void Postfix(Farmer __instance, SpriteBatch __0)
    {
        // Intentionally empty. The cart no longer switches between prefix/postfix passes based on
        // the farmer position; SpriteBatch layer depth handles the relationship continuously.
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

            // Preview rendering happens after the vanilla world, so it deliberately gets a high
            // depth and stays visible while the user moves the cursor over the farmer.
            DrawMinecartSprite(
                e.SpriteBatch,
                tile.X,
                tile.Y,
                placement.StationDirection,
                0.62f,
                !valid,
                0.999f
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

    private static void DrawFurnitureMinecarts(SpriteBatch batch)
    {
        if (stations is null)
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

                DrawMinecartSprite(
                    batch,
                    station.VisualTileX.Value,
                    station.VisualTileY.Value,
                    station.StationDirection,
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

    private static void DrawMinecartSprite(
        SpriteBatch batch,
        int tileX,
        int tileY,
        int direction,
        float alpha,
        bool invalid,
        float layerDepth)
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
            layerDepth
        );
    }

    private static float GetFurnitureLayerDepth(int worldBaseY)
    {
        // Stardew world sprites conventionally derive depth from their world-space base Y.
        // Keep the value inside SpriteBatch's valid 0..1 range for unusually tall modded maps.
        return Math.Clamp((worldBaseY + 1) / 10000f, 0.0001f, 0.999f);
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
