using System.Collections;
using System.Reflection;
using MinecartNetwork.Models;
using StardewModdingAPI;

namespace MinecartNetwork.Services;

/// <summary>Mirrors custom stations into Stardew Valley's native Data/Minecarts asset.</summary>
public sealed class MinecartDataSyncService
{
    public const string MinecartAssetName = "Data/Minecarts";
    public const string DefaultNetworkId = "Default";
    public const string ManagedDestinationPrefix = "SrZek.MinecartNetwork_";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    public MinecartDataSyncService(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public static string GetDestinationId(MinecartStation station)
        => $"{ManagedDestinationPrefix}{station.Id}";

    public static bool IsManagedDestinationId(string? destinationId)
        => !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(ManagedDestinationPrefix, StringComparison.OrdinalIgnoreCase);

    public bool Sync(IReadOnlyList<MinecartStation> stations)
    {
        try
        {
            object data = this.helper.GameContent.Load<object>(MinecartAssetName);
            object? network = this.GetNetwork(data, DefaultNetworkId);
            if (network is null)
            {
                this.monitor.Log(
                    $"Couldn't find minecart network '{DefaultNetworkId}' in {MinecartAssetName}; custom destinations weren't synced.",
                    LogLevel.Warn
                );
                return false;
            }

            object? destinations = this.GetMemberValue(network, "Destinations");
            if (destinations is null)
            {
                this.monitor.Log(
                    $"Minecart network '{DefaultNetworkId}' has no Destinations collection; custom destinations weren't synced.",
                    LogLevel.Warn
                );
                return false;
            }

            List<object> currentEntries = this.EnumerateDestinationValues(destinations).ToList();
            object? sample = currentEntries.FirstOrDefault(entry =>
                    !IsManagedDestinationId(this.GetString(entry, "Id")))
                ?? currentEntries.FirstOrDefault();

            this.RemoveManagedDestinations(destinations);

            List<MinecartStation> enabledStations = stations
                .Where(station => station.IsEnabled)
                .ToList();

            if (enabledStations.Count == 0)
            {
                this.monitor.Log(
                    $"Synced 0 custom minecart destination(s) into {MinecartAssetName}/{DefaultNetworkId}.",
                    LogLevel.Trace
                );
                return true;
            }

            if (sample is null)
            {
                this.monitor.Log(
                    $"Couldn't determine the native minecart destination model type in {MinecartAssetName}; custom destinations weren't synced.",
                    LogLevel.Warn
                );
                return false;
            }

            int added = 0;
            foreach (MinecartStation station in enabledStations)
            {
                object? destination = this.CreateDestination(sample, station);
                if (destination is null)
                    continue;

                string destinationId = GetDestinationId(station);
                if (this.AddDestination(destinations, destinationId, destination))
                    added++;
            }

            this.monitor.Log(
                $"Synced {added} custom minecart destination(s) into {MinecartAssetName}/{DefaultNetworkId}.",
                added == enabledStations.Count ? LogLevel.Trace : LogLevel.Warn
            );
            return added == enabledStations.Count;
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed syncing custom stations into {MinecartAssetName}: {ex}", LogLevel.Warn);
            return false;
        }
    }

    private object? CreateDestination(object sample, MinecartStation station)
    {
        Type destinationType = sample.GetType();
        object? destination;

        try
        {
            destination = Activator.CreateInstance(destinationType);
        }
        catch (Exception ex)
        {
            this.monitor.Log(
                $"Couldn't create native minecart destination model '{destinationType.FullName}': {ex.Message}",
                LogLevel.Warn
            );
            return null;
        }

        if (destination is null)
            return null;

        if (!this.TrySetMemberValue(destination, "Id", GetDestinationId(station))
            || !this.TrySetMemberValue(destination, "DisplayName", station.Name)
            || !this.TrySetMemberValue(destination, "TargetLocation", station.LocationName))
        {
            this.monitor.Log(
                $"Native minecart destination model '{destinationType.FullName}' is missing a required writable member.",
                LogLevel.Warn
            );
            return null;
        }

        object? sampleTile = this.GetMemberValue(sample, "TargetTile");
        object? targetTile = this.CreateMemberValue(destination, "TargetTile", sampleTile);
        if (targetTile is null
            || !this.TrySetMemberValue(targetTile, "X", station.TileX)
            || !this.TrySetMemberValue(targetTile, "Y", station.TileY)
            || !this.TrySetMemberValue(destination, "TargetTile", targetTile))
        {
            this.monitor.Log(
                $"Couldn't create TargetTile for native minecart destination '{station.Name}'.",
                LogLevel.Warn
            );
            return null;
        }

        this.TrySetDirection(destination, "TargetDirection", station.FacingDirection);
        this.TrySetMemberValue(destination, "Price", 0, required: false);
        this.TrySetMemberValue(destination, "Condition", null, required: false);

        return destination;
    }

    private object? CreateMemberValue(object instance, string memberName, object? sampleValue)
    {
        Type? memberType = this.GetMemberType(instance.GetType(), memberName);
        if (memberType is null)
            return null;

        Type targetType = Nullable.GetUnderlyingType(memberType) ?? memberType;

        try
        {
            if (sampleValue is not null && targetType.IsInstanceOfType(sampleValue))
                return Activator.CreateInstance(sampleValue.GetType());

            return Activator.CreateInstance(targetType);
        }
        catch
        {
            return null;
        }
    }

    private bool AddDestination(object destinations, string destinationId, object destination)
    {
        if (destinations is IDictionary dictionary)
        {
            dictionary[destinationId] = destination;
            return true;
        }

        if (destinations is IList list)
        {
            list.Add(destination);
            return true;
        }

        MethodInfo? addMethod = destinations.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "Add" && method.GetParameters().Length == 1);
        if (addMethod is null)
            return false;

        addMethod.Invoke(destinations, new[] { destination });
        return true;
    }

