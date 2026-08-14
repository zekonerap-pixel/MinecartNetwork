using HarmonyLib;
using StardewValley;

namespace MinecartNetwork.Patches;

[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.ShowMineCartMenu))]
internal static class VanillaMinecartPatch
{
    private static Func<string, string?, bool>? openUnifiedMenu;

    public static void Configure(Func<string, string?, bool> handler)
    {
        openUnifiedMenu = handler;
    }

    private static bool Prefix(string networkId, string? excludeDestinationId)
    {
        if (openUnifiedMenu is null)
            return true;

        // For now we only replace the vanilla/default network. Other modded
        // minecart networks keep their original behavior until dedicated
        // multi-network support is implemented.
        if (!networkId.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return true;

        bool handled = openUnifiedMenu(networkId, excludeDestinationId);
        return !handled;
    }
}
