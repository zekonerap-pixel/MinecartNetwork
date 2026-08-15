using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private StationEnvironmentService StationEnvironmentService = null!;
    private StationManager StationManager = null!;
    private MinecartDataSyncService MinecartDataSyncService = null!;
    private VanillaMinecartService VanillaMinecartService = null!;
    private TeleportService TeleportService = null!;
    private PlacementManager PlacementManager = null!;
    private InteractionManager InteractionManager = null!;
    private MinecartRenderer MinecartRenderer = null!;
    private DebugCommandHandler DebugCommands = null!;
    private bool DepthSortedStationRenderingEnabled;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.LocationRegionService = new LocationRegionService(helper);
        this.StationEnvironmentService = new StationEnvironmentService(this.Monitor);
        this.StationManager = new StationManager(
            helper,
            this.Monitor,
            this.LocationRegionService,
            this.StationEnvironmentService
        );
        this.MinecartDataSyncService = new MinecartDataSyncService(helper, this.Monitor);
        this.StationManager.Changed += this.OnStationsChanged;

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

        // Placed stations are normally drawn around the local Farmer.draw call so they participate
        // in the same front/back relationship as building-like world objects. If the runtime method
        // can't be patched, fall back to the previous RenderedWorld overlay instead of hiding them.
        helper.Events.Display.RenderedWorld += this.DepthSortedStationRenderingEnabled
            ? StationDepthRenderPatch.OnRenderedWorld
            : this.MinecartRenderer.OnRenderedWorld;

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
        var harmony = new Harmony(this.ModManifest.UniqueID);

        this.ApplyStationCollisionPatches(harmony);
        this.DepthSortedStationRenderingEnabled = this.ApplyStationDepthRenderPatch(harmony);
        this.ApplyVanillaMinecartPatch(harmony);
    }

    private void ApplyStationCollisionPatches(Harmony harmony)
    {
        StationCollisionPatch.Configure(this.Monitor, this.StationManager);

        List<MethodInfo> collisionMethods = typeof(GameLocation)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "isCollidingPosition" && method.ReturnType == typeof(bool))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Any(parameter =>
                        parameter.Name == "position" && parameter.ParameterType == typeof(Rectangle))
                    && parameters.Any(parameter =>
                        parameter.Name == "isFarmer" && parameter.ParameterType == typeof(bool));
            })
            .ToList();

        if (collisionMethods.Count == 0)
        {
            this.Monitor.Log(
                "Couldn't find GameLocation.isCollidingPosition with farmer collision arguments; custom station collisions are disabled.",
                LogLevel.Warn
            );
            return;
        }

        HarmonyMethod postfix = new(
            typeof(StationCollisionPatch),
            nameof(StationCollisionPatch.Postfix)
        );

        foreach (MethodInfo method in collisionMethods)
            harmony.Patch(method, postfix: postfix);

        this.Monitor.Log(
            $"Enabled Minecart Network physical collisions on {collisionMethods.Count} GameLocation collision method(s).",
            LogLevel.Debug
        );
    }

    private bool ApplyStationDepthRenderPatch(Harmony harmony)
    {
        if (!StationDepthRenderPatch.Configure(
                this.Monitor,
                this.StationManager,
                this.PlacementManager,
                this.MinecartRenderer))
        {
            return false;
        }

        MethodInfo? farmerDraw = typeof(Farmer)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                if (method.Name != "draw" || method.ReturnType != typeof(void))
                    return false;

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1
                    && parameters[0].ParameterType == typeof(SpriteBatch);
            });

        if (farmerDraw is null)
        {
            this.Monitor.Log(
                "Couldn't find Farmer.draw(SpriteBatch); using the RenderedWorld fallback for station sprites.",
                LogLevel.Warn
            );
            return false;
        }

        harmony.Patch(
            farmerDraw,
            prefix: new HarmonyMethod(
                typeof(StationDepthRenderPatch),
                nameof(StationDepthRenderPatch.Prefix)
            ),
            postfix: new HarmonyMethod(
                typeof(StationDepthRenderPatch),
                nameof(StationDepthRenderPatch.Postfix)
            )
        );

        this.Monitor.Log(
            "Enabled building-style depth sorting for custom station sprites.",
            LogLevel.Debug
        );
        return true;
    }

    private void ApplyVanillaMinecartPatch(Harmony harmony)
    {
        VanillaMinecartPatch.Configure(this.Monitor, this.TryOpenVanillaMinecartMenu);

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

    private void OnStationsChanged()
    {
        this.MinecartDataSyncService.Sync(this.StationManager.Stations);
    }
}
