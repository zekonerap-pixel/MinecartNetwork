using MinecartNetwork.Menus;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class ManagementMenuController
{
    private readonly IModHelper helper;
    private readonly VanillaMinecartService vanillaMinecarts;
    private readonly PlacementManager placement;
    private readonly ModConfig config;

    public ManagementMenuController(
        IModHelper helper,
        VanillaMinecartService vanillaMinecarts,
        PlacementManager placement,
        ModConfig config)
    {
        this.helper = helper;
        this.vanillaMinecarts = vanillaMinecarts;
        this.placement = placement;
        this.config = config;
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady
            || !Context.IsPlayerFree
            || this.placement.IsPlacing
            || Game1.activeClickableMenu is not null
            || e.Button != this.config.ManagementMenuKey)
        {
            return;
        }

        this.helper.Input.Suppress(e.Button);

        if (!Context.IsMainPlayer)
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(this.helper.Translation.Get("management.host-only").ToString());
            return;
        }

        if (!this.vanillaMinecarts.IsDefaultNetworkUnlocked())
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(this.helper.Translation.Get("management.locked").ToString());
            return;
        }

        Game1.playSound("bigSelect");
        Game1.activeClickableMenu = new NetworkManagementMenu(
            this.helper,
            this.placement,
            this.config
        );
    }
}
