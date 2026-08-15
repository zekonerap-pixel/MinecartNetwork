using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Rendering;

public sealed class MinecartRenderer
{
    private const int MinecartWorldSize = 128;
    private const int EntranceWorldSize = 192;
    private const int TrackGroundOffsetY = 8;
    private const int TrackEntranceOverlap = 32;

    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly PlacementManager placement;
    private readonly MinecartVisualAssets visualAssets;
    private readonly StationVisualStyleResolver styleResolver;

    public MinecartRenderer(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        LocationRegionService regions,
        PlacementManager placement,
        ModConfig config)
    {
        this.helper = helper;
        this.stations = stations;
        this.placement = placement;
        this.visualAssets = new MinecartVisualAssets(helper, monitor);
        this.styleResolver = new StationVisualStyleResolver(helper, regions, config);
    }

    public void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        string locationName = Game1.currentLocation.NameOrUniqueName;

        foreach (MinecartStation station in this.stations.Stations)
        {
            if (!station.IsEnabled
                || !station.HasPhysicalMinecart
                || !station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase))
                continue;

            this.DrawStationForStation(e.SpriteBatch, station, 1f, false);
        }

        if (!this.placement.IsPlacing || Game1.activeClickableMenu is not null)
            return;

        Point tile = this.placement.GetPreviewTile();
        bool valid = this.placement.CanPlaceAt(Game1.currentLocation, tile.X, tile.Y, out _);

        this.DrawPlacementFootprint(e.SpriteBatch, tile.X, tile.Y, valid);
        this.DrawStation(
            e.SpriteBatch,
            tile.X,
            tile.Y,
            this.placement.StationDirection,
            this.placement.TrackLength,
            this.placement.HasTracks,
            this.placement.HasWallHole,
            0.62f,
            !valid
        );
    }

    public void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!this.placement.IsPlacing || !Context.IsWorldReady || Game1.activeClickableMenu is not null)
            return;

        Point tile = this.placement.GetPreviewTile();
        bool valid = this.placement.CanPlaceAt(Game1.currentLocation, tile.X, tile.Y, out string reason);

        string status = valid
            ? this.helper.Translation.Get("placement.valid")
            : this.helper.Translation.Get("placement.invalid", new { reason });

        string titleKey = this.placement.IsMoving ? "placement.move-title" : "placement.title";
        string controlsKey = this.placement.IsMoving ? "placement.move-controls" : "placement.controls";

        string text = string.Join(
            Environment.NewLine,
            this.helper.Translation.Get(titleKey, new
            {
                name = this.placement.PendingName,
                category = this.placement.PendingCategory
            }),
            this.helper.Translation.Get(controlsKey),
            this.helper.Translation.Get("placement.options", new
            {
                tracks = this.placement.HasTracks ? this.helper.Translation.Get("common.on") : this.helper.Translation.Get("common.off"),
                hole = this.placement.HasWallHole ? this.helper.Translation.Get("common.on") : this.helper.Translation.Get("common.off")
            }),
            this.helper.Translation.Get("placement.geometry", new
            {
                direction = this.GetDirectionLabel(this.placement.StationDirection),
                length = this.placement.TrackLength
            }),
            status
        );

        Vector2 size = Game1.smallFont.MeasureString(text);
        Rectangle panel = new(16, 16, (int)size.X + 28, (int)size.Y + 24);

        e.SpriteBatch.Draw(Game1.staminaRect, panel, Color.Black * 0.72f);
        e.SpriteBatch.DrawString(Game1.smallFont, text, new Vector2(30, 28), Color.White);
    }

    internal void DrawStationForStation(
        SpriteBatch batch,
        MinecartStation station,
        float alpha,
        bool invalid)
    {
        if (!station.HasPhysicalMinecart)
            return;

        ResolvedStationVisualStyles visuals = this.styleResolver.Resolve(station);
        this.DrawStationCore(
            batch,
            station.VisualTileX!.Value,
            station.VisualTileY!.Value,
            station.StationDirection,
            station.TrackLength,
            station.HasTracks,
            station.HasWallHole,
            alpha,
            invalid,
            visuals
        );
    }

    internal void DrawMinecartForStation(
        SpriteBatch batch,
        MinecartStation station,
        float alpha,
        bool invalid,
        float layerDepth)
    {
        if (!station.HasPhysicalMinecart)
            return;

        ResolvedStationVisualStyles visuals = this.styleResolver.Resolve(station);
        this.DrawMinecartSprite(
            batch,
            station.VisualTileX!.Value,
            station.VisualTileY!.Value,
            station.StationDirection,
            visuals.MinecartStyle,
            alpha,
            invalid,
            layerDepth
        );
    }

    internal void DrawPlacementPreview(SpriteBatch batch)
    {
        if (!this.placement.IsPlacing || !Context.IsWorldReady || Game1.activeClickableMenu is not null)
            return;

        Point tile = this.placement.GetPreviewTile();
        bool valid = this.placement.CanPlaceAt(Game1.currentLocation, tile.X, tile.Y, out _);

        this.DrawPlacementFootprint(batch, tile.X, tile.Y, valid);
        this.DrawStation(
            batch,
            tile.X,
            tile.Y,
            this.placement.StationDirection,
            this.placement.TrackLength,
            this.placement.HasTracks,
            this.placement.HasWallHole,
            0.62f,
            !valid
        );

        ResolvedStationVisualStyles visuals = this.styleResolver.Resolve(null);
        this.DrawMinecartSprite(
            batch,
            tile.X,
            tile.Y,
            this.placement.StationDirection,
            visuals.MinecartStyle,
            0.62f,
            !valid,
            0.999f
        );
    }

    private void DrawStation(
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
        this.DrawStationCore(
            batch,
            tileX,
            tileY,
            direction,
            trackLength,
            hasTracks,
            hasWallHole,
            alpha,
            invalid,
            this.styleResolver.Resolve(null)
        );
    }

    private void DrawStationCore(
        SpriteBatch batch,
        int tileX,
        int tileY,
        int direction,
        int trackLength,
        bool hasTracks,
        bool hasWallHole,
        float alpha,
        bool invalid,
        ResolvedStationVisualStyles visuals)
    {
        direction = StationGeometry.NormalizeDirection(direction);
        trackLength = Math.Clamp(trackLength, StationGeometry.MinTrackLength, StationGeometry.MaxTrackLength);
        Color tint = (invalid ? new Color(255, 105, 105) : Color.White) * alpha;

        if (hasWallHole)
        {
            int effectiveLength = hasTracks ? trackLength : 0;
            IReadOnlyList<Point> holeTiles = StationGeometry.GetHoleTiles(tileX, tileY, direction, effectiveLength);
            Rectangle logicalHole = this.GetScreenBounds(holeTiles);
            Rectangle entranceBounds = this.GetEntranceSpriteBounds(logicalHole, direction);
            Texture2D? entrance = this.visualAssets.GetWallHole(visuals.EntranceStyle);

            if (entrance is not null)
            {
                this.DrawTextureRegion(
                    batch,
                    entrance,
                    this.visualAssets.GetEntranceSourceRect(direction),
                    entranceBounds,
                    tint
                );
            }
            else
            {
                this.DrawFallbackEntrance(batch, entranceBounds, direction, alpha, invalid);
            }
        }

        if (hasTracks)
        {
            Texture2D? tracks = this.visualAssets.GetTracks(visuals.TrackStyle);
            Rectangle source = this.visualAssets.GetTrackSourceRect(direction);

            for (int segment = trackLength; segment >= 1; segment--)
            {
                Point segmentTile = this.GetTrackSegmentTile(tileX, tileY, direction, segment);
                Rectangle logicalTrack = this.WorldToScreen(StationGeometry.GetTilePixelBounds(segmentTile));

                this.DrawTrackVisual(
                    batch,
                    tracks,
                    source,
                    logicalTrack,
                    direction,
                    extendIntoEntrance: hasWallHole && segment == trackLength,
                    tint,
                    alpha,
                    invalid
                );
            }

            Rectangle logicalCartTrack = this.WorldToScreen(
                StationGeometry.GetCartPixelBounds(tileX, tileY, direction)
            );

            this.DrawTrackVisual(
                batch,
                tracks,
                source,
                logicalCartTrack,
                direction,
                extendIntoEntrance: hasWallHole && trackLength == 0,
                tint,
                alpha,
                invalid
            );
        }

        this.DrawMinecartSprite(
            batch,
            tileX,
            tileY,
            direction,
            visuals.MinecartStyle,
            alpha,
            invalid,
            0f
        );
    }

    private void DrawMinecartSprite(
        SpriteBatch batch,
        int tileX,
        int tileY,
        int direction,
        string style,
        float alpha,
        bool invalid,
        float layerDepth)
    {
        direction = StationGeometry.NormalizeDirection(direction);
        Rectangle logicalCart = this.WorldToScreen(
            StationGeometry.GetCartPixelBounds(tileX, tileY, direction)
        );
        Rectangle minecartBounds = this.GetMinecartSpriteBounds(logicalCart, direction);
        Texture2D? minecart = this.visualAssets.GetMinecart(style);
        Color tint = (invalid ? new Color(255, 105, 105) : Color.White) * alpha;

        if (minecart is not null)
        {
            batch.Draw(
                minecart,
                minecartBounds,
                this.visualAssets.GetMinecartSourceRect(direction),
                tint,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                layerDepth
            );
        }
        else
        {
            this.DrawFallbackMinecart(batch, minecartBounds, direction, alpha, invalid);
        }
    }

    private void DrawTrackVisual(
        SpriteBatch batch,
        Texture2D? tracks,
        Rectangle source,
        Rectangle logicalBounds,
        int direction,
        bool extendIntoEntrance,
        Color tint,
        float alpha,
        bool invalid)
    {
        Rectangle destination = this.GetTrackSpriteBounds(logicalBounds);

        if (tracks is not null)
            this.DrawTextureRegion(batch, tracks, source, destination, tint);
        else
            this.DrawFallbackTracks(batch, destination, direction, alpha, invalid);

        if (!extendIntoEntrance)
            return;

        Rectangle overlapBounds = this.GetTrackEntranceOverlapBounds(logicalBounds, direction);

        if (tracks is not null)
        {
            this.DrawTextureRegion(
                batch,
                tracks,
                this.GetTrackEntranceOverlapSource(source, direction),
                overlapBounds,
                tint
            );
        }
        else
        {
            this.DrawFallbackTracks(batch, overlapBounds, direction, alpha, invalid);
        }
    }

    private Point GetTrackSegmentTile(int tileX, int tileY, int direction, int segment)
    {
        Point forward = StationGeometry.GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);
        return new Point(tileX + back.X * segment, tileY + back.Y * segment);
    }

    private void DrawTextureRegion(
        SpriteBatch batch,
        Texture2D texture,
        Rectangle source,
        Rectangle destination,
        Color tint)
    {
        batch.Draw(texture, destination, source, tint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }

    private Rectangle GetEntranceSpriteBounds(Rectangle logicalBounds, int direction)
    {
        direction = StationGeometry.NormalizeDirection(direction);

        int x = direction switch
        {
            1 => logicalBounds.Right - EntranceWorldSize,
            3 => logicalBounds.Left,
            _ => logicalBounds.Center.X - EntranceWorldSize / 2
        };

        return new Rectangle(
            x,
            logicalBounds.Bottom - EntranceWorldSize,
            EntranceWorldSize,
            EntranceWorldSize
        );
    }

    private Rectangle GetTrackSpriteBounds(Rectangle logicalBounds)
    {
        return new Rectangle(
            logicalBounds.X,
            logicalBounds.Y + TrackGroundOffsetY,
            logicalBounds.Width,
            logicalBounds.Height
        );
    }

    private Rectangle GetTrackEntranceOverlapBounds(Rectangle logicalBounds, int direction)
    {
        Rectangle groundBounds = this.GetTrackSpriteBounds(logicalBounds);
        Point forward = StationGeometry.GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);

        if (back.X < 0)
        {
            return new Rectangle(
                groundBounds.X - TrackEntranceOverlap,
                groundBounds.Y,
                TrackEntranceOverlap,
                groundBounds.Height
            );
        }

        if (back.X > 0)
        {
            return new Rectangle(
                groundBounds.Right,
                groundBounds.Y,
                TrackEntranceOverlap,
                groundBounds.Height
            );
        }

        if (back.Y < 0)
        {
            return new Rectangle(
                groundBounds.X,
                groundBounds.Y - TrackEntranceOverlap,
                groundBounds.Width,
                TrackEntranceOverlap
            );
        }

        return new Rectangle(
            groundBounds.X,
            groundBounds.Bottom,
            groundBounds.Width,
            TrackEntranceOverlap
        );
    }

    private Rectangle GetTrackEntranceOverlapSource(Rectangle source, int direction)
    {
        Point forward = StationGeometry.GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);

        if (back.X < 0)
            return new Rectangle(source.X, source.Y, source.Width / 2, source.Height);

        if (back.X > 0)
        {
            return new Rectangle(
                source.Right - source.Width / 2,
                source.Y,
                source.Width / 2,
                source.Height
            );
        }

        if (back.Y < 0)
            return new Rectangle(source.X, source.Y, source.Width, source.Height / 2);

        return new Rectangle(
            source.X,
            source.Bottom - source.Height / 2,
            source.Width,
            source.Height / 2
        );
    }

    private Rectangle GetMinecartSpriteBounds(Rectangle logicalBounds, int direction)
    {
        int sourceBottomPadding = StationGeometry.NormalizeDirection(direction) switch
        {
            0 => 0,
            1 => 1,
            2 => 4,
            3 => 1,
            _ => 0
        };

        int worldBottomPadding = (int)Math.Round(
            sourceBottomPadding
                * (MinecartWorldSize / (double)MinecartVisualAssets.MinecartFrameHeight)
        );

        return new Rectangle(
            logicalBounds.Center.X - MinecartWorldSize / 2,
            logicalBounds.Bottom - MinecartWorldSize + worldBottomPadding,
            MinecartWorldSize,
            MinecartWorldSize
        );
    }

    private void DrawFallbackEntrance(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color frame = (invalid ? new Color(150, 55, 48) : new Color(120, 70, 38)) * alpha;
        Color dark = Color.Black * (0.82f * alpha);
        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        Rectangle opening = vertical
            ? new Rectangle(bounds.Center.X - 18, bounds.Y - 24, 36, 76)
            : new Rectangle(bounds.X - 12, bounds.Center.Y - 18, 88, 36);

        this.Fill(batch, opening, dark);
        this.Outline(batch, opening, frame, 6);
    }

    private void DrawFallbackTracks(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color rail = (invalid ? new Color(180, 70, 65) : new Color(130, 125, 115)) * alpha;
        Color sleeper = (invalid ? new Color(145, 50, 45) : new Color(105, 65, 40)) * alpha;
        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        if (vertical)
        {
            int centerX = bounds.Center.X;
            this.Fill(batch, new Rectangle(centerX - 10, bounds.Y, 4, bounds.Height), rail);
            this.Fill(batch, new Rectangle(centerX + 6, bounds.Y, 4, bounds.Height), rail);
            for (int y = bounds.Y + 6; y < bounds.Bottom; y += 16)
                this.Fill(batch, new Rectangle(centerX - 16, y, 32, 4), sleeper);
        }
        else
        {
            int centerY = bounds.Center.Y;
            this.Fill(batch, new Rectangle(bounds.X, centerY - 10, bounds.Width, 4), rail);
            this.Fill(batch, new Rectangle(bounds.X, centerY + 6, bounds.Width, 4), rail);
            for (int x = bounds.X + 6; x < bounds.Right; x += 16)
                this.Fill(batch, new Rectangle(x, centerY - 16, 4, 32), sleeper);
        }
    }

    private void DrawFallbackMinecart(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color outline = (invalid ? new Color(100, 30, 30) : new Color(45, 37, 33)) * alpha;
        Color wood = (invalid ? new Color(155, 55, 48) : new Color(120, 70, 42)) * alpha;
        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        Rectangle body = vertical
            ? new Rectangle(bounds.Center.X - 22, bounds.Center.Y - 18, 44, 36)
            : new Rectangle(bounds.Center.X - 30, bounds.Center.Y - 14, 60, 28);

        this.Fill(batch, body, outline);
        this.Fill(batch, new Rectangle(body.X + 4, body.Y + 4, body.Width - 8, body.Height - 8), wood);
    }

    private void DrawPlacementFootprint(SpriteBatch batch, int tileX, int tileY, bool valid)
    {
        IReadOnlyList<Point> constructionTiles = StationGeometry.GetConstructionTiles(
            tileX,
            tileY,
            this.placement.StationDirection,
            this.placement.TrackLength,
            this.placement.HasTracks,
            this.placement.HasWallHole
        );

        Point arrival = StationGeometry.GetArrivalTile(tileX, tileY, this.placement.StationDirection);
        Color bodyColor = valid ? Color.CornflowerBlue * 0.45f : Color.Red * 0.5f;
        Color arrivalColor = valid ? Color.LimeGreen * 0.8f : Color.Red * 0.85f;

        foreach (Point tile in constructionTiles)
        {
            Rectangle world = StationGeometry.GetTilePixelBounds(tile);
            this.Outline(batch, this.WorldToScreen(world), bodyColor, 3);
        }

        this.Outline(
            batch,
            this.WorldToScreen(StationGeometry.GetTilePixelBounds(arrival)),
            arrivalColor,
            5
        );
    }

    private Rectangle GetScreenBounds(IReadOnlyList<Point> tiles)
    {
        int minX = tiles.Min(tile => tile.X);
        int minY = tiles.Min(tile => tile.Y);
        int maxX = tiles.Max(tile => tile.X);
        int maxY = tiles.Max(tile => tile.Y);

        Rectangle world = new(
            minX * Game1.tileSize,
            minY * Game1.tileSize,
            (maxX - minX + 1) * Game1.tileSize,
            (maxY - minY + 1) * Game1.tileSize
        );

        return this.WorldToScreen(world);
    }

    private Rectangle WorldToScreen(Rectangle world)
    {
        Vector2 origin = Game1.GlobalToLocal(Game1.viewport, new Vector2(world.X, world.Y));
        return new Rectangle((int)origin.X, (int)origin.Y, world.Width, world.Height);
    }

    private string GetDirectionLabel(int direction)
    {
        string key = StationGeometry.NormalizeDirection(direction) switch
        {
            0 => "direction.up",
            1 => "direction.right",
            2 => "direction.down",
            3 => "direction.left",
            _ => "direction.down"
        };

        return this.helper.Translation.Get(key);
    }

    private void Fill(SpriteBatch batch, Rectangle rectangle, Color color)
    {
        batch.Draw(Game1.staminaRect, rectangle, color);
    }

    private void Outline(SpriteBatch batch, Rectangle rectangle, Color color, int thickness)
    {
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        this.Fill(batch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
