using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private readonly LocationRegionService regions;
    private readonly VanillaMinecartService vanillaMinecarts;
    private readonly TeleportService teleport;
    private readonly PlacementManager placement;
    private readonly string originName;
    private readonly string? excludedCustomStationId;
    private readonly string? excludedVanillaDestinationId;
    private readonly HashSet<string> collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MenuRow> visibleRows = new();

    private int scrollOffset;
    private int maxScroll;
    private int selectedIndex = -1;
    private bool controllerNavigationActive;

    public MinecartMenu(
        IModHelper helper,
        IMonitor monitor,
        StationManager stations,
        LocationRegionService regions,
        VanillaMinecartService vanillaMinecarts,
        TeleportService teleport,
        PlacementManager placement,
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
        this.regions = regions;
        this.vanillaMinecarts = vanillaMinecarts;
        this.teleport = teleport;
        this.placement = placement;
        this.originName = originName;
        this.excludedCustomStationId = excludedCustomStationId;
        this.excludedVanillaDestinationId = excludedVanillaDestinationId;

        this.BuildRows();
        this.ClampSelection();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        this.controllerNavigationActive = false;

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

        if (this.EditButton is Rectangle editButton && editButton.Contains(x, y))
        {
            this.OpenEditor();
            return;
        }

        this.BuildRows();

        for (int i = 0; i < this.visibleRows.Count; i++)
        {
            MenuRow row = this.visibleRows[i];
            if (!row.Bounds.Contains(x, y))
                continue;

            this.selectedIndex = i;
            this.ActivateRow(row);
            return;
        }
    }

    public override void receiveGamePadButton(Buttons button)
    {
        this.controllerNavigationActive = true;
        this.BuildRows();

        switch (button)
        {
            case Buttons.B:
                Game1.exitActiveMenu();
                return;

            case Buttons.DPadUp:
            case Buttons.LeftThumbstickUp:
                this.MoveSelection(-1);
                return;

            case Buttons.DPadDown:
            case Buttons.LeftThumbstickDown:
                this.MoveSelection(1);
                return;

            case Buttons.LeftShoulder:
                this.MoveSelection(-5);
                return;

            case Buttons.RightShoulder:
                this.MoveSelection(5);
                return;

            case Buttons.DPadLeft:
            case Buttons.LeftThumbstickLeft:
                this.SetSelectedCategoryCollapsed(true);
                return;

            case Buttons.DPadRight:
            case Buttons.LeftThumbstickRight:
                this.SetSelectedCategoryCollapsed(false);
                return;

            case Buttons.A:
                this.ActivateSelected();
                return;
        }

        base.receiveGamePadButton(button);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        base.receiveScrollWheelAction(direction);
        this.controllerNavigationActive = false;

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
            for (int i = 0; i < this.visibleRows.Count; i++)
            {
                MenuRow row = this.visibleRows[i];
                if (row.Bounds.Bottom < this.ContentTop || row.Bounds.Top > this.ContentBottom)
                    continue;

                bool selected = this.controllerNavigationActive && this.selectedIndex == i;
                if (row.Category is not null)
                    this.DrawCategoryRow(b, row, selected);
                else if (row.Destination is not null)
                    this.DrawDestinationRow(b, row, selected);
            }
        }

        if (this.EditButton is Rectangle editButton)
        {
            bool selected = this.controllerNavigationActive && this.selectedIndex == this.visibleRows.Count;
            this.DrawEditButton(b, editButton, selected);
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
    private int ContentBottom => this.yPositionOnScreen + this.height - 72;
    private int ContentHeight => this.ContentBottom - this.ContentTop;

    private Rectangle? EditButton => this.GetEditableOrigin() is null
        ? null
        : new Rectangle(this.xPositionOnScreen + 34, this.yPositionOnScreen + this.height - 50, 210, 34);

    private int SelectableCount => this.visibleRows.Count + (this.EditButton.HasValue ? 1 : 0);

    private MinecartStation? GetEditableOrigin()
    {
        if (string.IsNullOrWhiteSpace(this.excludedCustomStationId))
            return null;

        return this.stations.Stations.FirstOrDefault(station =>
            station.Id.Equals(this.excludedCustomStationId, StringComparison.OrdinalIgnoreCase)
            && station.HasPhysicalMinecart);
    }

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

        this.ClampSelection();
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
                this.regions.GetStationCategory(station),
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

    private void ActivateRow(MenuRow row)
    {
        if (row.Category is not null)
        {
            if (!this.collapsedCategories.Add(row.Category))
                this.collapsedCategories.Remove(row.Category);

            Game1.playSound("shwip");
            this.BuildRows();
            this.EnsureSelectedVisible();
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
    }

    private void ActivateSelected()
    {
        this.BuildRows();
        if (this.selectedIndex < 0)
            return;

        if (this.selectedIndex < this.visibleRows.Count)
        {
            this.ActivateRow(this.visibleRows[this.selectedIndex]);
            return;
        }

        if (this.EditButton.HasValue && this.selectedIndex == this.visibleRows.Count)
            this.OpenEditor();
    }

    private void OpenEditor()
    {
        MinecartStation? origin = this.GetEditableOrigin();
        if (origin is null)
            return;

        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new StationEditMenu(
            this.helper,
            this.stations,
            this.regions,
            this.placement,
            origin
        );
    }

    private void MoveSelection(int delta)
    {
        int count = this.SelectableCount;
        if (count <= 0)
        {
            this.selectedIndex = -1;
            return;
        }

        if (this.selectedIndex < 0)
            this.selectedIndex = 0;
        else
            this.selectedIndex = Math.Clamp(this.selectedIndex + delta, 0, count - 1);

        Game1.playSound("shiny4");
        this.EnsureSelectedVisible();
    }

    private void SetSelectedCategoryCollapsed(bool collapsed)
    {
        this.BuildRows();
        if (this.selectedIndex < 0 || this.selectedIndex >= this.visibleRows.Count)
            return;

        MenuRow row = this.visibleRows[this.selectedIndex];
        if (row.Category is null)
            return;

        bool changed = collapsed
            ? this.collapsedCategories.Add(row.Category)
            : this.collapsedCategories.Remove(row.Category);

        if (!changed)
            return;

        Game1.playSound("shwip");
        this.BuildRows();
        this.EnsureSelectedVisible();
    }

    private void EnsureSelectedVisible()
    {
        this.BuildRows();
        if (this.selectedIndex < 0 || this.selectedIndex >= this.visibleRows.Count)
            return;

        Rectangle bounds = this.visibleRows[this.selectedIndex].Bounds;
        if (bounds.Top < this.ContentTop)
            this.scrollOffset = Math.Max(0, this.scrollOffset - (this.ContentTop - bounds.Top));
        else if (bounds.Bottom > this.ContentBottom)
            this.scrollOffset = Math.Min(this.maxScroll, this.scrollOffset + (bounds.Bottom - this.ContentBottom));

        this.BuildRows();
    }

    private void ClampSelection()
    {
        int count = this.SelectableCount;
        if (count <= 0)
        {
            this.selectedIndex = -1;
            return;
        }

        if (this.selectedIndex < 0)
            this.selectedIndex = 0;
        else if (this.selectedIndex >= count)
            this.selectedIndex = count - 1;
    }

    private void DrawCategoryRow(SpriteBatch b, MenuRow row, bool selected)
    {
        bool collapsed = this.collapsedCategories.Contains(row.Category!);
        bool hovered = row.Bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;
        this.Fill(b, row.Bounds, highlighted ? new Color(88, 68, 53) : new Color(72, 56, 45));
        this.Outline(b, row.Bounds, highlighted ? new Color(145, 108, 76) : new Color(115, 86, 63), highlighted ? 3 : 2);

        string marker = collapsed ? "▶" : "▼";
        b.DrawString(
            Game1.smallFont,
            $"{marker}  {row.Category}",
            new Vector2(row.Bounds.X + 14, row.Bounds.Y + 10),
            Color.Wheat
        );
    }

    private void DrawDestinationRow(SpriteBatch b, MenuRow row, bool selected)
    {
        bool hovered = row.Bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;
        this.Fill(b, row.Bounds, highlighted ? new Color(66, 57, 51) : new Color(49, 43, 40));
        this.Outline(b, row.Bounds, highlighted ? new Color(109, 93, 80) : new Color(75, 66, 59), highlighted ? 2 : 1);

        b.DrawString(
            Game1.smallFont,
            row.Destination!.Name,
            new Vector2(row.Bounds.X + 16, row.Bounds.Y + 8),
            highlighted ? Color.Wheat : Color.White
        );
    }

    private void DrawEditButton(SpriteBatch b, Rectangle bounds, bool selected)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;
        this.Fill(b, bounds, highlighted ? new Color(88, 68, 53) : new Color(59, 50, 45));
        this.Outline(b, bounds, highlighted ? new Color(145, 108, 76) : new Color(115, 86, 63), highlighted ? 3 : 2);

        string text = this.helper.Translation.Get("menu.edit-station");
        Vector2 size = Game1.smallFont.MeasureString(text) * 0.75f;
        b.DrawString(
            Game1.smallFont,
            text,
            new Vector2(bounds.X + 12, bounds.Y + (bounds.Height - size.Y) / 2f),
            Color.Wheat,
            0f,
            Vector2.Zero,
            0.75f,
            SpriteEffects.None,
            0f
        );
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
