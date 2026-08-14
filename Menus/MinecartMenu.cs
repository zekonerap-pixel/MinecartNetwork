using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MinecartNetwork.Menus;

public sealed class MinecartMenu : IClickableMenu
{
    private const int MenuWidth = 760;
    private const int MenuHeight = 620;
    private const int HeaderHeight = 46;
    private const int StationHeight = 42;
    private const int RowGap = 6;
    private const int ScrollStep = 96;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly VanillaMinecartService vanillaMinecarts;
    private readonly TeleportService teleport;
    private readonly string originName;
    private readonly string? excludedCustomStationId;
    private readonly string? excludedVanillaDestinationId;
    private readonly HashSet<string> collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MenuRow> visibleRows = new();

    private int scrollOffset;
    private int maxScroll;

    public MinecartMenu(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        VanillaMinecartService vanillaMinecarts,
        TeleportService teleport,
        string originName,
        string? excludedCustomStationId = null,
        string? excludedVanillaDestinationId = null)
        : base(
            Game1.uiViewport.Width / 2 - MenuWidth / 2,
            Game1.uiViewport.Height / 2 - MenuHeight / 2,
            MenuWidth,
            MenuHeight,
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.vanillaMinecarts = vanillaMinecarts;
        this.teleport = teleport;
        this.originName = originName;
        this.excludedCustomStationId = excludedCustomStationId;
        this.excludedVanillaDestinationId = excludedVanillaDestinationId;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

        this.BuildRows();

        foreach (MenuRow row in this.visibleRows)
        {
            if (!row.Bounds.Contains(x, y))
                continue;

            if (row.Category is not null)
            {
                if (!this.collapsedCategories.Add(row.Category))
                    this.collapsedCategories.Remove(row.Category);

                Game1.playSound("shwip");
                this.ClampScroll();
                return;
            }

            if (row.Destination is null)
                return;

            MenuDestination destination = row.Destination;
            Game1.exitActiveMenu();

            bool success;
            string? error;

            if (destination.CustomStation is not null)
                success = this.teleport.TryWarp(destination.CustomStation, out error);
            else if (destination.VanillaDestination is not null)
                success = this.vanillaMinecarts.TryWarp(destination.VanillaDestination, out error);
            else
                return;

            if (!success)
            {
                this.monitor.Log(error ?? $"Failed to travel to destination '{destination.Name}'.", LogLevel.Error);
                if (!string.IsNullOrWhiteSpace(error))
                    Game1.showRedMessage(error);
            }

            return;
        }
    }

