using Microsoft.Xna.Framework;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class PlacementManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly ModConfig config;

    public bool IsPlacing { get; private set; }
    public string PendingName { get; private set; } = "";
    public string PendingCategory { get; private set; } = "";
    public bool HasTracks { get; private set; } = true;
    public bool HasWallHole { get; private set; }

    public PlacementManager(IModHelper helper, IMonitor monitor, StationManager stations, ModConfig config)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.config = config;
    }

    public bool Begin(string name, string category)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
        {
            this.monitor.Log("You can only start minecart placement while the player is free in a loaded save.", LogLevel.Warn);
            return false;
        }

        if (!Context.IsMainPlayer)
        {
            this.monitor.Log("Only the host can place custom minecarts in this alpha.", LogLevel.Warn);
            return false;
        }

        this.PendingName = string.IsNullOrWhiteSpace(name) ? "Minecart" : name.Trim();
        this.PendingCategory = string.IsNullOrWhiteSpace(category) ? this.config.DefaultCategory : category.Trim();
        this.HasTracks = true;
        this.HasWallHole = false;
        this.IsPlacing = true;

        this.monitor.Log(
            $"Placement started for '{this.PendingName}' ({this.PendingCategory}). Left click places; T toggles tracks; H toggles wall hole; right click/Escape cancels. Movement remains enabled.",
            LogLevel.Info
        );
        return true;
    }

    public void Cancel(bool silent = false)
    {
        if (!this.IsPlacing)
            return;

        this.IsPlacing = false;
        if (!silent)
            this.monitor.Log("Minecart placement cancelled.", LogLevel.Info);
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!this.IsPlacing || !Context.IsWorldReady)
            return;

        // Only consume Minecart Network's own placement controls. Other input,
        // especially movement, must continue to reach the game normally.
        if (e.Button is SButton.Escape or SButton.MouseRight or SButton.ControllerB)
        {
            this.helper.Input.Suppress(e.Button);
            this.Cancel();
            return;
        }

        if (e.Button == SButton.T)
        {
            this.helper.Input.Suppress(e.Button);
            this.HasTracks = !this.HasTracks;
            this.monitor.Log($"Tracks: {(this.HasTracks ? "ON" : "OFF")}.", LogLevel.Info);
            return;
        }

        if (e.Button == SButton.H)
        {
            this.helper.Input.Suppress(e.Button);
            this.HasWallHole = !this.HasWallHole;
            this.monitor.Log($"Wall hole: {(this.HasWallHole ? "ON" : "OFF")}.", LogLevel.Info);
            return;
        }

        if (e.Button is not (SButton.MouseLeft or SButton.ControllerA))
            return;

        this.helper.Input.Suppress(e.Button);

        Point tile = this.GetPreviewTile();
        if (!this.CanPlaceAt(Game1.currentLocation, tile.X, tile.Y, out string reason))
        {
            this.monitor.Log($"Can't place a minecart here: {reason}", LogLevel.Warn);
            Game1.playSound("cancel");
            return;
        }

        Point warpTile = new(tile.X, tile.Y + 1);
        MinecartStation station = this.stations.AddPlaced(
            this.PendingName,
            this.PendingCategory,
            Game1.currentLocation.NameOrUniqueName,
            tile.X,
            tile.Y,
            warpTile.X,
            warpTile.Y,
            this.HasTracks,
            this.HasWallHole
        );

        Game1.playSound("coin");
        this.monitor.Log(
            $"Placed station '{station.Name}' [{station.Id[..8]}] at {station.LocationName} {tile.X},{tile.Y}; arrival tile {warpTile.X},{warpTile.Y}.",
            LogLevel.Info
        );
        this.IsPlacing = false;
    }

    public void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!this.IsPlacing || e.NewMenu is null)
            return;

        // Input suppression stops the vanilla game from handling a button, but
        // SMAPI intentionally doesn't prevent other mods from handling the same
        // input. If any menu still opens while placement is active, close it.
        if (Game1.activeClickableMenu is not null)
        {
            this.monitor.Log(
                $"Blocked menu '{e.NewMenu.GetType().Name}' while minecart placement is active.",
                LogLevel.Trace
            );
            Game1.exitActiveMenu();
        }
    }

    public Point GetPreviewTile()
    {
        Vector2 tile = this.helper.Input.GetCursorPosition().GrabTile;
        return new Point((int)tile.X, (int)tile.Y);
    }

    public bool CanPlaceAt(GameLocation location, int tileX, int tileY, out string reason)
    {
        reason = "";

        Point[] requiredTiles =
        {
            new(tileX, tileY),
            new(tileX + 1, tileY),
            new(tileX, tileY + 1)
        };

        int width = location.Map.Layers[0].LayerWidth;
        int height = location.Map.Layers[0].LayerHeight;

        foreach (Point tile in requiredTiles)
        {
            if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
            {
                reason = "the station would be outside the map";
                return false;
            }

            Vector2 key = new(tile.X, tile.Y);

            if (location.objects.ContainsKey(key))
            {
                reason = "an object occupies one of the required tiles";
                return false;
            }

            if (location.terrainFeatures.ContainsKey(key))
            {
                reason = "a terrain feature occupies one of the required tiles";
                return false;
            }

            if (Game1.player.TilePoint == tile)
            {
                reason = "the player is standing on one of the required tiles";
                return false;
            }
        }

        var buildingsLayer = location.Map.GetLayer("Buildings");
        if (buildingsLayer is not null)
        {
            foreach (Point tile in requiredTiles)
            {
                if (buildingsLayer.Tiles[tile.X, tile.Y] is not null)
                {
                    reason = "the map has a building or wall tile there";
                    return false;
                }
            }
        }

        foreach (MinecartStation station in this.stations.Stations)
        {
            if (!station.HasPhysicalMinecart
                || !station.LocationName.Equals(location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
                continue;

            int existingX = station.VisualTileX!.Value;
            int existingY = station.VisualTileY!.Value;

            bool overlaps = tileY == existingY
                && (tileX == existingX || tileX == existingX + 1 || tileX + 1 == existingX);

            if (overlaps)
            {
                reason = $"it overlaps station '{station.Name}'";
                return false;
            }
        }

        return true;
    }
}
