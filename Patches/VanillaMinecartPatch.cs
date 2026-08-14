using StardewModdingAPI;

namespace MinecartNetwork.Patches;

internal static class VanillaMinecartPatch
{
    private static IMonitor? monitor;
    private static Func<string, string?, bool>? openUnifiedMenu;

    public static void Configure(IMonitor modMonitor, Func<string, string?, bool> handler)
    {
        monitor = modMonitor;
        openUnifiedMenu = handler;
    }

    public static bool Prefix(string networkId, string? excludeDestinationId)
    {
        try
        {
            if (openUnifiedMenu is null || string.IsNullOrWhiteSpace(networkId))
                return true;

            bool handled = openUnifiedMenu(networkId, excludeDestinationId);
            return !handled;
        }
        catch (Exception ex)
        {
            monitor?.Log(
                $"Failed intercepting minecart network '{networkId}'; falling back to the original game menu. {ex}",
                LogLevel.Error
            );
            return true;
        }
    }
}
