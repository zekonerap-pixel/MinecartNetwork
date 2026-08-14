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

    /// <summary>
    /// Get every safe destination reachable through the unified network view.
    /// The active network and Default network are always considered; other networks are
    /// federated only when they're unlocked and don't expose an active paid destination.
    /// Equivalent stops shared by multiple networks are returned only once.
    /// </summary>
    public IReadOnlyList<VanillaMinecartDestination> GetAvailableDefaultDestinations()
        => this.GetAvailableFederatedDestinations();

    public IReadOnlyList<VanillaMinecartDestination> GetAvailableFederatedDestinations()
    {
        var result = new List<VanillaMinecartDestination>();
        var seenStops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string networkId in this.GetFederatedNetworkIds())
        {
            foreach (VanillaMinecartDestination destination in this.GetAvailableDestinations(networkId))
            {
                string stopKey = GetStopKey(destination);
                if (seenStops.Add(stopKey))
                    result.Add(destination);
            }
        }

        return result;
    }

    public IReadOnlyList<string> GetFederatedNetworkIds()
    {
        var result = new List<string>();

        IEnumerable<string> orderedNetworks = this.GetNetworkIds()
            .OrderBy(id => id.Equals(this.ActiveNetworkId, StringComparison.OrdinalIgnoreCase) ? 0
                : id.Equals(DefaultNetworkId, StringComparison.OrdinalIgnoreCase) ? 1
                : 2)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase);

        foreach (string networkId in orderedNetworks)
        {
            if (!this.IsNetworkUnlocked(networkId))
                continue;

            bool isActive = networkId.Equals(this.ActiveNetworkId, StringComparison.OrdinalIgnoreCase);
            bool isDefault = networkId.Equals(DefaultNetworkId, StringComparison.OrdinalIgnoreCase);

            // Preserve payment mechanics. The active network keeps the same behavior as before
            // (its free destinations can still be shown), and Default remains available because
            // MinecartNetwork mirrors custom stations into it. Foreign paid networks stay isolated
            // behind their original minecart menu instead of being exposed as free cross-network travel.
            if (!isActive && !isDefault && this.HasAvailablePricedDestinations(networkId))
            {
                this.monitor.Log(
                    $"Skipping minecart network '{networkId}' from federation because it contains an available priced destination.",
                    LogLevel.Trace
                );
                continue;
            }

            result.Add(networkId);
        }

        return result;
    }

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

        // Some mods expose a human-readable network label. Read it opportunistically
        // without taking a hard dependency on a specific game-data model version.
        object? network = this.GetNetwork(networkId);
        string? rawDisplayName = network is null ? null : this.GetString(network, "DisplayName");
        if (!string.IsNullOrWhiteSpace(rawDisplayName)
            && !rawDisplayName.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            return rawDisplayName.Trim();
        }

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
            && !rawDisplayName.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            displayName = rawDisplayName.Trim();
        }

        string? customStationId = null;
        if (MinecartDataSyncService.IsManagedDestinationId(id))
        {
            customStationId = id[MinecartDataSyncService.ManagedDestinationPrefix.Length..];
            if (string.IsNullOrWhiteSpace(customStationId))
                customStationId = null;
        }

        string category = this.GetDestinationCategory(networkId, id, targetLocation);

        // Content packs often include the region in the destination label itself
        // (e.g. "Ginger Island - Quarry"). Once the menu groups by that region the
        // prefix becomes visual noise, so trim it for native/mod destinations only.
        // User-created station names are never modified.
        if (customStationId is null)
            displayName = StripRedundantPrefix(displayName, category);

        return new VanillaMinecartDestination
        {
            NetworkId = networkId,
            Id = id,
            Name = displayName,
            Category = category,
            TargetLocation = targetLocation,
            TargetTileX = tileX,
            TargetTileY = tileY,
            TargetDirection = direction,
            CustomStationId = customStationId
        };
    }

    private string GetDestinationCategory(string networkId, string destinationId, string targetLocation)
    {
        if (networkId.Equals(DefaultNetworkId, StringComparison.OrdinalIgnoreCase))
            return this.regions.GetCategoryForDestination(destinationId, targetLocation);

        string normalizedNetwork = NormalizeIdentifier(networkId);

        // Ginger Island is a base-game region and already has localized names in our i18n.
        // Everything else is intentionally dynamic: the network ID/display name supplied
        // by the other mod becomes the region header without MinecartNetwork knowing that mod.
        if (normalizedNetwork.Contains("gingerisland", StringComparison.Ordinal)
            || normalizedNetwork.Equals("island", StringComparison.Ordinal))
        {
            return this.helper.Translation.Get("region.island");
        }

        return this.GetNetworkDisplayName(networkId);
    }

    private static string StripRedundantPrefix(string displayName, string category)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(category))
            return displayName;

        string[] separators = { " - ", " – ", " — ", ": " };
        foreach (string separator in separators)
        {
            string prefix = category + separator;
            if (!displayName.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                continue;

            string trimmed = displayName[prefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? displayName : trimmed;
        }

        return displayName;
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

    private static string GetStopKey(VanillaMinecartDestination destination)
    {
        return string.Join(
            "|",
            destination.TargetLocation.Trim(),
            destination.TargetTileX,
            destination.TargetTileY
        );
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

    private static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
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