    private void RemoveManagedDestinations(object destinations)
    {
        if (destinations is IDictionary dictionary)
        {
            var keysToRemove = new List<object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is not null
                    && IsManagedDestinationId(this.GetString(entry.Value, "Id"))
                    && entry.Key is not null)
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            foreach (object key in keysToRemove)
                dictionary.Remove(key);
            return;
        }

        if (destinations is IList list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object? entry = list[i];
                if (entry is not null && IsManagedDestinationId(this.GetString(entry, "Id")))
                    list.RemoveAt(i);
            }
        }
    }

    private IEnumerable<object> EnumerateDestinationValues(object destinations)
    {
        if (destinations is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is not null)
                    yield return entry.Value;
            }
            yield break;
        }

        if (destinations is not IEnumerable entries)
            yield break;

        foreach (object? entry in entries)
        {
            if (entry is null)
                continue;

            object? value = this.GetMemberValue(entry, "Value");
            yield return value ?? entry;
        }
    }

    private object? GetNetwork(object data, string networkId)
    {
        if (data is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key?.ToString()?.Equals(networkId, StringComparison.OrdinalIgnoreCase) == true)
                    return entry.Value;
            }
            return null;
        }

        if (data is not IEnumerable entries)
            return null;

        foreach (object? entry in entries)
        {
            if (entry is null)
                continue;

            object? key = this.GetMemberValue(entry, "Key");
            if (key?.ToString()?.Equals(networkId, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            return this.GetMemberValue(entry, "Value");
        }

        return null;
    }

    private bool TrySetDirection(object instance, string memberName, int direction)
    {
        Type? memberType = this.GetMemberType(instance.GetType(), memberName);
        if (memberType is null)
            return false;

        Type targetType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        object value;

        if (targetType == typeof(string))
        {
            value = StationGeometry.NormalizeDirection(direction) switch
            {
                0 => "Up",
                1 => "Right",
                2 => "Down",
                3 => "Left",
                _ => "Down"
            };
        }
        else if (targetType.IsEnum)
        {
            value = Enum.ToObject(targetType, StationGeometry.NormalizeDirection(direction));
        }
        else
        {
            try
            {
                value = Convert.ChangeType(StationGeometry.NormalizeDirection(direction), targetType);
            }
            catch
            {
                return false;
            }
        }

        return this.TrySetMemberValue(instance, memberName, value);
    }

    private bool TrySetMemberValue(object instance, string memberName, object? value, bool required = true)
    {
        Type type = instance.GetType();
        PropertyInfo? property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        if (property is not null && property.CanWrite)
        {
            try
            {
                property.SetValue(instance, this.ConvertValue(value, property.PropertyType));
                return true;
            }
            catch
            {
                return false;
            }
        }

        FieldInfo? field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        if (field is not null)
        {
            try
            {
                field.SetValue(instance, this.ConvertValue(value, field.FieldType));
                return true;
            }
            catch
            {
                return false;
            }
        }

        return !required;
    }

    private object? ConvertValue(object? value, Type targetType)
    {
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is null)
            return null;

        if (effectiveType.IsInstanceOfType(value))
            return value;

        if (effectiveType.IsEnum)
        {
            if (value is string text)
                return Enum.Parse(effectiveType, text, ignoreCase: true);
            return Enum.ToObject(effectiveType, value);
        }

        return Convert.ChangeType(value, effectiveType);
    }

    private string? GetString(object instance, string memberName)
        => this.GetMemberValue(instance, memberName)?.ToString();

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

    private Type? GetMemberType(Type type, string memberName)
    {
        PropertyInfo? property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        if (property is not null)
            return property.PropertyType;

        FieldInfo? field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
        );
        return field?.FieldType;
    }
}
