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

        api.Register(manifest, reset, save);

        string[] allowedValues =
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
            allowedValues: allowedValues,
            formatAllowedValue: value => value == ModConfig.MenuStyleBasic
                ? helper.Translation.Get("config.menu-style.basic").ToString()
                : helper.Translation.Get("config.menu-style.stardew").ToString(),
            fieldId: "MenuStyle"
        );
    }
}
