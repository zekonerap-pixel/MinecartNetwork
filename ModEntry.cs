using HarmonyLib;
using MinecartNetwork.Commands;
using MinecartNetwork.Menus;
using MinecartNetwork.Patches;
using MinecartNetwork.Rendering;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MinecartNetwork;

public sealed class ModEntry : Mod
{
    private ModConfig Config = null!;
    private StationManager StationManager = null!;
    private VanillaMinecartService VanillaMinecartService = null!;
    private TeleportService TeleportService = null!;
    private PlacementManager PlacementManager = null!;
    private InteractionManager InteractionManager = null!;
    private MinecartRenderer MinecartRenderer = null!;
    private DebugCommandHandler DebugCommands = null!;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.StationManager = new StationManager(helper, this.Monitor);
        this.VanillaMinecartService = new VanillaMinecartService(helper, this.Monitor);
        this.TeleportService = new TeleportService(this.Monitor, this.Config);
        this.PlacementManager = new PlacementManager(helper, this.Monitor, this.StationManager, this.Config);
        this.InteractionManager = new InteractionManager(
            helper,
            this.Monitor,
            this.StationManager,
            this.VanillaMinecartService,
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

        this.ApplyHarmonyPatches();

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

    private void ApplyHarmonyPatches()
    {
        VanillaMinecartPatch.Configure(this.Monitor, this.TryOpenVanillaMinecartMenu);

        var harmony = new Harmony(this.ModManifest.UniqueID);
        var original = AccessTools.Method(
            typeof(GameLocation),
            nameof(GameLocation.ShowMineCartMenu),
            new[] { typeof(string), typeof(string) }
        );

        if (original is null)
        {
            this.Monitor.Log(
                "Couldn't find GameLocation.ShowMineCartMenu; vanilla minecarts will keep their original menu.",
                LogLevel.Warn
            );
            return;
        }

        harmony.Patch(
            original,
            prefix: new HarmonyMethod(typeof(VanillaMinecartPatch), nameof(VanillaMinecartPatch.Prefix))
        );
    }

    private bool TryOpenVanillaMinecartMenu(string networkId, string? excludeDestinationId)
    {
        if (!Context.IsWorldReady
            || !networkId.Equals("Default", StringComparison.OrdinalIgnoreCase)
            || !this.VanillaMinecartService.IsDefaultNetworkUnlocked())
            return false;

        string originName = this.VanillaMinecartService.GetDisplayName(excludeDestinationId);
        Game1.playSound("shwip");
        Game1.activeClickableMenu = new MinecartMenu(
            this.Helper,
            this.Monitor,
            this.StationManager,
            this.VanillaMinecartService,
            this.TeleportService,
            this.PlacementManager,
            originName,
            excludedVanillaDestinationId: excludeDestinationId
        );
        return true;
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
