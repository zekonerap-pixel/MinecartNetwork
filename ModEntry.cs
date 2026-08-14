using MinecartNetwork.Commands;
using MinecartNetwork.Rendering;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MinecartNetwork;

public sealed class ModEntry : Mod
{
    private ModConfig Config = null!;
    private StationManager StationManager = null!;
    private TeleportService TeleportService = null!;
    private PlacementManager PlacementManager = null!;
    private InteractionManager InteractionManager = null!;
    private MinecartRenderer MinecartRenderer = null!;
    private DebugCommandHandler DebugCommands = null!;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.StationManager = new StationManager(helper, this.Monitor);
        this.TeleportService = new TeleportService(this.Monitor, this.Config);
        this.PlacementManager = new PlacementManager(helper, this.Monitor, this.StationManager, this.Config);
        this.InteractionManager = new InteractionManager(
            helper,
            this.Monitor,
            this.StationManager,
            this.TeleportService,
            this.PlacementManager
        );
        this.MinecartRenderer = new MinecartRenderer(helper, this.StationManager, this.PlacementManager);
        this.DebugCommands = new DebugCommandHandler(
            this.Monitor,
            this.StationManager,
            this.TeleportService,
            this.PlacementManager,
            this.Config
        );

        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.InteractionManager.OnUpdateTicked;

        helper.Events.Input.ButtonPressed += this.PlacementManager.OnButtonPressed;
        helper.Events.Input.ButtonPressed += this.InteractionManager.OnButtonPressed;
        helper.Events.Display.MenuChanged += this.PlacementManager.OnMenuChanged;
        helper.Events.Display.RenderedWorld += this.MinecartRenderer.OnRenderedWorld;
        helper.Events.Display.RenderedHud += this.MinecartRenderer.OnRenderedHud;

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
        this.PlacementManager.Cancel(silent: true);
        this.StationManager.Clear();
    }
}
