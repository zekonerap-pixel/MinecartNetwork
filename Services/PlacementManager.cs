using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
    private readonly LocationRegionService regions;
    private readonly ModConfig config;

    private string? movingStationId;

    public bool IsPlacing { get; private set; }
    public bool IsMoving => this.movingStationId is not null;
    public string PendingName { get; private set; } = "";
    public string PendingCategory { get; private set; } = "";
    public bool PendingUsesAutomaticCategory { get; private set; }
    public int PendingBuildCost { get; private set; }
    public bool HasTracks { get; private set; } = true;
    public bool HasWallHole { get; private set; } = true;
    public int StationDirection { get; private set; } = 2;
    public int TrackLength { get; private set; } = StationGeometry.DefaultTrackLength;

    public PlacementManager(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        LocationRegionService regions,
        ModConfig config)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.regions = regions;
        this.config = config;
    }

    public bool Begin(string name, string? category = null, int buildCost = 0)
    {
        if (!this.CanBeginPlacement())
            return false;

        int normalizedBuildCost = Math.Max(0, buildCost);
        if (normalizedBuildCost > 0 && Game1.player.Money < normalizedBuildCost)
        {
            this.ShowInsufficientFunds(normalizedBuildCost);
            return false;
        }

        this.movingStationId = null;
        this.PendingBuildCost = normalizedBuildCost;
        this.PendingName = string.IsNullOrWhiteSpace(name) ? "Minecart" : name.Trim();
        this.PendingUsesAutomaticCategory = string.IsNullOrWhiteSpace(category) && this.config.AutoCategorizeNewStations;
        this.PendingCategory = this.PendingUsesAutomaticCategory
            ? this.regions.GetCategoryForLocation(Game1.currentLocation.NameOrUniqueName)
            : string.IsNullOrWhiteSpace(category) ? this.config.DefaultCategory : category.Trim();

        // Default layout mirrors a believable mine station:
        // entrance -> two one-tile rail sections -> one-tile minecart -> clear arrival tile.
        this.HasTracks = true;
        this.HasWallHole = true;
        this.StationDirection = 2;
        this.TrackLength = StationGeometry.DefaultTrackLength;
        this.IsPlacing = true;

        string categoryMode = this.PendingUsesAutomaticCategory ? $"auto: {this.PendingCategory}" : this.PendingCategory;
        string priceSuffix = this.PendingBuildCost > 0 ? $" | pending cost {this.PendingBuildCost:N0}g" : "";
        this.monitor.Log(
            $"Placement started for '{this.PendingName}' ({categoryMode}){priceSuffix}. Left click places; R rotates; Q/E or controller shoulders change track length; T toggles tracks; H toggles mine entrance; right click/Escape cancels.",
            LogLevel.Info
        );
        return true;
    }

    public bool BeginMove(MinecartStation station)
    {
        if (!station.HasPhysicalMinecart)
        {
            this.monitor.Log("Only physical minecart stations can be moved.", LogLevel.Warn);
            return false;
        }

        if (!this.CanBeginPlacement())
            return false;

        this.movingStationId = station.Id;
        this.PendingBuildCost = 0;
        this.PendingName = station.Name;
        this.PendingUsesAutomaticCategory = station.UseAutomaticCategory;
        this.PendingCategory = this.PendingUsesAutomaticCategory
            ? this.regions.GetCategoryForLocation(station.LocationName)
            : station.Category;
        this.HasTracks = station.HasTracks;
        this.HasWallHole = station.HasWallHole;
        this.StationDirection = StationGeometry.NormalizeDirection(station.StationDirection);
        this.TrackLength = Math.Clamp(
            station.TrackLength,
            StationGeometry.MinTrackLength,
            StationGeometry.MaxTrackLength
        );
        this.IsPlacing = true;

        this.monitor.Log(
            $"Moving station '{station.Name}'. Its direction and track length can be changed before confirming; cancelling keeps the original station unchanged.",
            LogLevel.Info
        );
        return true;
    }

    public void Cancel(bool silent = false)
    {
        if (!this.IsPlacing)
            return;

        bool wasMoving = this.IsMoving;
        this.IsPlacing = false;
        this.movingStationId = null;
        this.PendingBuildCost = 0;

        if (!silent)
        {
            this.monitor.Log(
                wasMoving ? "Minecart move cancelled; original position kept." : "Minecart placement cancelled; no gold was charged.",
                LogLevel.Info
            );
        }
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!this.IsPlacing || !Context.IsWorldReady)
            return;

        if (e.Button is SButton.Escape or SButton.MouseRight or SButton.ControllerB)
        {
            this.helper.Input.Suppress(e.Button);
            this.Cancel();
            return;
        }

        if (e.Button == SButton.R || (e.Button.TryGetController(out Buttons controller) && controller == Buttons.X))
        {
            this.helper.Input.Suppress(e.Button);
            this.RotateClockwise();
            return;
        }

        if (e.Button == SButton.Q || (e.Button.TryGetController(out controller) && controller == Buttons.LeftShoulder))
        {
            this.helper.Input.Suppress(e.Button);
            this.AdjustTrackLength(-1);
            return;
        }

        if (e.Button == SButton.E || (e.Button.TryGetController(out controller) && controller == Buttons.RightShoulder))
        {
            this.helper.Input.Suppress(e.Button);
            this.AdjustTrackLength(1);
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
            this.monitor.Log($"Mine entrance: {(this.HasWallHole ? "ON" : "OFF")}.", LogLevel.Info);
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

        Point warpTile = StationGeometry.GetArrivalTile(tile.X, tile.Y, this.StationDirection);

        if (this.IsMoving)
        {
            string movingId = this.movingStationId!;
            bool moved = this.stations.MovePlaced(
                movingId,
                Game1.currentLocation.NameOrUniqueName,
                tile.X,
                tile.Y,
                warpTile.X,
                warpTile.Y,
                this.HasTracks,
                this.HasWallHole,
                this.StationDirection,
                this.TrackLength
            );

            if (!moved)
            {
                this.monitor.Log("The station being moved could no longer be found.", LogLevel.Error);
                Game1.playSound("cancel");
                this.Cancel(silent: true);
                return;
            }

            string effectiveCategory = this.PendingUsesAutomaticCategory
                ? this.regions.GetCategoryForLocation(Game1.currentLocation.NameOrUniqueName)
                : this.PendingCategory;
            Game1.playSound("coin");
            this.monitor.Log(
                $"Moved station '{this.PendingName}' to {Game1.currentLocation.NameOrUniqueName} {tile.X},{tile.Y}; arrival {warpTile.X},{warpTile.Y}; direction {this.StationDirection}; tracks {this.TrackLength}; category {effectiveCategory}.",
                LogLevel.Info
            );
            this.IsPlacing = false;
            this.movingStationId = null;
            this.PendingBuildCost = 0;
            return;
        }

        int buildCost = this.PendingBuildCost;
        if (buildCost > 0 && Game1.player.Money < buildCost)
        {
            this.ShowInsufficientFunds(buildCost);
            Game1.playSound("cancel");
            return;
        }

        string storedCategory = this.PendingUsesAutomaticCategory
            ? this.regions.GetCategoryForLocation(Game1.currentLocation.NameOrUniqueName)
            : this.PendingCategory;

        MinecartStation station = this.stations.AddPlaced(
            this.PendingName,
            storedCategory,
            Game1.currentLocation.NameOrUniqueName,
            tile.X,
            tile.Y,
            warpTile.X,
            warpTile.Y,
            this.HasTracks,
            this.HasWallHole,
            useAutomaticCategory: this.PendingUsesAutomaticCategory,
            stationDirection: this.StationDirection,
            trackLength: this.TrackLength
        );

        if (buildCost > 0)
            Game1.player.Money -= buildCost;

        Game1.playSound("coin");
        this.monitor.Log(
            $"Placed station '{station.Name}' [{station.Id[..8]}] at {station.LocationName} {tile.X},{tile.Y}; arrival {warpTile.X},{warpTile.Y}; direction {station.StationDirection}; tracks {station.TrackLength}; category {storedCategory}{(station.UseAutomaticCategory ? " (auto)" : "")}; charged {buildCost:N0}g.",
            LogLevel.Info
        );
        this.IsPlacing = false;
        this.PendingBuildCost = 0;
    }

    public void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!this.IsPlacing || e.NewMenu is null)
            return;

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
        // Use the actual cursor tile instead of GrabTile. GrabTile is constrained by the
        // player's interaction reach, which makes the preview cling to the farmer while testing.
        Vector2 tile = this.helper.Input.GetCursorPosition().Tile;
        return new Point((int)tile.X, (int)tile.Y);
    }

    public bool CanPlaceAt(GameLocation location, int tileX, int tileY, out string reason)
    {
        reason = "";

        IReadOnlyList<Point> constructionTiles = StationGeometry.GetConstructionTiles(
            tileX,
            tileY,
            this.StationDirection,
            this.TrackLength,
            this.HasTracks,
            this.HasWallHole
        );
        Point arrivalTile = StationGeometry.GetArrivalTile(tileX, tileY, this.StationDirection);

        int width = location.Map.Layers[0].LayerWidth;
        int height = location.Map.Layers[0].LayerHeight;

        foreach (Point tile in constructionTiles.Append(arrivalTile))
        {
            if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
            {
                reason = "the station would extend outside the map";
                return false;
            }
        }

        // Tunnel, rails and cart form a construction corridor which may visually replace
        // the local environment. Only the tile in front of the cart must genuinely remain open.
        Vector2 arrivalVector = new(arrivalTile.X, arrivalTile.Y);
        if (location.IsTileOccupiedBy(
                arrivalVector,
                CollisionMask.Buildings
                    | CollisionMask.Furniture
                    | CollisionMask.Objects
                    | CollisionMask.Characters
                    | CollisionMask.TerrainFeatures,
                ignorePassables: CollisionMask.Flooring)
            || !location.isTilePassable(
                new xTile.Dimensions.Location(arrivalTile.X, arrivalTile.Y),
                Game1.viewport))
        {
            reason = "the arrival tile at the end of the minecart must be clear and walkable";
            return false;
        }

        HashSet<Point> candidateBody = constructionTiles.ToHashSet();
        foreach (MinecartStation station in this.stations.Stations)
        {
            if (!station.HasPhysicalMinecart
                || station.Id.Equals(this.movingStationId, StringComparison.OrdinalIgnoreCase)
                || !station.LocationName.Equals(location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
                continue;

            HashSet<Point> existingBody = StationGeometry.GetConstructionTiles(
                station.VisualTileX!.Value,
                station.VisualTileY!.Value,
                station.StationDirection,
                station.TrackLength,
                station.HasTracks,
                station.HasWallHole
            ).ToHashSet();
            Point existingArrival = StationGeometry.GetArrivalTile(
                station.VisualTileX.Value,
                station.VisualTileY.Value,
                station.StationDirection
            );

            if (candidateBody.Overlaps(existingBody)
                || candidateBody.Contains(existingArrival)
                || existingBody.Contains(arrivalTile)
                || existingArrival == arrivalTile)
            {
                reason = $"it overlaps station '{station.Name}'";
                return false;
            }
        }

        return true;
    }

    private void RotateClockwise()
    {
        this.StationDirection = (this.StationDirection + 1) % 4;
        this.monitor.Log($"Station direction: {this.GetDirectionName(this.StationDirection)}.", LogLevel.Info);
        Game1.playSound("shwip");
    }

    private void AdjustTrackLength(int delta)
    {
        int next = Math.Clamp(
            this.TrackLength + delta,
            StationGeometry.MinTrackLength,
            StationGeometry.MaxTrackLength
        );
        if (next == this.TrackLength)
            return;

        this.TrackLength = next;
        this.monitor.Log($"Track sections between tunnel and minecart: {this.TrackLength}.", LogLevel.Info);
        Game1.playSound("shiny4");
    }

    private string GetDirectionName(int direction)
    {
        return StationGeometry.NormalizeDirection(direction) switch
        {
            0 => "up",
            1 => "right",
            2 => "down",
            3 => "left",
            _ => "down"
        };
    }

    private void ShowInsufficientFunds(int cost)
    {
        string message = this.helper.Translation.Get("management.insufficient-funds", new
        {
            cost = Math.Max(0, cost).ToString("N0")
        }).ToString();
        this.monitor.Log(message, LogLevel.Warn);
        Game1.showRedMessage(message);
    }

    private bool CanBeginPlacement()
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
        {
            this.monitor.Log("You can only start minecart placement while the player is free in a loaded save.", LogLevel.Warn);
            return false;
        }

        if (!Context.IsMainPlayer)
        {
            this.monitor.Log("Only the host can place or move custom minecarts in this alpha.", LogLevel.Warn);
            return false;
        }

        return true;
    }
}
