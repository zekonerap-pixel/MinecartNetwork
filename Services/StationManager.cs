using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class StationManager
{
    private const string SaveKey = "minecart-network-data";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    public MinecartSaveData Data { get; private set; } = new();

    public StationManager(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public IReadOnlyList<MinecartStation> Stations => this.Data.Stations;

    public void Load()
    {
        if (!Context.IsMainPlayer)
        {
            this.Data = new MinecartSaveData();
            this.monitor.Log("Farmhand save-data sync is not implemented yet; custom stations are host-owned in this alpha.", LogLevel.Debug);
            return;
        }

        this.Data = this.helper.Data.ReadSaveData<MinecartSaveData>(SaveKey) ?? new MinecartSaveData();
        this.Data.Stations ??= new List<MinecartStation>();
        this.monitor.Log($"Loaded {this.Data.Stations.Count} custom minecart station(s).", LogLevel.Debug);
    }

    public void Save()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;

        this.helper.Data.WriteSaveData(SaveKey, this.Data);
        this.monitor.Log($"Saved {this.Data.Stations.Count} custom minecart station(s).", LogLevel.Trace);
    }

    public MinecartStation AddAtPlayer(string name, string category, bool hasTracks = true, bool hasWallHole = false)
    {
        if (!Context.IsWorldReady)
            throw new InvalidOperationException("A save must be loaded before creating a station.");

        string locationName = Game1.currentLocation.NameOrUniqueName;
        int x = Game1.player.TilePoint.X;
        int y = Game1.player.TilePoint.Y;

        var station = new MinecartStation
        {
            Name = name.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim(),
            LocationName = locationName,
            TileX = x,
            TileY = y,
            FacingDirection = Game1.player.FacingDirection,
            HasTracks = hasTracks,
            HasWallHole = hasWallHole,
            CreatedByPlayerId = Game1.player.UniqueMultiplayerID
        };

        this.Data.Stations.Add(station);
        this.Save();
        return station;
    }

    public MinecartStation? Find(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
            return null;

        string value = idOrName.Trim();

        return this.Data.Stations.FirstOrDefault(station =>
            station.Id.Equals(value, StringComparison.OrdinalIgnoreCase)
            || station.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    public bool Remove(string idOrName)
    {
        MinecartStation? station = this.Find(idOrName);
        if (station is null)
            return false;

        this.Data.Stations.Remove(station);
        this.Save();
        return true;
    }

    public void Clear()
    {
        this.Data = new MinecartSaveData();
    }
}
