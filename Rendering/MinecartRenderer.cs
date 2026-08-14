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

        // Tunnel is the rear-most/background element.
        if (hasWallHole)
        {
            int effectiveLength = hasTracks ? trackLength : 0;
            IReadOnlyList<Point> holeTiles = StationGeometry.GetHoleTiles(tileX, tileY, direction, effectiveLength);
            Rectangle holeBounds = this.GetScreenBounds(holeTiles);
            Texture2D? wallHole = this.visualAssets.WallHole;
            if (wallHole is not null && direction == 2)
                this.DrawTextureIntoBounds(batch, wallHole, this.GetTunnelTextureBounds(holeBounds), spriteTint);
            else
                this.DrawProceduralMineEntrance(batch, holeBounds, direction, alpha, invalid);
        }

        if (hasTracks)
        {
            // Rail corridor between tunnel and cart.
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

            // The cart is visibly sitting on rails, but the logical footprint remains one cart tile.
            Rectangle cartTrackBounds = this.WorldToScreen(StationGeometry.GetCartPixelBounds(tileX, tileY, direction));
            Texture2D? tracksUnderCart = this.visualAssets.Tracks;
            if (tracksUnderCart is not null && direction == 2)
                this.DrawTextureIntoBounds(batch, tracksUnderCart, cartTrackBounds, spriteTint);
            else
                this.DrawProceduralTracks(batch, cartTrackBounds, direction, alpha, invalid);
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

        return new[]
        {
            new Point(
                tileX + back.X * segment,
                tileY + back.Y * segment
            )
        };
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

    private Rectangle GetTunnelTextureBounds(Rectangle logicalBounds)
    {
        // The tunnel structure may overhang its logical 1x1 tile visually.
        return new Rectangle(
            logicalBounds.X - 8,
            logicalBounds.Y - 24,
            logicalBounds.Width + 16,
            logicalBounds.Height + 24
        );
    }

    private void DrawProceduralMineEntrance(
        SpriteBatch batch,
        Rectangle bounds,
        int direction,
        float alpha,
        bool invalid)
    {
        Color shadow = Color.Black * (0.82f * alpha);
        Color timberDark = (invalid ? new Color(100, 35, 30) : new Color(82, 48, 30)) * alpha;
        Color timber = (invalid ? new Color(150, 58, 48) : new Color(139, 83, 45)) * alpha;
        Color timberLight = (invalid ? new Color(190, 78, 65) : new Color(187, 117, 61)) * alpha;
        Color metal = (invalid ? new Color(155, 65, 60) : new Color(104, 96, 82)) * alpha;
        Color lamp = (invalid ? new Color(220, 110, 80) : new Color(245, 194, 82)) * alpha;

        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        if (vertical)
        {
            Rectangle opening = new(bounds.X + 17, bounds.Y - 6, 30, 58);
            this.Fill(batch, opening, shadow);

            // Timber posts and lintel, inspired by a compact mine entrance.
            this.Fill(batch, new Rectangle(bounds.X + 10, bounds.Y - 10, 8, 68), timberDark);
            this.Fill(batch, new Rectangle(bounds.Right - 18, bounds.Y - 10, 8, 68), timberDark);
            this.Fill(batch, new Rectangle(bounds.X + 5, bounds.Y - 15, bounds.Width - 10, 10), timber);
            this.Fill(batch, new Rectangle(bounds.X + 9, bounds.Y - 12, bounds.Width - 18, 4), timberLight);

            // Short rails disappear into the dark opening.
            this.Fill(batch, new Rectangle(bounds.X + 24, bounds.Y + 25, 4, 38), metal);
            this.Fill(batch, new Rectangle(bounds.X + 36, bounds.Y + 25, 4, 38), metal);
            for (int y = bounds.Y + 28; y < bounds.Bottom; y += 13)
                this.Fill(batch, new Rectangle(bounds.X + 20, y, 24, 4), timber);

            // Small warm work lamp on the right post.
            this.Fill(batch, new Rectangle(bounds.Right - 14, bounds.Y + 5, 8, 12), timberDark);
            this.Fill(batch, new Rectangle(bounds.Right - 13, bounds.Y + 7, 6, 7), lamp);
        }
        else
        {
            Rectangle opening = new(bounds.X + 6, bounds.Y + 17, 52, 30);
            this.Fill(batch, opening, shadow);

            this.Fill(batch, new Rectangle(bounds.X + 1, bounds.Y + 10, 62, 8), timberDark);
            this.Fill(batch, new Rectangle(bounds.X + 1, bounds.Bottom - 18, 62, 8), timberDark);
            this.Fill(batch, new Rectangle(bounds.X - 4, bounds.Y + 5, 10, bounds.Height - 10), timber);
            this.Fill(batch, new Rectangle(bounds.X - 1, bounds.Y + 9, 4, bounds.Height - 18), timberLight);

            this.Fill(batch, new Rectangle(bounds.X + 22, bounds.Y + 24, 42, 4), metal);
            this.Fill(batch, new Rectangle(bounds.X + 22, bounds.Y + 36, 42, 4), metal);
            for (int x = bounds.X + 24; x < bounds.Right; x += 13)
                this.Fill(batch, new Rectangle(x, bounds.Y + 20, 4, 24), timber);

            this.Fill(batch, new Rectangle(bounds.X + 8, bounds.Bottom - 14, 12, 8), timberDark);
            this.Fill(batch, new Rectangle(bounds.X + 10, bounds.Bottom - 13, 7, 6), lamp);
        }
    }

    private void DrawProceduralTracks(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color metal = (invalid ? new Color(170, 55, 55) : new Color(124, 121, 111)) * alpha;
        Color highlight = (invalid ? new Color(205, 78, 70) : new Color(184, 178, 157)) * alpha;
        Color wood = (invalid ? new Color(155, 50, 45) : new Color(116, 73, 43)) * alpha;
        Color woodLight = (invalid ? new Color(190, 72, 64) : new Color(160, 103, 55)) * alpha;
        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        if (vertical)
        {
            int rail1 = bounds.X + 22;
            int rail2 = bounds.X + 42;
            this.Fill(batch, new Rectangle(rail1 - 2, bounds.Y, 5, bounds.Height), metal);
            this.Fill(batch, new Rectangle(rail2 - 2, bounds.Y, 5, bounds.Height), metal);
            this.Fill(batch, new Rectangle(rail1 - 1, bounds.Y, 1, bounds.Height), highlight);
            this.Fill(batch, new Rectangle(rail2 - 1, bounds.Y, 1, bounds.Height), highlight);

            for (int y = bounds.Y + 6; y < bounds.Bottom; y += 16)
            {
                this.Fill(batch, new Rectangle(bounds.X + 14, y, 36, 6), wood);
                this.Fill(batch, new Rectangle(bounds.X + 17, y + 1, 30, 2), woodLight);
            }
        }
        else
        {
            int rail1 = bounds.Y + 22;
            int rail2 = bounds.Y + 42;
            this.Fill(batch, new Rectangle(bounds.X, rail1 - 2, bounds.Width, 5), metal);
            this.Fill(batch, new Rectangle(bounds.X, rail2 - 2, bounds.Width, 5), metal);
            this.Fill(batch, new Rectangle(bounds.X, rail1 - 1, bounds.Width, 1), highlight);
            this.Fill(batch, new Rectangle(bounds.X, rail2 - 1, bounds.Width, 1), highlight);

            for (int x = bounds.X + 6; x < bounds.Right; x += 16)
            {
                this.Fill(batch, new Rectangle(x, bounds.Y + 14, 6, 36), wood);
                this.Fill(batch, new Rectangle(x + 1, bounds.Y + 17, 2, 30), woodLight);
            }
        }
    }

    private void DrawProceduralMinecart(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color outline = (invalid ? new Color(90, 27, 27) : new Color(47, 39, 35)) * alpha;
        Color metal = (invalid ? new Color(165, 58, 55) : new Color(108, 103, 94)) * alpha;
        Color metalLight = (invalid ? new Color(200, 82, 72) : new Color(160, 151, 130)) * alpha;
        Color wood = (invalid ? new Color(150, 50, 45) : new Color(120, 72, 43)) * alpha;
        Color woodLight = (invalid ? new Color(195, 76, 65) : new Color(173, 106, 57)) * alpha;
        int normalized = StationGeometry.NormalizeDirection(direction);
        bool verticalTravel = normalized is 0 or 2;

        // Compact visual contained around one logical tile. A few pixels may visually
        // approach the tile edge, but interaction/collision remains exactly 1x1.
        Rectangle body = verticalTravel
            ? new Rectangle(bounds.X + 7, bounds.Y + 13, 50, 36)
            : new Rectangle(bounds.X + 13, bounds.Y + 7, 36, 50);

        this.Fill(batch, body, outline);
        Rectangle shell = this.Inset(body, 4);
        this.Fill(batch, shell, wood);

        // Dark open interior/tub.
        if (verticalTravel)
        {
            this.Fill(batch, new Rectangle(shell.X + 4, shell.Y + 4, shell.Width - 8, 13), outline);
            this.Fill(batch, new Rectangle(shell.X + 5, shell.Y + 5, shell.Width - 10, 3), metal);
            this.Fill(batch, new Rectangle(shell.X + 4, shell.Bottom - 8, shell.Width - 8, 4), woodLight);

            int wheelY = normalized == 0 ? body.Y - 3 : body.Bottom - 3;
            this.Fill(batch, new Rectangle(body.X + 7, wheelY, 11, 7), outline);
            this.Fill(batch, new Rectangle(body.Right - 18, wheelY, 11, 7), outline);
            this.Fill(batch, new Rectangle(body.X + 10, wheelY + 2, 5, 4), metalLight);
            this.Fill(batch, new Rectangle(body.Right - 15, wheelY + 2, 5, 4), metalLight);
        }
        else
        {
            this.Fill(batch, new Rectangle(shell.X + 4, shell.Y + 4, 13, shell.Height - 8), outline);
            this.Fill(batch, new Rectangle(shell.X + 5, shell.Y + 5, 3, shell.Height - 10), metal);
            this.Fill(batch, new Rectangle(shell.Right - 8, shell.Y + 4, 4, shell.Height - 8), woodLight);

            int wheelX = normalized == 3 ? body.X - 3 : body.Right - 3;
            this.Fill(batch, new Rectangle(wheelX, body.Y + 7, 7, 11), outline);
            this.Fill(batch, new Rectangle(wheelX, body.Bottom - 18, 7, 11), outline);
            this.Fill(batch, new Rectangle(wheelX + 2, body.Y + 10, 4, 5), metalLight);
            this.Fill(batch, new Rectangle(wheelX + 2, body.Bottom - 15, 4, 5), metalLight);
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
