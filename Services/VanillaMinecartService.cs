using System.Collections;
using System.Reflection;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class VanillaMinecartService
{
    private const string MinecartAssetName = "Data/Minecarts";
    private const string DefaultNetworkId = "Default";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    public VanillaMinecartService(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public bool IsDefaultNetworkUnlocked()
    {
        try
        {
            object? network = this.GetNetwork(DefaultNetworkId);
            if (network is null)
                return false;

            string? unlockCondition = this.GetString(network, "UnlockCondition");
            return string.IsNullOrWhiteSpace(unlockCondition) || GameStateQuery.CheckConditions(unlockCondition);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed checking vanilla minecart unlock state: {ex}", LogLevel.Warn);
            return false;
        }
    }

    public IReadOnlyList<VanillaMinecartDestination> GetAvailableDefaultDestinations()
    {
        var result = new List<VanillaMinecartDestination>();

        try
        {
            object? network = this.GetNetwork(DefaultNetworkId);
            if (network is null)
                return result;

            string? unlockCondition = this.GetString(network, "UnlockCondition");
            if (!string.IsNullOrWhiteSpace(unlockCondition) && !GameStateQuery.CheckConditions(unlockCondition))
                return result;

            object? destinationsValue = this.GetMemberValue(network, "Destinations");
            if (destinationsValue is not IEnumerable destinations)
                return result;

            foreach (object? rawDestination in destinations)
            {
                if (rawDestination is null)
                    continue;

                string? condition = this.GetString(rawDestination, "Condition");
                if (!string.IsNullOrWhiteSpace(condition) && !GameStateQuery.CheckConditions(condition))
                    continue;

                int price = this.GetInt(rawDestination, "Price") ?? 0;
                if (price > 0)
                {
                    this.monitor.Log(
                        $"Skipping priced minecart destination '{this.GetString(rawDestination, "Id") ?? "unknown"}' in the unified menu for now.",
                        LogLevel.Trace
                    );
                    continue;
                }

                string id = this.GetString(rawDestination, "Id") ?? "Unknown";
                string targetLocation = this.GetString(rawDestination, "TargetLocation") ?? "";
                object? targetTile = this.GetMemberValue(rawDestination, "TargetTile");
                int tileX = targetTile is null ? 0 : this.GetInt(targetTile, "X") ?? 0;
                int tileY = targetTile is null ? 0 : this.GetInt(targetTile, "Y") ?? 0;
                int direction = this.ParseDirection(this.GetMemberValue(rawDestination, "TargetDirection"));

                if (string.IsNullOrWhiteSpace(targetLocation))
                    continue;

                result.Add(new VanillaMinecartDestination
                {
                    Id = id,
                    Name = this.GetDisplayName(id, targetLocation),
                    Category = this.GetCategory(id, targetLocation),
                    TargetLocation = targetLocation,
                    TargetTileX = tileX,
                    TargetTileY = tileY,
                    TargetDirection = direction
                });
            }
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed reading vanilla minecart destinations from {MinecartAssetName}: {ex}", LogLevel.Warn);
        }

        return result;
    }

    public string GetDisplayName(string? destinationId)
    {
        if (string.IsNullOrWhiteSpace(destinationId))
            return this.helper.Translation.Get("vanilla.minecart");

        VanillaMinecartDestination? destination = this.GetAvailableDefaultDestinations()
            .FirstOrDefault(entry => entry.Id.Equals(destinationId, StringComparison.OrdinalIgnoreCase));

        return destination?.Name ?? this.GetDisplayName(destinationId, destinationId);
    }

    public bool TryWarp(VanillaMinecartDestination destination, out string? error)
    {
        error = null;

        if (!Context.IsWorldReady)
        {
            error = "No save is currently loaded.";
            return false;
        }

        GameLocation? location = Game1.getLocationFromName(destination.TargetLocation);
        if (location is null)
        {
            error = $"Location '{destination.TargetLocation}' no longer exists.";
            return false;
        }

        try
        {
            Game1.warpFarmer(
                destination.TargetLocation,
                destination.TargetTileX,
                destination.TargetTileY,
                destination.TargetDirection
            );
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            this.monitor.Log($"Failed to warp to vanilla minecart destination '{destination.Id}': {ex}", LogLevel.Error);
            return false;
        }
    }

    private object? GetNetwork(string networkId)
    {
        object data = this.helper.GameContent.Load<object>(MinecartAssetName);

        if (data is IDictionary dictionary)
            return dictionary.Contains(networkId) ? dictionary[networkId] : null;

        if (data is not IEnumerable entries)
            return null;

        foreach (object? entry in entries)
        {
            if (entry is null)
                continue;

            object? key = this.GetMemberValue(entry, "Key");
            if (!string.Equals(key?.ToString(), networkId, StringComparison.OrdinalIgnoreCase))
                continue;

            return this.GetMemberValue(entry, "Value");
        }

        return null;
    }

    private string GetDisplayName(string id, string targetLocation)
    {
        string key = id.ToLowerInvariant() switch
        {
            "bus" => "vanilla.bus",
            "mines" => "vanilla.mines",
            "town" => "vanilla.town",
            "quarry" => "vanilla.quarry",
            _ => ""
        };

        if (!string.IsNullOrEmpty(key))
            return this.helper.Translation.Get(key);

        return string.IsNullOrWhiteSpace(id) ? targetLocation : id;
    }

    private string GetCategory(string id, string targetLocation)
    {
        string normalizedId = id.ToLowerInvariant();
        string normalizedLocation = targetLocation.ToLowerInvariant();

        if (normalizedId == "town" || normalizedLocation == "town")
            return this.helper.Translation.Get("region.town");

        if (normalizedId == "mines" || normalizedLocation.Contains("mine"))
            return this.helper.Translation.Get("region.mines");

        if (normalizedId == "bus" || normalizedLocation == "busstop")
            return this.helper.Translation.Get("region.farm");

        if (normalizedId == "quarry" || normalizedLocation == "mountain")
            return this.helper.Translation.Get("region.mountain");

        return this.helper.Translation.Get("region.other");
    }

    private int ParseDirection(object? value)
    {
        if (value is null)
            return 2;

        if (value is int intValue && intValue is >= 0 and <= 3)
            return intValue;

        return value.ToString()?.Trim().ToLowerInvariant() switch
        {
            "up" => 0,
            "right" => 1,
            "down" => 2,
            "left" => 3,
            _ => 2
        };
    }

    private string? GetString(object instance, string memberName)
    {
        return this.GetMemberValue(instance, memberName)?.ToString();
    }

    private int? GetInt(object instance, string memberName)
    {
        object? value = this.GetMemberValue(instance, memberName);
        if (value is null)
            return null;

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private object? GetMemberValue(object instance, string memberName)
    {
        Type type = instance.GetType();

        PropertyInfo? property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        if (property is not null)
            return property.GetValue(instance);

        FieldInfo? field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        return field?.GetValue(instance);
    }
}
