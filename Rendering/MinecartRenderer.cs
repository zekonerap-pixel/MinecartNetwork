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
    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly PlacementManager placement;
    private readonly MinecartVisualAssets visualAssets;

    public MinecartRenderer(IModHelper helper, StationManager stations, PlacementManager placement)
    {
        this.helper = helper;
        this.stations = stations;
        this.placement = placement;
        this.visualAssets = new MinecartVisualAssets(helper);
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

            this.DrawStation(
                e.SpriteBatch,
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
        var panel = new Rectangle(16, 16, (int)size.X + 28, (int)size.Y + 24);

        e.SpriteBatch.Draw(Game1.staminaRect, panel, Color.Black * 0.72f);
        e.SpriteBatch.DrawString(Game1.smallFont, text, new Vector2(30, 28), Color.White);
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
        direction = StationGeometry.NormalizeDirection(direction);
        trackLength = Math.Clamp(trackLength, StationGeometry.MinTrackLength, StationGeometry.MaxTrackLength);
        Color spriteTint = (invalid ? new Color(255, 105, 105) : Color.White) * alpha;

        if (hasWallHole)
        {
            int effectiveLength = hasTracks ? trackLength : 0;
            IReadOnlyList<Point> holeTiles = StationGeometry.GetHoleTiles(tileX, tileY, direction, effectiveLength);
            Rectangle holeBounds = this.GetScreenBounds(holeTiles);
            Texture2D? wallHole = this.visualAssets.WallHole;
            if (wallHole is not null && direction == 2)
                this.DrawTextureIntoBounds(batch, wallHole, holeBounds, spriteTint);
            else
                this.DrawProceduralWallHole(batch, holeBounds, alpha, invalid);
        }

        if (hasTracks)
        {
            for (int segment = trackLength; segment >= 1; segment--)
            {
                IReadOnlyList<Point> segmentTiles = this.GetTrackSegmentTiles(tileX, tileY, direction, segment);
                Rectangle segmentBounds = this.GetScreenBounds(segmentTiles);
                Texture2D? tracks = this.visualAssets.Tracks;
                if (tracks is not null && direction == 2)
                    this.DrawTextureIntoBounds(batch, tracks, segmentBounds, spriteTint);
                else
                    this.DrawProceduralTracks(batch, segmentBounds, direction, alpha, invalid);
            }
        }

        Rectangle cartWorld = StationGeometry.GetCartPixelBounds(tileX, tileY, direction);
        Rectangle cartBounds = this.WorldToScreen(cartWorld);
        Texture2D? minecart = this.visualAssets.Minecart;
        if (minecart is not null && direction == 2)
            this.DrawTextureIntoBounds(batch, minecart, cartBounds, spriteTint);
        else
            this.DrawProceduralMinecart(batch, cartBounds, direction, alpha, invalid);
    }

    private IReadOnlyList<Point> GetTrackSegmentTiles(int tileX, int tileY, int direction, int segment)
    {
        Point forward = StationGeometry.GetForwardVector(direction);
        Point back = new(-forward.X, -forward.Y);
        int anchorX = tileX + back.X * segment;
        int anchorY = tileY + back.Y * segment;

        return direction is 0 or 2
            ? new[] { new Point(anchorX, anchorY), new Point(anchorX + 1, anchorY) }
            : new[] { new Point(anchorX, anchorY), new Point(anchorX, anchorY + 1) };
    }

    private void DrawTextureIntoBounds(SpriteBatch batch, Texture2D texture, Rectangle bounds, Color tint)
    {
        batch.Draw(
            texture,
            bounds,
            null,
            tint,
            0f,
            Vector2.Zero,
            SpriteEffects.None,
            0f
        );
    }

    private void DrawProceduralWallHole(SpriteBatch batch, Rectangle bounds, float alpha, bool invalid)
    {
        Color dark = (invalid ? new Color(95, 28, 28) : new Color(52, 42, 38)) * alpha;
        Color shadow = Color.Black * (0.72f * alpha);

        Rectangle outer = this.Inset(bounds, 8);
        Rectangle inner = this.Inset(bounds, 16);
        this.Fill(batch, outer, shadow);
        this.Fill(batch, inner, dark);
    }

    private void DrawProceduralTracks(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color metal = (invalid ? new Color(170, 55, 55) : new Color(116, 116, 112)) * alpha;
        Color wood = (invalid ? new Color(155, 50, 45) : new Color(128, 78, 48)) * alpha;
        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        if (vertical)
        {
            int rail1 = bounds.X + bounds.Width / 3;
            int rail2 = bounds.X + bounds.Width * 2 / 3;
            this.Fill(batch, new Rectangle(rail1 - 3, bounds.Y, 6, bounds.Height), metal);
            this.Fill(batch, new Rectangle(rail2 - 3, bounds.Y, 6, bounds.Height), metal);

            for (int y = bounds.Y + 8; y < bounds.Bottom; y += 22)
                this.Fill(batch, new Rectangle(bounds.X + 12, y, bounds.Width - 24, 6), wood);
        }
        else
        {
            int rail1 = bounds.Y + bounds.Height / 3;
            int rail2 = bounds.Y + bounds.Height * 2 / 3;
            this.Fill(batch, new Rectangle(bounds.X, rail1 - 3, bounds.Width, 6), metal);
            this.Fill(batch, new Rectangle(bounds.X, rail2 - 3, bounds.Width, 6), metal);

            for (int x = bounds.X + 8; x < bounds.Right; x += 22)
                this.Fill(batch, new Rectangle(x, bounds.Y + 12, 6, bounds.Height - 24), wood);
        }
    }

    private void DrawProceduralMinecart(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color dark = (invalid ? new Color(95, 28, 28) : new Color(52, 42, 38)) * alpha;
        Color metal = (invalid ? new Color(170, 55, 55) : new Color(116, 116, 112)) * alpha;
        Color wood = (invalid ? new Color(155, 50, 45) : new Color(128, 78, 48)) * alpha;
        Color woodLight = (invalid ? new Color(200, 74, 65) : new Color(180, 116, 66)) * alpha;
        bool verticalTravel = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        Rectangle body = this.Inset(bounds, 10);
        this.Fill(batch, body, dark);
        this.Fill(batch, this.Inset(body, 6), wood);

        if (verticalTravel)
        {
            this.Fill(batch, new Rectangle(body.X + 8, body.Y + 7, body.Width - 16, 7), woodLight);
            this.Fill(batch, new Rectangle(body.X + 14, body.Bottom - 7, 18, 10), metal);
            this.Fill(batch, new Rectangle(body.Right - 32, body.Bottom - 7, 18, 10), metal);
        }
        else
        {
            this.Fill(batch, new Rectangle(body.X + 7, body.Y + 8, 7, body.Height - 16), woodLight);
            this.Fill(batch, new Rectangle(body.Right - 7, body.Y + 14, 10, 18), metal);
            this.Fill(batch, new Rectangle(body.Right - 7, body.Bottom - 32, 10, 18), metal);
        }
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

    private Rectangle Inset(Rectangle rectangle, int amount)
    {
        int x = rectangle.X + amount;
        int y = rectangle.Y + amount;
        int width = Math.Max(1, rectangle.Width - amount * 2);
        int height = Math.Max(1, rectangle.Height - amount * 2);
        return new Rectangle(x, y, width, height);
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
