using Microsoft.Xna.Framework;
using MinecartNetwork.Menus;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class InteractionManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly TeleportService teleport;
    private readonly PlacementManager placement;

    public InteractionManager(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        TeleportService teleport,
        PlacementManager placement)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.teleport = teleport;
        this.placement = placement;
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady
            || this.placement.IsPlacing
            || Game1.activeClickableMenu is not null
            || !e.Button.IsActionButton())
            return;

        MinecartStation? station = this.GetFacedStation();
        if (station is null)
            return;

        this.helper.Input.Suppress(e.Button);
        Game1.playSound("shwip");
        Game1.activeClickableMenu = new MinecartMenu(
            this.helper,
            this.monitor,
            this.stations,
            this.teleport,
            station
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

        return this.stations.Stations.FirstOrDefault(station =>
            station.IsEnabled
            && station.HasPhysicalMinecart
            && station.LocationName.Equals(locationName, StringComparison.OrdinalIgnoreCase)
            && station.VisualTileY == target.Y
            && (station.VisualTileX == target.X || station.VisualTileX + 1 == target.X));
    }
}
