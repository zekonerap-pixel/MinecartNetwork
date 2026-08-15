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
                StationVisualSettings.Apply(getConfig());
            },
            save
        );

        string[] menuStyles =
        {
            ModConfig.MenuStyleStardew,
            ModConfig.MenuStyleBasic
        };

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
            allowedValues: ModConfig.StationVisualStyles,
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
            allowedValues: ModConfig.StationVisualStyles,
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
            allowedValues: ModConfig.StationVisualStyles,
            formatAllowedValue: value => FormatStationStyle(helper, value),
            fieldId: "TrackVisualStyle"
        );
    }

    private static string FormatStationStyle(IModHelper helper, string value)
    {
        string key = ModConfig.NormalizeStationVisualStyle(value) switch
        {
            ModConfig.StationVisualRustic => "config.station-style.rustic",
            ModConfig.StationVisualCopper => "config.station-style.copper",
            ModConfig.StationVisualDarkIron => "config.station-style.dark-iron",
            ModConfig.StationVisualMoss => "config.station-style.moss",
            ModConfig.StationVisualCrystal => "config.station-style.crystal",
            _ => "config.station-style.current"
        };

        return helper.Translation.Get(key).ToString();
    }
}
