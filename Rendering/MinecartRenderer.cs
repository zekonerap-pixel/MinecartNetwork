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
                this.DrawTextureIntoBounds(batch, wallHole, this.GetTunnelTextureBounds(holeBounds), spriteTint);
            else
                this.DrawProceduralMineEntrance(batch, holeBounds, direction, alpha, invalid);
        }

        if (hasTracks)
        {
            for (int segment = trackLength; segment >= 1; segment--)
            {
                Rectangle segmentBounds = this.GetScreenBounds(
                    this.GetTrackSegmentTiles(tileX, tileY, direction, segment)
                );
                Texture2D? tracks = this.visualAssets.Tracks;

                if (tracks is not null && direction == 2)
                    this.DrawTextureIntoBounds(batch, tracks, segmentBounds, spriteTint);
                else
                    this.DrawProceduralTracks(batch, segmentBounds, direction, alpha, invalid);
            }

            Rectangle cartTrackBounds = this.WorldToScreen(
                StationGeometry.GetCartPixelBounds(tileX, tileY, direction)
            );
            Texture2D? tracksUnderCart = this.visualAssets.Tracks;

            if (tracksUnderCart is not null && direction == 2)
                this.DrawTextureIntoBounds(batch, tracksUnderCart, cartTrackBounds, spriteTint);
            else
                this.DrawProceduralTracks(batch, cartTrackBounds, direction, alpha, invalid);
        }

        Rectangle cartBounds = this.WorldToScreen(
            StationGeometry.GetCartPixelBounds(tileX, tileY, direction)
        );
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
        batch.Draw(texture, bounds, null, tint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }

    private Rectangle GetTunnelTextureBounds(Rectangle logicalBounds)
    {
        return new Rectangle(
            logicalBounds.X - 8,
            logicalBounds.Y - 36,
            logicalBounds.Width + 16,
            logicalBounds.Height + 36
        );
    }

    private void DrawProceduralMineEntrance(
        SpriteBatch batch,
        Rectangle bounds,
        int direction,
        float alpha,
        bool invalid)
    {
        Color shadow = Color.Black * (0.90f * alpha);
        Color deepShadow = new Color(17, 14, 13) * alpha;
        Color rockShadow = new Color(34, 29, 27) * alpha;
        Color timberDark = (invalid ? new Color(100, 35, 30) : new Color(74, 41, 25)) * alpha;
        Color timber = (invalid ? new Color(150, 58, 48) : new Color(132, 75, 38)) * alpha;
        Color timberLight = (invalid ? new Color(190, 78, 65) : new Color(190, 119, 59)) * alpha;
        Color metal = (invalid ? new Color(155, 65, 60) : new Color(103, 101, 91)) * alpha;
        Color lampFrame = (invalid ? new Color(125, 48, 43) : new Color(85, 56, 33)) * alpha;
        Color lamp = (invalid ? new Color(220, 110, 80) : new Color(247, 192, 70)) * alpha;

        int normalized = StationGeometry.NormalizeDirection(direction);

        if (normalized is 0 or 2)
        {
            int centerX = bounds.Center.X;
            Rectangle outerOpening = new(centerX - 19, bounds.Y - 27, 38, 73);
            Rectangle innerOpening = new(centerX - 14, bounds.Y - 20, 28, 64);

            this.Fill(batch, outerOpening, shadow);
            this.Fill(batch, innerOpening, deepShadow);
            this.Fill(batch, new Rectangle(centerX - 12, bounds.Y - 16, 24, 9), rockShadow);

            this.Fill(batch, new Rectangle(centerX - 29, bounds.Y - 31, 10, 83), timberDark);
            this.Fill(batch, new Rectangle(centerX + 19, bounds.Y - 31, 10, 83), timberDark);
            this.Fill(batch, new Rectangle(centerX - 33, bounds.Y - 38, 66, 13), timber);
            this.Fill(batch, new Rectangle(centerX - 29, bounds.Y - 35, 58, 4), timberLight);

            this.Fill(batch, new Rectangle(centerX - 32, bounds.Y + 45, 16, 9), timberDark);
            this.Fill(batch, new Rectangle(centerX + 16, bounds.Y + 45, 16, 9), timberDark);

            this.Fill(batch, new Rectangle(centerX - 10, bounds.Y + 9, 4, 55), metal);
            this.Fill(batch, new Rectangle(centerX + 6, bounds.Y + 9, 4, 55), metal);
            for (int y = bounds.Y + 14; y < bounds.Bottom; y += 14)
                this.Fill(batch, new Rectangle(centerX - 15, y, 30, 4), timber);

            this.Fill(batch, new Rectangle(centerX + 31, bounds.Y - 20, 9, 19), lampFrame);
            this.Fill(batch, new Rectangle(centerX + 33, bounds.Y - 16, 5, 10), lamp);
            this.Fill(batch, new Rectangle(centerX + 32, bounds.Y - 22, 7, 3), timberDark);
            return;
        }

        bool opensRight = normalized == 1;
        int centerY = bounds.Center.Y;

        int backX = opensRight ? bounds.X + 2 : bounds.Right - 12;
        int openingX = opensRight ? bounds.X + 10 : bounds.X - 2;
        int darkX = opensRight ? bounds.X + 15 : bounds.X - 1;

        Rectangle sideOuterOpening = new(openingX, centerY - 23, 58, 46);
        Rectangle sideInnerOpening = new(darkX, centerY - 17, 49, 34);
        this.Fill(batch, sideOuterOpening, shadow);
        this.Fill(batch, sideInnerOpening, deepShadow);

        int rearShadowX = opensRight ? backX + 11 : backX - 8;
        this.Fill(batch, new Rectangle(rearShadowX, centerY - 16, 10, 32), rockShadow);

        this.Fill(batch, new Rectangle(backX, centerY - 34, 10, 68), timberDark);
        this.Fill(batch, new Rectangle(backX + (opensRight ? 3 : 1), centerY - 30, 4, 60), timberLight);

        int beamX = opensRight ? backX : bounds.X - 4;
        this.Fill(batch, new Rectangle(beamX, centerY - 31, 70, 10), timber);
        this.Fill(batch, new Rectangle(beamX, centerY + 21, 70, 10), timberDark);

        if (opensRight)
        {
            this.Fill(batch, new Rectangle(backX + 9, centerY - 27, 12, 5), timber);
            this.Fill(batch, new Rectangle(backX + 14, centerY - 22, 7, 5), timberDark);
        }
        else
        {
            this.Fill(batch, new Rectangle(backX - 11, centerY - 27, 12, 5), timber);
            this.Fill(batch, new Rectangle(backX - 11, centerY - 22, 7, 5), timberDark);
        }

        int railStart = opensRight ? bounds.X + 16 : bounds.X - 4;
        int railWidth = 54;
        this.Fill(batch, new Rectangle(railStart, centerY - 10, railWidth, 4), metal);
        this.Fill(batch, new Rectangle(railStart, centerY + 6, railWidth, 4), metal);

        for (int x = railStart + 3; x < railStart + railWidth; x += 14)
            this.Fill(batch, new Rectangle(x, centerY - 15, 4, 30), timber);

        int lampX = opensRight ? backX - 7 : backX + 9;
        this.Fill(batch, new Rectangle(lampX, centerY - 13, 8, 17), lampFrame);
        this.Fill(batch, new Rectangle(lampX + 2, centerY - 9, 4, 9), lamp);
    }

    private void DrawProceduralTracks(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color metal = (invalid ? new Color(170, 55, 55) : new Color(119, 117, 109)) * alpha;
        Color highlight = (invalid ? new Color(205, 78, 70) : new Color(177, 171, 153)) * alpha;
        Color wood = (invalid ? new Color(155, 50, 45) : new Color(105, 64, 38)) * alpha;
        Color woodLight = (invalid ? new Color(190, 72, 64) : new Color(151, 92, 47)) * alpha;

        bool vertical = StationGeometry.NormalizeDirection(direction) is 0 or 2;

        if (vertical)
        {
            int centerX = bounds.Center.X;
            int rail1 = centerX - 9;
            int rail2 = centerX + 9;

            this.Fill(batch, new Rectangle(rail1 - 2, bounds.Y, 4, bounds.Height), metal);
            this.Fill(batch, new Rectangle(rail2 - 2, bounds.Y, 4, bounds.Height), metal);
            this.Fill(batch, new Rectangle(rail1 - 1, bounds.Y, 1, bounds.Height), highlight);
            this.Fill(batch, new Rectangle(rail2 - 1, bounds.Y, 1, bounds.Height), highlight);

            for (int y = bounds.Y + 5; y < bounds.Bottom; y += 15)
            {
                this.Fill(batch, new Rectangle(centerX - 15, y, 30, 5), wood);
                this.Fill(batch, new Rectangle(centerX - 12, y + 1, 24, 2), woodLight);
            }

            return;
        }

        int centerY = bounds.Center.Y;
        int railTop = centerY - 9;
        int railBottom = centerY + 9;

        this.Fill(batch, new Rectangle(bounds.X, railTop - 2, bounds.Width, 4), metal);
        this.Fill(batch, new Rectangle(bounds.X, railBottom - 2, bounds.Width, 4), metal);
        this.Fill(batch, new Rectangle(bounds.X, railTop - 1, bounds.Width, 1), highlight);
        this.Fill(batch, new Rectangle(bounds.X, railBottom - 1, bounds.Width, 1), highlight);

        for (int x = bounds.X + 5; x < bounds.Right; x += 15)
        {
            this.Fill(batch, new Rectangle(x, centerY - 15, 5, 30), wood);
            this.Fill(batch, new Rectangle(x + 1, centerY - 12, 2, 24), woodLight);
        }
    }

    private void DrawProceduralMinecart(SpriteBatch batch, Rectangle bounds, int direction, float alpha, bool invalid)
    {
        Color outline = (invalid ? new Color(90, 27, 27) : new Color(40, 34, 31)) * alpha;
        Color deepest = (invalid ? new Color(82, 25, 25) : new Color(28, 25, 23)) * alpha;
        Color metalDark = (invalid ? new Color(135, 47, 45) : new Color(72, 72, 70)) * alpha;
        Color metal = (invalid ? new Color(165, 58, 55) : new Color(104, 102, 96)) * alpha;
        Color metalLight = (invalid ? new Color(200, 82, 72) : new Color(164, 156, 139)) * alpha;
        Color woodDark = (invalid ? new Color(126, 42, 39) : new Color(82, 47, 31)) * alpha;
        Color wood = (invalid ? new Color(150, 50, 45) : new Color(116, 67, 40)) * alpha;
        Color woodLight = (invalid ? new Color(195, 76, 65) : new Color(174, 105, 55)) * alpha;

        int normalized = StationGeometry.NormalizeDirection(direction);

        if (normalized is 0 or 2)
        {
            int centerX = bounds.Center.X;
            bool frontAtBottom = normalized == 2;

            Rectangle body = new(centerX - 21, bounds.Center.Y - 17, 42, 35);
            Rectangle rim = new(centerX - 23, body.Y - 4, 46, 8);
            Rectangle shell = new(body.X + 3, body.Y + 3, body.Width - 6, body.Height - 6);

            this.Fill(batch, new Rectangle(body.X + 2, body.Bottom - 2, body.Width - 4, 6), deepest);
            this.Fill(batch, body, outline);
            this.Fill(batch, shell, wood);

            this.Fill(batch, rim, outline);
            this.Fill(batch, new Rectangle(rim.X + 3, rim.Y + 2, rim.Width - 6, 3), metal);
            this.Fill(batch, new Rectangle(rim.X + 5, rim.Y + 1, rim.Width - 10, 1), metalLight);

            int cavityY = frontAtBottom ? body.Y + 5 : body.Bottom - 16;
            this.Fill(batch, new Rectangle(centerX - 15, cavityY, 30, 11), deepest);
            this.Fill(batch, new Rectangle(centerX - 13, cavityY + 2, 26, 3), woodDark);

            int frontFaceY = frontAtBottom ? body.Bottom - 13 : body.Y + 3;
            this.Fill(batch, new Rectangle(centerX - 17, frontFaceY, 34, 11), woodDark);
            this.Fill(batch, new Rectangle(centerX - 15, frontFaceY + 2, 30, 5), wood);
            this.Fill(batch, new Rectangle(centerX - 13, frontFaceY + 2, 26, 2), woodLight);
            this.Fill(batch, new Rectangle(centerX - 7, frontFaceY + 6, 14, 4), metalDark);
            this.Fill(batch, new Rectangle(centerX - 5, frontFaceY + 7, 10, 2), metalLight);

            this.Fill(batch, new Rectangle(body.X + 3, body.Y + 5, 4, body.Height - 9), woodDark);
            this.Fill(batch, new Rectangle(body.Right - 7, body.Y + 5, 4, body.Height - 9), outline);

            int wheelY = frontAtBottom ? body.Bottom - 1 : body.Y - 5;
            this.Fill(batch, new Rectangle(centerX - 17, wheelY, 10, 7), outline);
            this.Fill(batch, new Rectangle(centerX + 7, wheelY, 10, 7), outline);
            this.Fill(batch, new Rectangle(centerX - 14, wheelY + 2, 5, 4), metal);
            this.Fill(batch, new Rectangle(centerX + 9, wheelY + 2, 5, 4), metal);
            this.Fill(batch, new Rectangle(centerX - 10, wheelY + 3, 20, 3), metalDark);
            return;
        }

        bool frontAtRight = normalized == 1;
        int sideCenterY = bounds.Center.Y;
        int sideCenterX = bounds.Center.X;

        Rectangle sideBody = new(sideCenterX - 24, sideCenterY - 14, 48, 28);
        Rectangle sideLowerBody = new(sideBody.X + 2, sideBody.Y + 5, sideBody.Width - 4, sideBody.Height - 5);
        Rectangle sideRim = new(sideBody.X - 2, sideBody.Y - 5, sideBody.Width + 4, 9);

        this.Fill(batch, new Rectangle(sideBody.X + 4, sideBody.Bottom - 1, sideBody.Width - 8, 7), deepest);

        this.Fill(batch, new Rectangle(sideBody.X + 3, sideBody.Y, sideBody.Width - 6, sideBody.Height), outline);
        this.Fill(batch, new Rectangle(sideBody.X, sideBody.Y + 5, sideBody.Width, sideBody.Height - 10), outline);
        this.Fill(batch, new Rectangle(sideLowerBody.X + 3, sideLowerBody.Y + 2, sideLowerBody.Width - 6, sideLowerBody.Height - 4), wood);
        this.Fill(batch, new Rectangle(sideLowerBody.X + 1, sideLowerBody.Y + 5, 4, sideLowerBody.Height - 8), woodDark);
        this.Fill(batch, new Rectangle(sideLowerBody.Right - 5, sideLowerBody.Y + 5, 4, sideLowerBody.Height - 8), woodDark);

        this.Fill(batch, sideRim, outline);
        this.Fill(batch, new Rectangle(sideRim.X + 3, sideRim.Y + 2, sideRim.Width - 6, 4), metal);
        this.Fill(batch, new Rectangle(sideRim.X + 5, sideRim.Y + 1, sideRim.Width - 10, 1), metalLight);

        Rectangle sideCavity = new(sideBody.X + 7, sideBody.Y + 2, sideBody.Width - 14, 10);
        this.Fill(batch, sideCavity, deepest);
        this.Fill(batch, new Rectangle(sideCavity.X + 3, sideCavity.Y + 2, sideCavity.Width - 6, 2), woodDark);

        this.Fill(batch, new Rectangle(sideBody.X + 5, sideBody.Y + 14, sideBody.Width - 10, 8), woodDark);
        this.Fill(batch, new Rectangle(sideBody.X + 7, sideBody.Y + 15, sideBody.Width - 14, 4), wood);
        this.Fill(batch, new Rectangle(sideBody.X + 9, sideBody.Y + 15, sideBody.Width - 18, 2), woodLight);

        int frontX = frontAtRight ? sideBody.Right - 8 : sideBody.X;
        this.Fill(batch, new Rectangle(frontX, sideBody.Y + 6, 8, sideBody.Height - 12), metalDark);
        this.Fill(batch, new Rectangle(frontAtRight ? frontX + 1 : frontX + 4, sideBody.Y + 8, 3, sideBody.Height - 16), metal);

        int sideWheelY = sideBody.Bottom - 1;
        int wheel1X = sideBody.X + 8;
        int wheel2X = sideBody.Right - 16;

        this.Fill(batch, new Rectangle(wheel1X, sideWheelY, 10, 8), outline);
        this.Fill(batch, new Rectangle(wheel2X, sideWheelY, 10, 8), outline);
        this.Fill(batch, new Rectangle(wheel1X + 3, sideWheelY + 2, 5, 5), metal);
        this.Fill(batch, new Rectangle(wheel2X + 2, sideWheelY + 2, 5, 5), metal);
        this.Fill(batch, new Rectangle(wheel1X + 7, sideWheelY + 4, wheel2X - wheel1X - 4, 3), metalDark);
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

        Point arrival = StationGeometry.GetArrivalTile(
            tileX,
            tileY,
            this.placement.StationDirection
        );

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
