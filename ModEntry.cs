using MinecartNetwork.Commands;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MinecartNetwork;

public sealed class ModEntry : Mod
{
    private ModConfig Config = null!;
    private StationManager StationManager = null!;
    private TeleportService TeleportService = null!;
    private DebugCommandHandler DebugCommands = null!;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.StationManager = new StationManager(helper, this.Monitor);
        this.TeleportService = new TeleportService(this.Monitor, this.Config);
        this.DebugCommands = new DebugCommandHandler(this.Monitor, this.StationManager, this.TeleportService, this.Config);

        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;

        helper.ConsoleCommands.Add(
            "mn",
            "Minecart Network development commands. Run 'mn' for help.",
            this.DebugCommands.Handle
        );

        this.Monitor.Log("Minecart Network initialized.", LogLevel.Debug);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.StationManager.Load();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.StationManager.Save();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.StationManager.Clear();
    }
}
