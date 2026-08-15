using MinecartNetwork.Models;

namespace MinecartNetwork.Services;

public static class StationManagerVisualExtensions
{
    public static bool SetVisualStyleMode(this StationManager stations, string stationId, string mode)
    {
        MinecartStation? station = FindById(stations, stationId);
        if (station is null)
            return false;

        station.VisualStyleMode = ModConfig.NormalizeStationVisualMode(mode);
        stations.Save();
        return true;
    }

    public static bool SetMinecartVisualStyle(this StationManager stations, string stationId, string style)
    {
        MinecartStation? station = FindById(stations, stationId);
        if (station is null)
            return false;

        station.MinecartVisualStyle = ModConfig.NormalizeStationVisualStyle(style);
        station.VisualStyleMode = ModConfig.StationVisualModeCustom;
        stations.Save();
        return true;
    }

    public static bool SetEntranceVisualStyle(this StationManager stations, string stationId, string style)
    {
        MinecartStation? station = FindById(stations, stationId);
        if (station is null)
            return false;

        station.EntranceVisualStyle = ModConfig.NormalizeStationVisualStyle(style);
        station.VisualStyleMode = ModConfig.StationVisualModeCustom;
        stations.Save();
        return true;
    }

    public static bool SetTrackVisualStyle(this StationManager stations, string stationId, string style)
    {
        MinecartStation? station = FindById(stations, stationId);
        if (station is null)
            return false;

        station.TrackVisualStyle = ModConfig.NormalizeStationVisualStyle(style);
        station.VisualStyleMode = ModConfig.StationVisualModeCustom;
        stations.Save();
        return true;
    }

    private static MinecartStation? FindById(StationManager stations, string stationId)
    {
        return stations.Stations.FirstOrDefault(station =>
            station.Id.Equals(stationId, StringComparison.OrdinalIgnoreCase));
    }
}
