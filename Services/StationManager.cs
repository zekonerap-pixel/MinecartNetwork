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

    public MinecartStation AddAtPlayer(
        string name,
        string category,
        bool hasTracks = true,
        bool hasWallHole = false,
        bool useAutomaticCategory = false)
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
            UseAutomaticCategory = useAutomaticCategory,
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

    public MinecartStation AddPlaced(
        string name,
        string category,
        string locationName,
        int cartTileX,
        int cartTileY,
        int warpTileX,
        int warpTileY,
        bool hasTracks,
        bool hasWallHole,
        bool useAutomaticCategory = false)
    {
        if (!Context.IsWorldReady)
            throw new InvalidOperationException("A save must be loaded before creating a station.");

        var station = new MinecartStation
        {
            Name = name.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim(),
            UseAutomaticCategory = useAutomaticCategory,
            LocationName = locationName,
            TileX = warpTileX,
            TileY = warpTileY,
            FacingDirection = 0,
            VisualTileX = cartTileX,
            VisualTileY = cartTileY,
            HasTracks = hasTracks,
            HasWallHole = hasWallHole,
            CreatedByPlayerId = Game1.player.UniqueMultiplayerID
        };

        this.Data.Stations.Add(station);
        this.Save();
        return station;
    }

    public bool UpdateDetails(string id, string name, string category)
    {
        MinecartStation? station = this.GetById(id);
        if (station is null)
            return false;

        station.Name = string.IsNullOrWhiteSpace(name) ? station.Name : name.Trim();
        station.Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
        station.UseAutomaticCategory = false;
        this.Save();
        return true;
    }

    public bool SetAutomaticCategory(string id, bool enabled)
    {
        MinecartStation? station = this.GetById(id);
        if (station is null)
            return false;

        station.UseAutomaticCategory = enabled;
        this.Save();
        return true;
    }

    public bool MovePlaced(
        string id,
        string locationName,
        int cartTileX,
        int cartTileY,
        int warpTileX,
        int warpTileY,
        bool hasTracks,
        bool hasWallHole)
    {
        MinecartStation? station = this.GetById(id);
        if (station is null || !station.HasPhysicalMinecart)
            return false;

        station.LocationName = locationName;
        station.VisualTileX = cartTileX;
        station.VisualTileY = cartTileY;
        station.TileX = warpTileX;
        station.TileY = warpTileY;
        station.FacingDirection = 0;
        station.HasTracks = hasTracks;
        station.HasWallHole = hasWallHole;
        this.Save();
        return true;
    }

    public IReadOnlyList<MinecartStation> FindMatches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<MinecartStation>();

        string value = query.Trim();

        return this.Data.Stations
            .Where(station =>
                station.Id.Equals(value, StringComparison.OrdinalIgnoreCase)
                || (value.Length >= 4 && station.Id.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                || station.Name.Equals(value, StringComparison.OrdinalIgnoreCase)
                || $"{station.Category} {station.Name}".Equals(value, StringComparison.OrdinalIgnoreCase)
                || $"{station.Name} {station.Category}".Equals(value, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public MinecartStation? Find(string idOrName)
    {
        IReadOnlyList<MinecartStation> matches = this.FindMatches(idOrName);
        return matches.Count == 1 ? matches[0] : null;
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

    private MinecartStation? GetById(string id)
    {
        return this.Data.Stations.FirstOrDefault(candidate =>
            candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
