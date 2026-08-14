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
    private LocationRegionService LocationRegionService = null!;
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
        this.LocationRegionService = new LocationRegionService(helper);
        this.StationManager = new StationManager(helper, this.Monitor, this.LocationRegionService);
        this.VanillaMinecartService = new VanillaMinecartService(helper, this.Monitor, this.LocationRegionService);
        this.TeleportService = new TeleportService(this.Monitor, this.Config);
        this.PlacementManager = new PlacementManager(
            helper,
            this.Monitor,
            this.StationManager,
            this.LocationRegionService,
            this.Config
        );
        this.InteractionManager = new InteractionManager(
            helper,
            this.Monitor,
            this.StationManager,
            this.LocationRegionService,
            this.VanillaMinecartService,
            this.TeleportService,
            this.PlacementManager
        );
        this.MinecartRenderer = new MinecartRenderer(helper, this.StationManager, this.PlacementManager);
        this.DebugCommands = new DebugCommandHandler(
            this.Monitor,
            this.StationManager,
            this.LocationRegionService,
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
                "Couldn't find GameLocation.ShowMineCartMenu; game and modded minecarts will keep their original menus.",
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
        if (!Context.IsWorldReady || !this.VanillaMinecartService.IsNetworkUnlocked(networkId))
            return false;

        bool isDefaultNetwork = networkId.Equals(
            VanillaMinecartService.DefaultNetworkId,
            StringComparison.OrdinalIgnoreCase
        );

        if (!isDefaultNetwork && this.VanillaMinecartService.HasAvailablePricedDestinations(networkId))
        {
            this.Monitor.Log(
                $"Minecart network '{networkId}' contains priced destinations; preserving its original menu to avoid bypassing payment mechanics.",
                LogLevel.Trace
            );
            return false;
        }

        this.VanillaMinecartService.SelectNetwork(networkId);
        string originName = this.VanillaMinecartService.GetDisplayName(networkId, excludeDestinationId);

        Game1.playSound("shwip");
        Game1.activeClickableMenu = new MinecartMenu(
            this.Helper,
            this.Monitor,
            this.StationManager,
            this.LocationRegionService,
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
        this.VanillaMinecartService.SelectNetwork(VanillaMinecartService.DefaultNetworkId);
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.StationManager.Save();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.PlacementManager.Cancel(silent: true);
        this.StationManager.Clear();
        this.VanillaMinecartService.SelectNetwork(VanillaMinecartService.DefaultNetworkId);
    }
}
