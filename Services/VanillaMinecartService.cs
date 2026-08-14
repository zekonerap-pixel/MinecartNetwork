using System.Collections;
using System.Reflection;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class VanillaMinecartService
{
    private const string MinecartAssetName = "Data/Minecarts";
    public const string DefaultNetworkId = "Default";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly LocationRegionService regions;

    public string ActiveNetworkId { get; private set; } = DefaultNetworkId;

    public VanillaMinecartService(IModHelper helper, IMonitor monitor, LocationRegionService regions)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.regions = regions;
    }

    public void SelectNetwork(string? networkId)
    {
        this.ActiveNetworkId = string.IsNullOrWhiteSpace(networkId)
            ? DefaultNetworkId
            : networkId.Trim();
    }

    public bool IsDefaultNetworkUnlocked() => this.IsNetworkUnlocked(DefaultNetworkId);

    public bool IsNetworkUnlocked(string networkId)
    {
        try
        {
            object? network = this.GetNetwork(networkId);
            if (network is null)
                return false;

            string? unlockCondition = this.GetString(network, "UnlockCondition");
            return string.IsNullOrWhiteSpace(unlockCondition) || GameStateQuery.CheckConditions(unlockCondition);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed checking minecart network '{networkId}' unlock state: {ex}", LogLevel.Warn);
            return false;
        }
    }

    public IReadOnlyList<string> GetNetworkIds()
    {
        try
        {
            return this.EnumerateNetworks()
                .Select(entry => entry.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed reading minecart network IDs from {MinecartAssetName}: {ex}", LogLevel.Warn);
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<VanillaMinecartDestination> GetAvailableDefaultDestinations()
        => this.GetAvailableDestinations(this.ActiveNetworkId);

    public IReadOnlyList<VanillaMinecartDestination> GetAvailableDestinations(string networkId)
    {
        var result = new List<VanillaMinecartDestination>();

        try
        {
            object? network = this.GetNetwork(networkId);
            if (network is null)
                return result;

            string? unlockCondition = this.GetString(network, "UnlockCondition");
            if (!string.IsNullOrWhiteSpace(unlockCondition) && !GameStateQuery.CheckConditions(unlockCondition))
                return result;

            foreach (object rawDestination in this.GetRawDestinations(network))
            {
                if (!this.IsDestinationAvailable(rawDestination))
                    continue;

                int price = this.GetInt(rawDestination, "Price") ?? 0;
                if (price > 0)
                {
                    this.monitor.Log(
                        $"Skipping priced minecart destination '{this.GetString(rawDestination, "Id") ?? "unknown"}' from network '{networkId}' in the unified menu.",
                        LogLevel.Trace
                    );
                    continue;
                }

                VanillaMinecartDestination? destination = this.CreateDestination(networkId, rawDestination);
                if (destination is not null)
                    result.Add(destination);
            }
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed reading minecart network '{networkId}' from {MinecartAssetName}: {ex}", LogLevel.Warn);
        }

        return result;
    }

    public bool HasAvailablePricedDestinations(string networkId)
    {
        try
        {
            object? network = this.GetNetwork(networkId);
            if (network is null)
                return false;

            string? unlockCondition = this.GetString(network, "UnlockCondition");
            if (!string.IsNullOrWhiteSpace(unlockCondition) && !GameStateQuery.CheckConditions(unlockCondition))
                return false;

            return this.GetRawDestinations(network)
                .Any(destination => this.IsDestinationAvailable(destination)
                    && (this.GetInt(destination, "Price") ?? 0) > 0);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed checking priced destinations for minecart network '{networkId}': {ex}", LogLevel.Warn);
            return true;
        }
    }

    public string GetDisplayName(string? destinationId)
        => this.GetDisplayName(this.ActiveNetworkId, destinationId);

    public string GetDisplayName(string networkId, string? destinationId)
    {
        if (string.IsNullOrWhiteSpace(destinationId))
            return this.GetNetworkDisplayName(networkId);

        VanillaMinecartDestination? destination = this.GetAvailableDestinations(networkId)
            .FirstOrDefault(entry => entry.Id.Equals(destinationId, StringComparison.OrdinalIgnoreCase));

        return destination?.Name ?? this.GetFallbackDisplayName(destinationId, destinationId);
    }

    public string GetNetworkDisplayName(string networkId)
    {
        if (networkId.Equals(DefaultNetworkId, StringComparison.OrdinalIgnoreCase))
            return this.helper.Translation.Get("vanilla.minecart");

        return this.regions.HumanizeIdentifier(networkId);
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
            this.monitor.Log(
                $"Failed to warp to minecart destination '{destination.NetworkId}/{destination.Id}': {ex}",
                LogLevel.Error
            );
            return false;
        }
    }

    private VanillaMinecartDestination? CreateDestination(string networkId, object rawDestination)
    {
        string id = this.GetString(rawDestination, "Id") ?? "Unknown";
        string targetLocation = this.GetString(rawDestination, "TargetLocation") ?? "";
        object? targetTile = this.GetMemberValue(rawDestination, "TargetTile");
        int tileX = targetTile is null ? 0 : this.GetInt(targetTile, "X") ?? 0;
        int tileY = targetTile is null ? 0 : this.GetInt(targetTile, "Y") ?? 0;
        int direction = this.ParseDirection(this.GetMemberValue(rawDestination, "TargetDirection"));

        if (string.IsNullOrWhiteSpace(targetLocation))
            return null;

        string? rawDisplayName = this.GetString(rawDestination, "DisplayName");
        string displayName = this.GetFallbackDisplayName(id, targetLocation);
        if (!string.IsNullOrWhiteSpace(rawDisplayName)
            && !rawDisplayName.TrimStart().StartsWith('[', StringComparison.Ordinal))
        {
            displayName = rawDisplayName.Trim();
        }

        return new VanillaMinecartDestination
        {
            NetworkId = networkId,
            Id = id,
            Name = displayName,
            Category = this.regions.GetCategoryForDestination(id, targetLocation),
            TargetLocation = targetLocation,
            TargetTileX = tileX,
            TargetTileY = tileY,
            TargetDirection = direction
        };
    }

    private bool IsDestinationAvailable(object rawDestination)
    {
        string? condition = this.GetString(rawDestination, "Condition");
        return string.IsNullOrWhiteSpace(condition) || GameStateQuery.CheckConditions(condition);
    }

    private IEnumerable<object> GetRawDestinations(object network)
    {
        object? destinationsValue = this.GetMemberValue(network, "Destinations");
        if (destinationsValue is null)
            yield break;

        if (destinationsValue is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is not null)
                    yield return entry.Value;
            }
            yield break;
        }

        if (destinationsValue is not IEnumerable destinations)
            yield break;

        foreach (object? entry in destinations)
        {
            if (entry is null)
                continue;

            object? value = this.GetMemberValue(entry, "Value");
            yield return value ?? entry;
        }
    }

    private object? GetNetwork(string networkId)
    {
        foreach ((string Id, object Network) entry in this.EnumerateNetworks())
        {
            if (entry.Id.Equals(networkId, StringComparison.OrdinalIgnoreCase))
                return entry.Network;
        }

        return null;
    }

    private IEnumerable<(string Id, object Network)> EnumerateNetworks()
    {
        object data = this.helper.GameContent.Load<object>(MinecartAssetName);

        if (data is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is null || entry.Value is null)
                    continue;

                yield return (entry.Key.ToString() ?? "", entry.Value);
            }
            yield break;
        }

        if (data is not IEnumerable entries)
            yield break;

        foreach (object? entry in entries)
        {
            if (entry is null)
                continue;

            object? key = this.GetMemberValue(entry, "Key");
            object? value = this.GetMemberValue(entry, "Value");
            if (key is null || value is null)
                continue;

            yield return (key.ToString() ?? "", value);
        }
    }

    private string GetFallbackDisplayName(string id, string targetLocation)
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

        return this.regions.HumanizeIdentifier(string.IsNullOrWhiteSpace(id) ? targetLocation : id);
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
