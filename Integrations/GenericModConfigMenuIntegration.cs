using MinecartNetwork.Rendering;
using StardewModdingAPI;

namespace MinecartNetwork.Integrations;

internal static class GenericModConfigMenuIntegration
{
    private const string ModId = "spacechase0.GenericModConfigMenu";

    public static void Register(
        IModHelper helper,
        IManifest manifest,
        Func<ModConfig> getConfig,
        Action reset,
        Action save)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModId);
        if (api is null)
            return;

        api.Register(
            manifest,
            reset: () =>
            {
                reset();

                ModConfig defaults = new();
                getConfig().MinecartVisualStyle = defaults.MinecartVisualStyle;
                getConfig().EntranceVisualStyle = defaults.EntranceVisualStyle;
                getConfig().TrackVisualStyle = defaults.TrackVisualStyle;
                StationVisualSettings.Apply(getConfig());
            },
            save
        );

        api.AddKeybind(
            manifest,
            getValue: () => getConfig().ManagementMenuKey,
            setValue: value => getConfig().ManagementMenuKey = value,
            name: () => helper.Translation.Get("config.management-key.name").ToString(),
            tooltip: () => helper.Translation.Get("config.management-key.tooltip").ToString(),
            fieldId: "ManagementMenuKey"
        );

        api.AddNumberOption(
            manifest,
            getValue: () => Math.Max(0, getConfig().StationBuildCost),
            setValue: value => getConfig().StationBuildCost = Math.Max(0, value),
            name: () => helper.Translation.Get("config.station-build-cost.name").ToString(),
            tooltip: () => helper.Translation.Get("config.station-build-cost.tooltip").ToString(),
            min: 0,
            max: 1000000,
            interval: 5000,
            formatValue: value => $"{value:N0}g",
            fieldId: "StationBuildCost"
        );

        string[] menuStyles =
        {
            ModConfig.MenuStyleStardew,
            ModConfig.MenuStyleBasic
        };

        // Discover complete sprite sets from assets/styles so adding a valid folder automatically
        // makes it available in all three independent selectors.
        string[] stationStyles = MinecartVisualAssets.GetAvailableStyles(helper);

        api.AddTextOption(
            manifest,
            getValue: () => ModConfig.NormalizeMenuStyle(getConfig().MenuStyle),
            setValue: value => getConfig().MenuStyle = ModConfig.NormalizeMenuStyle(value),
            name: () => helper.Translation.Get("config.menu-style.name").ToString(),
            tooltip: () => helper.Translation.Get("config.menu-style.tooltip").ToString(),
            allowedValues: menuStyles,
            formatAllowedValue: value => value == ModConfig.MenuStyleBasic
                ? helper.Translation.Get("config.menu-style.basic").ToString()
                : helper.Translation.Get("config.menu-style.stardew").ToString(),
            fieldId: "MenuStyle"
        );

        api.AddTextOption(
            manifest,
            getValue: () => ModConfig.NormalizeStationVisualStyle(getConfig().MinecartVisualStyle),
            setValue: value =>
            {
                string normalized = ModConfig.NormalizeStationVisualStyle(value);
                getConfig().MinecartVisualStyle = normalized;
                StationVisualSettings.SetMinecartStyle(normalized);
            },
            name: () => helper.Translation.Get("config.minecart-style.name").ToString(),
            tooltip: () => helper.Translation.Get("config.minecart-style.tooltip").ToString(),
            allowedValues: stationStyles,
            formatAllowedValue: value => FormatStationStyle(helper, value),
            fieldId: "MinecartVisualStyle"
        );

        api.AddTextOption(
            manifest,
            getValue: () => ModConfig.NormalizeStationVisualStyle(getConfig().EntranceVisualStyle),
            setValue: value =>
            {
                string normalized = ModConfig.NormalizeStationVisualStyle(value);
                getConfig().EntranceVisualStyle = normalized;
                StationVisualSettings.SetEntranceStyle(normalized);
            },
            name: () => helper.Translation.Get("config.entrance-style.name").ToString(),
            tooltip: () => helper.Translation.Get("config.entrance-style.tooltip").ToString(),
            allowedValues: stationStyles,
            formatAllowedValue: value => FormatStationStyle(helper, value),
            fieldId: "EntranceVisualStyle"
        );

        api.AddTextOption(
            manifest,
            getValue: () => ModConfig.NormalizeStationVisualStyle(getConfig().TrackVisualStyle),
            setValue: value =>
            {
                string normalized = ModConfig.NormalizeStationVisualStyle(value);
                getConfig().TrackVisualStyle = normalized;
                StationVisualSettings.SetTrackStyle(normalized);
            },
            name: () => helper.Translation.Get("config.track-style.name").ToString(),
            tooltip: () => helper.Translation.Get("config.track-style.tooltip").ToString(),
            allowedValues: stationStyles,
            formatAllowedValue: value => FormatStationStyle(helper, value),
            fieldId: "TrackVisualStyle"
        );
    }

    private static string FormatStationStyle(IModHelper helper, string value)
    {
        string normalized = ModConfig.NormalizeStationVisualStyle(value);
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
            : helper.Translation.Get(key).ToString();
    }
}
