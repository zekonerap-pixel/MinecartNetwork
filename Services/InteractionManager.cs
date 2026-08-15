using Microsoft.Xna.Framework;
using MinecartNetwork.Menus;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class InteractionManager
{
    private const int ActionCursor = 2;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly LocationRegionService regions;
    private readonly VanillaMinecartService vanillaMinecarts;
    private readonly TeleportService teleport;
    private readonly PlacementManager placement;
    private readonly ModConfig config;

    private bool wasHoveringMinecart;

    public InteractionManager(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        LocationRegionService regions,
        VanillaMinecartService vanillaMinecarts,
        TeleportService teleport,
        PlacementManager placement,
        ModConfig config)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.regions = regions;
        this.vanillaMinecarts = vanillaMinecarts;
        this.teleport = teleport;
        this.placement = placement;
        this.config = config;
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady
            || this.placement.IsPlacing
            || Game1.activeClickableMenu is not null)
            return;

        MinecartStation? hoveredStation = this.GetHoveredStation(requireReach: true);
        bool clickedSurface = e.Button == SButton.MouseLeft && hoveredStation is not null;

        if (!e.Button.IsActionButton() && !clickedSurface)
            return;

        MinecartStation? station = hoveredStation ?? this.GetFacedStation();
        if (station is null)
            return;

        this.helper.Input.Suppress(e.Button);
        this.OpenMenu(station);
    }

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady
            || this.placement.IsPlacing
            || Game1.activeClickableMenu is not null)
        {
            this.ResetHoverCursor();
            return;
        }

        bool hovering = this.GetHoveredStation(requireReach: true) is not null;
        if (hovering)
        {
            Game1.mouseCursor = ActionCursor;
            this.wasHoveringMinecart = true;
        }
        else
        {
            this.ResetHoverCursor();
        }
    }

    public MinecartStation? GetHoveredStation(bool requireReach)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
            return null;

        Vector2 cursor = this.helper.Input.GetCursorPosition().AbsolutePixels;
        int cursorX = (int)cursor.X;
        int cursorY = (int)cursor.Y;
        string locationName = Game1.currentLocation.NameOrUniqueName;

        foreach (MinecartStation station in this.stations.Stations)
        {
            if (!station.IsEnabled
                || !station.HasPhysicalMinecart
                || !station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase))
                continue;

            Rectangle hitbox = this.GetInteractionHitbox(station);
            if (!hitbox.Contains(cursorX, cursorY))
                continue;

            if (requireReach && !this.IsWithinReach(hitbox))
                continue;

            return station;
        }

        return null;
    }

    public Rectangle GetInteractionHitbox(MinecartStation station)
    {
        if (!station.HasPhysicalMinecart)
            return Rectangle.Empty;

        return StationGeometry.GetCartPixelBounds(
            station.VisualTileX!.Value,
            station.VisualTileY!.Value,
            station.StationDirection
        );
    }

    private MinecartStation? GetFacedStation()
    {
        Point target = Game1.player.TilePoint;

        switch (Game1.player.FacingDirection)
        {
            case 0:
                target.Y--;
                break;
            case 1:
                target.X++;
                break;
            case 2:
                target.Y++;
                break;
            case 3:
                target.X--;
                break;
            default:
                return null;
        }

        string locationName = Game1.currentLocation.NameOrUniqueName;
        Rectangle targetTile = new(
            target.X * Game1.tileSize,
            target.Y * Game1.tileSize,
            Game1.tileSize,
            Game1.tileSize
        );

        return this.stations.Stations.FirstOrDefault(station =>
            station.IsEnabled
            && station.HasPhysicalMinecart
            && station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase)
            && this.GetInteractionHitbox(station).Intersects(targetTile));
    }

    private bool IsWithinReach(Rectangle minecartHitbox)
    {
        Rectangle reach = Game1.player.GetBoundingBox();
        reach.Inflate(Game1.tileSize, Game1.tileSize);
        return reach.Intersects(minecartHitbox);
    }

    private void OpenMenu(MinecartStation station)
    {
        this.vanillaMinecarts.SelectNetwork(VanillaMinecartService.DefaultNetworkId);
        Game1.playSound("shwip");
        Game1.activeClickableMenu = new MinecartMenu(
            this.helper,
            this.monitor,
            this.stations,
            this.regions,
            this.vanillaMinecarts,
            this.teleport,
            this.placement,
            this.config,
            station.Name,
            excludedCustomStationId: station.Id,
            excludedVanillaDestinationId: MinecartDataSyncService.GetDestinationId(station)
        );
    }

    private void ResetHoverCursor()
    {
        if (!this.wasHoveringMinecart)
            return;

        Game1.mouseCursor = 0;
        this.wasHoveringMinecart = false;
    }
}