    public override void receiveScrollWheelAction(int direction)
    {
        base.receiveScrollWheelAction(direction);

        if (this.maxScroll <= 0 || direction == 0)
            return;

        this.scrollOffset += direction > 0 ? -ScrollStep : ScrollStep;
        this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.maxScroll);
    }

    public override void draw(SpriteBatch b)
    {
        Rectangle viewportRect = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        b.Draw(Game1.staminaRect, viewportRect, Color.Black * 0.45f);

        Rectangle panel = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        this.Fill(b, panel, new Color(37, 31, 28) * 0.98f);
        this.Outline(b, panel, new Color(121, 88, 60), 5);

        string title = this.helper.Translation.Get("menu.title");
        b.DrawString(
            Game1.dialogueFont,
            title,
            new Vector2(this.xPositionOnScreen + 34, this.yPositionOnScreen + 22),
            Color.Wheat
        );

        string originText = this.helper.Translation.Get("menu.origin", new { name = this.originName });
        b.DrawString(
            Game1.smallFont,
            originText,
            new Vector2(this.xPositionOnScreen + 36, this.yPositionOnScreen + 72),
            Color.LightGray
        );

        this.BuildRows();

        if (this.visibleRows.Count == 0)
        {
            string empty = this.helper.Translation.Get("menu.empty");
            Vector2 size = Game1.smallFont.MeasureString(empty);
            b.DrawString(
                Game1.smallFont,
                empty,
                new Vector2(
                    this.xPositionOnScreen + (this.width - size.X) / 2f,
                    this.yPositionOnScreen + 180
                ),
                Color.LightGray
            );
        }
        else
        {
            foreach (MenuRow row in this.visibleRows)
            {
                if (row.Bounds.Bottom < this.ContentTop || row.Bounds.Top > this.ContentBottom)
                    continue;

                if (row.Category is not null)
                    this.DrawCategoryRow(b, row);
                else if (row.Destination is not null)
                    this.DrawDestinationRow(b, row);
            }
        }

        if (this.maxScroll > 0)
        {
            string scroll = this.helper.Translation.Get("menu.scroll");
            Vector2 size = Game1.smallFont.MeasureString(scroll) * 0.75f;
            b.DrawString(
                Game1.smallFont,
                scroll,
                new Vector2(
                    this.xPositionOnScreen + this.width - size.X - 34,
                    this.yPositionOnScreen + this.height - 31
                ),
                Color.Gray,
                0f,
                Vector2.Zero,
                0.75f,
                SpriteEffects.None,
                0f
            );
        }

        this.upperRightCloseButton?.draw(b);
        this.drawMouse(b);
    }

    private int ContentTop => this.yPositionOnScreen + 116;
    private int ContentBottom => this.yPositionOnScreen + this.height - 48;
    private int ContentHeight => this.ContentBottom - this.ContentTop;

    private void BuildRows()
    {
        this.visibleRows.Clear();

        List<MenuDestination> destinations = this.GetDestinations();
        List<IGrouping<string, MenuDestination>> groups = destinations
            .OrderBy(destination => destination.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(destination => destination.Name, StringComparer.CurrentCultureIgnoreCase)
            .GroupBy(destination => destination.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int totalHeight = 0;
        foreach (IGrouping<string, MenuDestination> group in groups)
        {
            totalHeight += HeaderHeight + RowGap;
            if (!this.collapsedCategories.Contains(group.Key))
                totalHeight += group.Count() * (StationHeight + RowGap);
        }

        this.maxScroll = Math.Max(0, totalHeight - this.ContentHeight);
        this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.maxScroll);

        int y = this.ContentTop - this.scrollOffset;
        int x = this.xPositionOnScreen + 32;
        int rowWidth = this.width - 64;

        foreach (IGrouping<string, MenuDestination> group in groups)
        {
            this.visibleRows.Add(new MenuRow(
                new Rectangle(x, y, rowWidth, HeaderHeight),
                group.Key,
                null
            ));
            y += HeaderHeight + RowGap;

            if (this.collapsedCategories.Contains(group.Key))
                continue;

            foreach (MenuDestination destination in group)
            {
                this.visibleRows.Add(new MenuRow(
                    new Rectangle(x + 18, y, rowWidth - 18, StationHeight),
                    null,
                    destination
                ));
                y += StationHeight + RowGap;
            }
        }
    }

    private List<MenuDestination> GetDestinations()
    {
        var result = new List<MenuDestination>();

        foreach (MinecartStation station in this.stations.Stations)
        {
            if (!station.IsEnabled
                || (!string.IsNullOrEmpty(this.excludedCustomStationId)
                    && station.Id.Equals(this.excludedCustomStationId, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new MenuDestination(
                station.Name,
                station.Category,
                station,
                null
            ));
        }

        foreach (VanillaMinecartDestination destination in this.vanillaMinecarts.GetAvailableDefaultDestinations())
        {
            if (!string.IsNullOrEmpty(this.excludedVanillaDestinationId)
                && destination.Id.Equals(this.excludedVanillaDestinationId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new MenuDestination(
                destination.Name,
                destination.Category,
                null,
                destination
            ));
        }

        return result;
    }

    private void DrawCategoryRow(SpriteBatch b, MenuRow row)
    {
        bool collapsed = this.collapsedCategories.Contains(row.Category!);
        this.Fill(b, row.Bounds, new Color(72, 56, 45));
        this.Outline(b, row.Bounds, new Color(115, 86, 63), 2);

        string marker = collapsed ? "▶" : "▼";
        b.DrawString(
            Game1.smallFont,
            $"{marker}  {row.Category}",
            new Vector2(row.Bounds.X + 14, row.Bounds.Y + 10),
            Color.Wheat
        );
    }

    private void DrawDestinationRow(SpriteBatch b, MenuRow row)
    {
        this.Fill(b, row.Bounds, new Color(49, 43, 40));
        this.Outline(b, row.Bounds, new Color(75, 66, 59), 1);

        b.DrawString(
            Game1.smallFont,
            row.Destination!.Name,
            new Vector2(row.Bounds.X + 16, row.Bounds.Y + 8),
            Color.White
        );
    }

    private void ClampScroll()
    {
        this.BuildRows();
        this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.maxScroll);
    }

    private void Fill(SpriteBatch batch, Rectangle rectangle, Color color)
    {
        batch.Draw(Game1.staminaRect, rectangle, color);
    }

    private void Outline(SpriteBatch batch, Rectangle rectangle, Color color, int thickness)
    {
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        this.Fill(batch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        this.Fill(batch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    private sealed record MenuDestination(
        string Name,
        string Category,
        MinecartStation? CustomStation,
        VanillaMinecartDestination? VanillaDestination
    );

    private sealed record MenuRow(Rectangle Bounds, string? Category, MenuDestination? Destination);
}
