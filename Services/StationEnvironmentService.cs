using Microsoft.Xna.Framework;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace MinecartNetwork.Services;

public sealed class StationEnvironmentService
{
    private readonly IMonitor monitor;

    public StationEnvironmentService(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    public List<RemovedWorldObject> Prepare(
        GameLocation location,
        IEnumerable<Point> constructionTiles)
    {
        var removed = new List<RemovedWorldObject>();

        foreach (Point tile in constructionTiles.Distinct())
        {
            Vector2 key = new(tile.X, tile.Y);
            if (!location.objects.TryGetValue(key, out SObject? obj) || !this.CanSafelyClear(obj))
                continue;

            removed.Add(new RemovedWorldObject
            {
                TileX = tile.X,
                TileY = tile.Y,
                QualifiedItemId = obj.QualifiedItemId,
                Stack = Math.Max(1, obj.Stack),
                Quality = obj.Quality,
                WasSpawnedObject = obj.IsSpawnedObject
            });

            location.objects.Remove(key);
        }

        if (removed.Count > 0)
        {
            this.monitor.Log(
                $"Minecart construction cleared {removed.Count} reversible world object(s) in {location.NameOrUniqueName}.",
                LogLevel.Trace
            );
        }

        return removed;
    }

    public bool CanRestore(MinecartStation station, out string? error)
    {
        error = null;
        if (station.ClearedObjects.Count == 0)
            return true;

        GameLocation? location = Game1.getLocationFromName(station.LocationName);
        if (location is null)
        {
            error = $"Location '{station.LocationName}' no longer exists, so its cleared environment can't be restored.";
            return false;
        }

        foreach (RemovedWorldObject snapshot in station.ClearedObjects)
        {
            Vector2 tile = new(snapshot.TileX, snapshot.TileY);
            if (location.objects.ContainsKey(tile))
            {
                error = $"Tile {snapshot.TileX},{snapshot.TileY} is occupied, so the original environment can't be restored safely.";
                return false;
            }
        }

        return true;
    }

    public bool Restore(MinecartStation station, out string? error)
    {
        if (!this.CanRestore(station, out error))
            return false;

        if (station.ClearedObjects.Count == 0)
            return true;

        GameLocation location = Game1.getLocationFromName(station.LocationName)!;

        try
        {
            foreach (RemovedWorldObject snapshot in station.ClearedObjects)
            {
                SObject obj = ItemRegistry.Create<SObject>(
                    snapshot.QualifiedItemId,
                    Math.Max(1, snapshot.Stack),
                    snapshot.Quality
                );
                obj.TileLocation = new Vector2(snapshot.TileX, snapshot.TileY);
                obj.IsSpawnedObject = snapshot.WasSpawnedObject;
                location.objects[obj.TileLocation] = obj;
            }

            this.monitor.Log(
                $"Restored {station.ClearedObjects.Count} world object(s) previously cleared by station '{station.Name}'.",
                LogLevel.Trace
            );
            station.ClearedObjects.Clear();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            this.monitor.Log($"Failed restoring environment for station '{station.Name}': {ex}", LogLevel.Error);
            return false;
        }
    }

    private bool CanSafelyClear(SObject obj)
    {
        if (obj.bigCraftable.Value)
            return false;

        return obj.IsSpawnedObject
            || obj.isForage()
            || obj.Category == SObject.litterCategory;
    }
}
