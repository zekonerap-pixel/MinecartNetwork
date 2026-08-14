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

    public MinecartRenderer(IModHelper helper, StationManager stations, PlacementManager placement)
    {
        this.helper = helper;
        this.stations = stations;
        this.placement = placement;
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

            this.DrawMinecart(
                e.SpriteBatch,
                station.VisualTileX!.Value,
                station.VisualTileY!.Value,
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
        this.DrawMinecart(
            e.SpriteBatch,
            tile.X,
            tile.Y,
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

        string text = string.Join(
            Environment.NewLine,
            this.helper.Translation.Get("placement.title", new
            {
                name = this.placement.PendingName,
                category = this.placement.PendingCategory
            }),
            this.helper.Translation.Get("placement.controls"),
            this.helper.Translation.Get("placement.options", new
            {
                tracks = this.placement.HasTracks ? this.helper.Translation.Get("common.on") : this.helper.Translation.Get("common.off"),
                hole = this.placement.HasWallHole ? this.helper.Translation.Get("common.on") : this.helper.Translation.Get("common.off")
            }),
            status
        );

        Vector2 size = Game1.smallFont.MeasureString(text);
        var panel = new Rectangle(16, 16, (int)size.X + 28, (int)size.Y + 24);

        e.SpriteBatch.Draw(Game1.staminaRect, panel, Color.Black * 0.72f);
        e.SpriteBatch.DrawString(Game1.smallFont, text, new Vector2(30, 28), Color.White);
    }

    private void DrawMinecart(
        SpriteBatch batch,
        int tileX,
        int tileY,
        bool hasTracks,
        bool hasWallHole,
        float alpha,
        bool invalid)
    {
        Vector2 origin = Game1.GlobalToLocal(
            Game1.viewport,
            new Vector2(tileX * Game1.tileSize, tileY * Game1.tileSize)
        );

        int x = (int)origin.X;
        int y = (int)origin.Y;

        Color dark = (invalid ? new Color(95, 28, 28) : new Color(52, 42, 38)) * alpha;
        Color metal = (invalid ? new Color(170, 55, 55) : new Color(116, 116, 112)) * alpha;
        Color wood = (invalid ? new Color(155, 50, 45) : new Color(128, 78, 48)) * alpha;
        Color woodLight = (invalid ? new Color(200, 74, 65) : new Color(180, 116, 66)) * alpha;
        Color shadow = Color.Black * (0.65f * alpha);

        if (hasWallHole)
        {
            this.Fill(batch, new Rectangle(x + 18, y - 24, 92, 48), shadow);
            this.Fill(batch, new Rectangle(x + 26, y - 18, 76, 42), dark);
        }

        if (hasTracks)
        {
            for (int sleeperX = x + 4; sleeperX <= x + 116; sleeperX += 28)
                this.Fill(batch, new Rectangle(sleeperX, y + 52, 18, 6), wood);

            this.Fill(batch, new Rectangle(x, y + 48, 128, 5), metal);
            this.Fill(batch, new Rectangle(x, y + 59, 128, 5), metal);
            this.Fill(batch, new Rectangle(x, y + 49, 128, 2), Color.White * (0.25f * alpha));
        }

        this.Fill(batch, new Rectangle(x + 14, y + 12, 100, 8), dark);
        this.Fill(batch, new Rectangle(x + 18, y + 18, 92, 30), wood);
        this.Fill(batch, new Rectangle(x + 24, y + 20, 80, 7), woodLight);
        this.Fill(batch, new Rectangle(x + 22, y + 42, 84, 8), dark);

        this.Fill(batch, new Rectangle(x + 28, y + 48, 18, 14), dark);
        this.Fill(batch, new Rectangle(x + 76, y + 48, 18, 14), dark);
        this.Fill(batch, new Rectangle(x + 32, y + 51, 10, 8), metal);
        this.Fill(batch, new Rectangle(x + 80, y + 51, 10, 8), metal);

        this.Fill(batch, new Rectangle(x + 10, y + 9, 108, 5), metal);
        this.Fill(batch, new Rectangle(x + 20, y + 17, 4, 26), woodLight);
        this.Fill(batch, new Rectangle(x + 104, y + 17, 4, 26), dark);
    }

    private void DrawPlacementFootprint(SpriteBatch batch, int tileX, int tileY, bool valid)
    {
        Vector2 origin = Game1.GlobalToLocal(
            Game1.viewport,
            new Vector2(tileX * Game1.tileSize, tileY * Game1.tileSize)
        );

        Color color = valid ? Color.LimeGreen * 0.55f : Color.Red * 0.65f;
        var cartArea = new Rectangle((int)origin.X, (int)origin.Y, Game1.tileSize * 2, Game1.tileSize);
        var arrivalArea = new Rectangle((int)origin.X, (int)origin.Y + Game1.tileSize, Game1.tileSize, Game1.tileSize);

        this.Outline(batch, cartArea, color, 4);
        this.Outline(batch, arrivalArea, color, 4);
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
