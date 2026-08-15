using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;

namespace MinecartNetwork.Rendering;

public readonly record struct ResolvedStationVisualStyles(
    string MinecartStyle,
    string EntranceStyle,
    string TrackStyle
);

public sealed class StationVisualStyleResolver
{
    private readonly IModHelper helper;
    private readonly LocationRegionService regions;
    private readonly string[] availableStyles;

    public StationVisualStyleResolver(
        IModHelper helper,
        LocationRegionService regions)
    {
        this.helper = helper;
        this.regions = regions;
        this.availableStyles = MinecartVisualAssets.GetAvailableStyles(helper);
    }

    public StationVisualStyleResolver(
        IModHelper helper,
        LocationRegionService regions,
        ModConfig config)
        : this(helper, regions)
    {
        // The live defaults are read from StationVisualSettings so GMCM changes are reflected
        // immediately without replacing the resolver instance.
    }

    public IReadOnlyList<string> AvailableStyles => this.availableStyles;

    public ResolvedStationVisualStyles Resolve(MinecartStation? station)
    {
        ResolvedStationVisualStyles defaults = this.ResolveDefaults();
        if (station is null)
            return defaults;

        string mode = ModConfig.NormalizeStationVisualMode(station.VisualStyleMode);
        if (mode == ModConfig.StationVisualModeAutomatic)
        {
            string? automaticStyle = this.GetAutomaticStyle(station);
            if (automaticStyle is not null)
            {
                return new ResolvedStationVisualStyles(
                    automaticStyle,
                    automaticStyle,
                    automaticStyle
                );
            }

            return defaults;
        }

        if (mode != ModConfig.StationVisualModeCustom)
            return defaults;

        return new ResolvedStationVisualStyles(
            this.ResolveAvailable(station.MinecartVisualStyle, defaults.MinecartStyle),
            this.ResolveAvailable(station.EntranceVisualStyle, defaults.EntranceStyle),
            this.ResolveAvailable(station.TrackVisualStyle, defaults.TrackStyle)
        );
    }

    public string? GetAutomaticStyle(MinecartStation station)
    {
        string category = this.regions.GetCategoryForLocation(station.LocationName);
        string? candidate = null;

        if (this.CategoryEquals(category, "region.mines")
            || this.CategoryEquals(category, "region.mountain")
            || this.CategoryEquals(category, "region.desert"))
        {
            candidate = ModConfig.StationVisualMiner;
        }
        else if (this.CategoryEquals(category, "region.town"))
        {
            candidate = ModConfig.StationVisualIndustrial;
        }
        else if (this.CategoryEquals(category, "region.farm")
            || this.CategoryEquals(category, "region.forest")
            || this.CategoryEquals(category, "region.beach")
            || this.CategoryEquals(category, "region.island"))
        {
            candidate = ModConfig.StationVisualRustic;
        }

        if (candidate is null)
            return null;

        return this.TryGetAvailable(candidate, out string available)
            ? available
            : null;
    }

    public string GetStyleDisplayName(string? style)
    {
        string normalized = ModConfig.NormalizeStationVisualStyle(style);
        string? key = normalized switch
        {
            ModConfig.StationVisualLegacyCurrent => "config.station-style.current",
            ModConfig.StationVisualIndustrial => "config.station-style.industrial",
            ModConfig.StationVisualRustic => "config.station-style.rustic",
            ModConfig.StationVisualMiner => "config.station-style.miner",
            ModConfig.StationVisualCopper => "config.station-style.copper",
            ModConfig.StationVisualDarkIron => "config.station-style.dark-iron",
            ModConfig.StationVisualMoss => "config.station-style.moss",
            ModConfig.StationVisualCrystal => "config.station-style.crystal",
            _ => null
        };

        return key is null
            ? normalized
            : this.helper.Translation.Get(key).ToString();
    }

    public string GetModeDisplayName(string? mode)
    {
        return ModConfig.NormalizeStationVisualMode(mode) switch
        {
            ModConfig.StationVisualModeAutomatic => this.helper.Translation.Get("style.mode.automatic"),
            ModConfig.StationVisualModeCustom => this.helper.Translation.Get("style.mode.custom"),
            _ => this.helper.Translation.Get("style.mode.default")
        };
    }

    private ResolvedStationVisualStyles ResolveDefaults()
    {
        return new ResolvedStationVisualStyles(
            this.ResolveAvailable(StationVisualSettings.MinecartStyle, ModConfig.StationVisualLegacyCurrent),
            this.ResolveAvailable(StationVisualSettings.EntranceStyle, ModConfig.StationVisualLegacyCurrent),
            this.ResolveAvailable(StationVisualSettings.TrackStyle, ModConfig.StationVisualLegacyCurrent)
        );
    }

    private string ResolveAvailable(string? requested, string fallback)
    {
        if (this.TryGetAvailable(requested, out string available))
            return available;
        if (this.TryGetAvailable(fallback, out available))
            return available;

        return ModConfig.StationVisualLegacyCurrent;
    }

    private bool TryGetAvailable(string? requested, out string style)
    {
        string normalized = ModConfig.NormalizeStationVisualStyle(requested);
        style = this.availableStyles.FirstOrDefault(candidate =>
            candidate.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        return style.Length > 0;
    }

    private bool CategoryEquals(string category, string translationKey)
    {
        return category.Equals(
            this.helper.Translation.Get(translationKey).ToString(),
            StringComparison.OrdinalIgnoreCase
        );
    }
}
