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
    private const int PreferredMenuWidth = 760;
    private const int PreferredMenuHeight = 620;
    private const int ViewportMargin = 48;
    private const int HeaderHeight = 54;
    private const int StationHeight = 46;
    private const int RowGap = 8;
    private const int ScrollStep = 96;
    private const int ScrollBarWidth = 18;
    private const int ScrollBarMinThumbHeight = 30;

    private const float TitleScale = 1f;
    private const float OriginScale = 0.85f;
    private const float CategoryScale = 0.90f;
    private const float DestinationScale = 0.90f;
    private const float EditButtonScale = 0.85f;
    private const float FooterScale = 0.86f;

    private static readonly Rectangle MenuBoxSource = new(0, 256, 60, 60);
    private static readonly Color SubtleTextColor = new(119, 79, 48);
    private static readonly Color CategoryFill = new(246, 177, 74);
    private static readonly Color CategoryHoverFill = new(255, 195, 98);
    private static readonly Color DestinationFill = new(240, 231, 201);
    private static readonly Color DestinationHoverFill = new(247, 238, 210);
    private static readonly Color OriginFill = new(242, 233, 199);
    private static readonly Color ScrollTrackColor = new(188, 140, 83);
    private static readonly Color ScrollThumbColor = new(233, 197, 127);

    private static SpriteFont MenuFont => Game1.smallFont;
    private static SpriteFont MenuTitleFont => Game1.dialogueFont;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly StationManager stations;
    private readonly LocationRegionService regions;
    private readonly VanillaMinecartService vanillaMinecarts;
    private readonly TeleportService teleport;
    private readonly PlacementManager placement;
    private readonly ModConfig config;
    private readonly string originName;
    private readonly string? excludedCustomStationId;
    private readonly string? excludedVanillaDestinationId;
    private readonly HashSet<string> collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MenuGroup> destinationGroups = new();
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
        ModConfig config,
        string originName,
        string? excludedCustomStationId = null,
        string? excludedVanillaDestinationId = null)
        : base(
            (Game1.uiViewport.Width - GetMenuWidth()) / 2,
            (Game1.uiViewport.Height - GetMenuHeight()) / 2,
            GetMenuWidth(),
            GetMenuHeight(),
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.stations = stations;
        this.regions = regions;
        this.vanillaMinecarts = vanillaMinecarts;
        this.teleport = teleport;
        this.placement = placement;
        this.config = config;
        this.originName = originName;
        this.excludedCustomStationId = excludedCustomStationId;
        this.excludedVanillaDestinationId = excludedVanillaDestinationId;

        this.RefreshDestinations();
        this.CollapseAllCategories();
        this.BuildRows();
        this.ClampSelection();
    }

    private bool UseBasicStyle => ModConfig.IsBasicMenuStyle(this.config.MenuStyle);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        this.controllerNavigationActive = false;

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

        if (this.maxScroll > 0 && this.ScrollBarTrack.Contains(x, y))
        {
            this.SetScrollFromPointer(y);
            return;
        }

        if (this.EditButton is Rectangle editButton && editButton.Contains(x, y))
        {
            this.OpenEditor();
            return;
        }

        if (!this.ContentBounds.Contains(x, y))
            return;

        for (int i = 0; i < this.visibleRows.Count; i++)
        {
            MenuRow row = this.visibleRows[i];
            if (!this.IsRowFullyVisible(row) || !row.Bounds.Contains(x, y))
                continue;

            this.selectedIndex = i;
            this.ActivateRow(row);
            return;
        }
    }

    public override void receiveGamePadButton(Buttons button)
    {
        this.controllerNavigationActive = true;

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
        this.BuildRows();
    }

    public override void draw(SpriteBatch b)
    {
        Rectangle viewportRect = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        b.Draw(Game1.staminaRect, viewportRect, Color.Black * (this.UseBasicStyle ? 0.45f : 0.38f));

        Rectangle panel = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        if (this.UseBasicStyle)
        {
            this.Fill(b, panel, new Color(37, 31, 28) * 0.98f);
            this.Outline(b, panel, new Color(121, 88, 60), 5);
        }
        else
        {
            this.DrawVanillaBox(b, panel, Color.White, drawShadow: true);
        }

        Rectangle titleBounds = new(
            this.xPositionOnScreen + 36,
            this.yPositionOnScreen + 16,
            this.width - 108,
            52
        );
        this.DrawLeftCenteredScaledText(
            b,
            titleBounds,
            this.helper.Translation.Get("menu.title"),
            MenuTitleFont,
            TitleScale,
            0.86f,
            this.UseBasicStyle ? Color.Wheat : Game1.textColor,
            drawShadow: !this.UseBasicStyle
        );

        Rectangle originPanel = new(
            this.xPositionOnScreen + 32,
            this.yPositionOnScreen + 76,
            this.width - 64,
            46
        );
        if (this.UseBasicStyle)
        {
            this.Fill(b, originPanel, new Color(49, 43, 40));
            this.Outline(b, originPanel, new Color(75, 66, 59), 1);
        }
        else
        {
            this.DrawVanillaBox(b, originPanel, OriginFill, drawShadow: false);
        }

        this.DrawLeftCenteredScaledText(
            b,
            new Rectangle(originPanel.X + 12, originPanel.Y, originPanel.Width - 24, originPanel.Height),
            this.helper.Translation.Get("menu.origin", new { name = this.originName }),
            MenuFont,
            OriginScale,
            0.76f,
            this.UseBasicStyle ? Color.LightGray : SubtleTextColor
        );

        if (this.visibleRows.Count == 0)
        {
            this.DrawCenteredScaledText(
                b,
                new Rectangle(this.ContentBounds.X + 12, this.ContentTop + 28, this.ContentBounds.Width - 24, 64),
                this.helper.Translation.Get("menu.empty"),
                MenuFont,
                0.94f,
                0.78f,
                this.UseBasicStyle ? Color.LightGray : SubtleTextColor
            );
        }
        else
        {
            for (int i = 0; i < this.visibleRows.Count; i++)
            {
                MenuRow row = this.visibleRows[i];
                if (!this.IsRowFullyVisible(row))
                    continue;

                bool selected = this.controllerNavigationActive && this.selectedIndex == i;
                if (row.Category is not null)
                    this.DrawCategoryRow(b, row, selected);
                else if (row.Destination is not null)
                    this.DrawDestinationRow(b, row, selected);
            }
        }

        if (this.maxScroll > 0)
            this.DrawScrollBar(b);

        if (this.EditButton is Rectangle editButton)
        {
            bool selected = this.controllerNavigationActive && this.selectedIndex == this.visibleRows.Count;
            this.DrawEditButton(b, editButton, selected);
        }

        this.DrawScrollHint(b);
        this.upperRightCloseButton?.draw(b);
        this.drawMouse(b);
    }

    private static int GetMenuWidth()
    {
        int available = Math.Max(1, Game1.uiViewport.Width - ViewportMargin * 2);
        return Math.Min(PreferredMenuWidth, available);
    }

    private static int GetMenuHeight()
    {
        int available = Math.Max(1, Game1.uiViewport.Height - ViewportMargin * 2);
        return Math.Min(PreferredMenuHeight, available);
    }

    private int ContentTop => this.yPositionOnScreen + 134;
    private int ContentBottom => this.yPositionOnScreen + this.height - 86;
    private int ContentHeight => Math.Max(1, this.ContentBottom - this.ContentTop);
    private Rectangle ContentBounds => new(
        this.xPositionOnScreen + 32,
        this.ContentTop,
        this.width - 64,
        this.ContentHeight
    );

    private Rectangle ScrollBarTrack => new(
        this.xPositionOnScreen + this.width - 32,
        this.ContentTop + 6,
        ScrollBarWidth,
        Math.Max(1, this.ContentHeight - 12)
    );

    private Rectangle? EditButton
    {
        get
        {
            if (this.GetEditableOrigin() is null)
                return null;

            int availableWidth = Math.Max(1, this.width - 68);
            int buttonWidth = Math.Min(270, availableWidth);
            return new Rectangle(
                this.xPositionOnScreen + 34,
                this.yPositionOnScreen + this.height - 66,
                buttonWidth,
                48
            );
        }
    }

    private int SelectableCount => this.visibleRows.Count + (this.EditButton.HasValue ? 1 : 0);

    private MinecartStation? GetEditableOrigin()
    {
        if (string.IsNullOrWhiteSpace(this.excludedCustomStationId))
            return null;

        return this.stations.Stations.FirstOrDefault(station =>
            station.Id.Equals(this.excludedCustomStationId, StringComparison.OrdinalIgnoreCase)
            && station.HasPhysicalMinecart);
    }

    private void RefreshDestinations()
    {
        this.destinationGroups.Clear();

        foreach (IGrouping<string, MenuDestination> group in this.GetDestinations()
                     .OrderBy(destination => destination.Category, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(destination => destination.Name, StringComparer.CurrentCultureIgnoreCase)
                     .GroupBy(destination => destination.Category, StringComparer.OrdinalIgnoreCase))
        {
            this.destinationGroups.Add(new MenuGroup(group.Key, group.ToList()));
        }
    }

    private void CollapseAllCategories()
    {
        this.collapsedCategories.Clear();
        foreach (MenuGroup group in this.destinationGroups)
            this.collapsedCategories.Add(group.Category);

        this.scrollOffset = 0;
    }

    private void BuildRows()
    {
        this.visibleRows.Clear();

        int totalHeight = 0;
        foreach (MenuGroup group in this.destinationGroups)
        {
            totalHeight += HeaderHeight + RowGap;
            if (!this.collapsedCategories.Contains(group.Category))
                totalHeight += group.Destinations.Count * (StationHeight + RowGap);
        }

        this.maxScroll = Math.Max(0, totalHeight - this.ContentHeight);
        this.scrollOffset = Math.Clamp(this.scrollOffset, 0, this.maxScroll);

        int y = this.ContentTop - this.scrollOffset;
        int x = this.xPositionOnScreen + 36;
        int scrollPadding = this.maxScroll > 0 ? 32 : 0;
        int rowWidth = Math.Max(1, this.width - 72 - scrollPadding);

        foreach (MenuGroup group in this.destinationGroups)
        {
            this.visibleRows.Add(new MenuRow(
                new Rectangle(x, y, rowWidth, HeaderHeight),
                group.Category,
                null
            ));
            y += HeaderHeight + RowGap;

            if (this.collapsedCategories.Contains(group.Category))
                continue;

            foreach (MenuDestination destination in group.Destinations)
            {
                this.visibleRows.Add(new MenuRow(
                    new Rectangle(x + 20, y, Math.Max(1, rowWidth - 20), StationHeight),
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

        foreach (VanillaMinecartDestination destination in this.vanillaMinecarts.GetAvailableDefaultDestinations())
        {
            if (!string.IsNullOrEmpty(this.excludedVanillaDestinationId)
                && destination.Id.Equals(this.excludedVanillaDestinationId, StringComparison.OrdinalIgnoreCase))
                continue;

            string category = destination.Category;
            if (destination.IsCustomStation)
            {
                MinecartStation? station = this.stations.Stations.FirstOrDefault(candidate =>
                    candidate.Id.Equals(destination.CustomStationId, StringComparison.OrdinalIgnoreCase));
                if (station is not null)
                    category = this.regions.GetStationCategory(station);
            }

            result.Add(new MenuDestination(destination.Name, category, null, destination));
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
            this.config,
            origin,
            this.ReturnFromEditor
        );
    }

    private void ReturnFromEditor()
    {
        this.RefreshDestinations();
        this.CollapseAllCategories();
        this.selectedIndex = 0;
        this.controllerNavigationActive = false;
        this.BuildRows();
        Game1.activeClickableMenu = this;
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

    private bool IsRowFullyVisible(MenuRow row)
        => row.Bounds.Top >= this.ContentTop && row.Bounds.Bottom <= this.ContentBottom;

    private void SetScrollFromPointer(int pointerY)
    {
        Rectangle track = this.ScrollBarTrack;
        int thumbHeight = this.GetScrollThumbHeight();
        int travel = Math.Max(1, track.Height - thumbHeight);
        float position = Math.Clamp((pointerY - track.Y - thumbHeight / 2f) / travel, 0f, 1f);
        this.scrollOffset = (int)Math.Round(this.maxScroll * position);
        this.BuildRows();
        Game1.playSound("shiny4");
    }

    private int GetScrollThumbHeight()
    {
        if (this.maxScroll <= 0)
            return this.ScrollBarTrack.Height;

        float contentRatio = this.ContentHeight / (float)(this.ContentHeight + this.maxScroll);
        return Math.Clamp(
            (int)Math.Round(this.ScrollBarTrack.Height * contentRatio),
            ScrollBarMinThumbHeight,
            this.ScrollBarTrack.Height
        );
    }

    private void DrawScrollBar(SpriteBatch b)
    {
        Rectangle track = this.ScrollBarTrack;

        if (this.UseBasicStyle)
        {
            this.Fill(b, track, new Color(49, 43, 40));
            this.Outline(b, track, new Color(75, 66, 59), 1);
        }
        else
        {
            Rectangle trackBox = new(track.X - 3, track.Y, track.Width + 6, track.Height);
            this.DrawVanillaBox(b, trackBox, ScrollTrackColor, drawShadow: false, scale: 0.40f);
        }

        int thumbHeight = this.GetScrollThumbHeight();
        int travel = Math.Max(0, track.Height - thumbHeight);
        int thumbY = track.Y;
        if (this.maxScroll > 0 && travel > 0)
            thumbY += (int)Math.Round(travel * (this.scrollOffset / (double)this.maxScroll));

        Rectangle thumb = new(track.X - (this.UseBasicStyle ? 0 : 1), thumbY, track.Width + (this.UseBasicStyle ? 0 : 2), thumbHeight);
        if (this.UseBasicStyle)
            this.Fill(b, thumb, new Color(145, 108, 76));
        else
            this.DrawVanillaBox(b, thumb, ScrollThumbColor, drawShadow: true, scale: 0.34f);
    }

    private void DrawScrollHint(SpriteBatch b)
    {
        if (this.maxScroll <= 0)
            return;

        Rectangle? editButton = this.EditButton;
        int left = editButton?.Right + 12 ?? this.xPositionOnScreen + 34;
        int right = this.xPositionOnScreen + this.width - 34;
        int availableWidth = right - left;
        if (availableWidth < 80)
            return;

        this.DrawRightCenteredScaledText(
            b,
            new Rectangle(left, this.yPositionOnScreen + this.height - 62, availableWidth, 44),
            this.helper.Translation.Get("menu.scroll"),
            MenuFont,
            FooterScale,
            0.68f,
            this.UseBasicStyle ? Color.Gray : SubtleTextColor
        );
    }

    private void DrawCategoryRow(SpriteBatch b, MenuRow row, bool selected)
    {
        bool collapsed = this.collapsedCategories.Contains(row.Category!);
        bool hovered = row.Bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;

        if (this.UseBasicStyle)
        {
            this.Fill(b, row.Bounds, highlighted ? new Color(94, 72, 55) : new Color(72, 56, 45));
            this.Outline(b, row.Bounds, highlighted ? new Color(155, 116, 80) : new Color(115, 86, 63), highlighted ? 3 : 2);
        }
        else
        {
            this.DrawVanillaBox(
                b,
                row.Bounds,
                highlighted ? CategoryHoverFill : CategoryFill,
                drawShadow: highlighted
            );
        }

        string marker = collapsed ? ">" : "v";
        this.DrawLeftCenteredScaledText(
            b,
            new Rectangle(row.Bounds.X + 16, row.Bounds.Y, row.Bounds.Width - 32, row.Bounds.Height),
            $"{marker}  {row.Category}",
            MenuFont,
            CategoryScale,
            0.78f,
            this.UseBasicStyle ? Color.Wheat : Game1.textColor
        );
    }

    private void DrawDestinationRow(SpriteBatch b, MenuRow row, bool selected)
    {
        bool hovered = row.Bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;

        if (this.UseBasicStyle)
        {
            this.Fill(b, row.Bounds, highlighted ? new Color(66, 57, 51) : new Color(49, 43, 40));
            this.Outline(b, row.Bounds, highlighted ? new Color(122, 102, 84) : new Color(75, 66, 59), highlighted ? 2 : 1);
        }
        else
        {
            this.DrawVanillaBox(
                b,
                row.Bounds,
                highlighted ? DestinationHoverFill : DestinationFill,
                drawShadow: highlighted
            );
        }

        this.DrawLeftCenteredScaledText(
            b,
            new Rectangle(row.Bounds.X + 18, row.Bounds.Y, row.Bounds.Width - 36, row.Bounds.Height),
            row.Destination!.Name,
            MenuFont,
            DestinationScale,
            0.78f,
            this.UseBasicStyle ? (highlighted ? Color.Wheat : Color.White) : Game1.textColor,
            drawShadow: !this.UseBasicStyle && highlighted
        );
    }

    private void DrawEditButton(SpriteBatch b, Rectangle bounds, bool selected)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;

        if (this.UseBasicStyle)
        {
            this.Fill(b, bounds, highlighted ? new Color(94, 72, 55) : new Color(59, 50, 45));
            this.Outline(b, bounds, highlighted ? new Color(155, 116, 80) : new Color(115, 86, 63), highlighted ? 3 : 2);
        }
        else
        {
            this.DrawVanillaBox(
                b,
                bounds,
                highlighted ? CategoryHoverFill : CategoryFill,
                drawShadow: highlighted
            );
        }

        this.DrawLeftCenteredScaledText(
            b,
            new Rectangle(bounds.X + 14, bounds.Y, bounds.Width - 28, bounds.Height),
            this.helper.Translation.Get("menu.edit-station"),
            MenuFont,
            EditButtonScale,
            0.72f,
            this.UseBasicStyle ? Color.Wheat : Game1.textColor,
            drawShadow: !this.UseBasicStyle && highlighted
        );
    }

    private void DrawVanillaBox(
        SpriteBatch b,
        Rectangle bounds,
        Color tint,
        bool drawShadow,
        float scale = 1f)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            MenuBoxSource,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            tint,
            scale,
            drawShadow
        );
    }

    private void DrawLeftCenteredScaledText(
        SpriteBatch b,
        Rectangle bounds,
        string text,
        SpriteFont font,
        float preferredScale,
        float minScale,
        Color color,
        bool drawShadow = false)
    {
        float scale = FitScale(font, text, bounds.Width, bounds.Height, preferredScale, minScale);
        string displayText = TruncateScaledText(text, font, bounds.Width, scale);
        float visualLineHeight = font.LineSpacing * scale;
        Vector2 position = new(bounds.X, bounds.Y + (bounds.Height - visualLineHeight) / 2f);

        if (drawShadow)
        {
            b.DrawString(
                font,
                displayText,
                position + new Vector2(2f, 2f),
                Color.Black * 0.22f,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        b.DrawString(font, displayText, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawCenteredScaledText(
        SpriteBatch b,
        Rectangle bounds,
        string text,
        SpriteFont font,
        float preferredScale,
        float minScale,
        Color color)
    {
        float scale = FitScale(font, text, bounds.Width, bounds.Height, preferredScale, minScale);
        string displayText = TruncateScaledText(text, font, bounds.Width, scale);
        float measuredWidth = font.MeasureString(displayText).X * scale;
        float visualLineHeight = font.LineSpacing * scale;
        Vector2 position = new(
            bounds.X + (bounds.Width - measuredWidth) / 2f,
            bounds.Y + (bounds.Height - visualLineHeight) / 2f
        );
        b.DrawString(font, displayText, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRightCenteredScaledText(
        SpriteBatch b,
        Rectangle bounds,
        string text,
        SpriteFont font,
        float preferredScale,
        float minScale,
        Color color)
    {
        float scale = FitScale(font, text, bounds.Width, bounds.Height, preferredScale, minScale);
        string displayText = TruncateScaledText(text, font, bounds.Width, scale);
        float measuredWidth = font.MeasureString(displayText).X * scale;
        float visualLineHeight = font.LineSpacing * scale;
        Vector2 position = new(
            bounds.Right - measuredWidth,
            bounds.Y + (bounds.Height - visualLineHeight) / 2f
        );
        b.DrawString(font, displayText, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private static float FitScale(
        SpriteFont font,
        string text,
        float maxWidth,
        float maxHeight,
        float preferredScale,
        float minScale)
    {
        float rawWidth = font.MeasureString(text).X;
        if (rawWidth <= 0f)
            return preferredScale;

        float widthScale = maxWidth / rawWidth;
        return Math.Clamp(Math.Min(preferredScale, widthScale), minScale, preferredScale);
    }

    private static string TruncateScaledText(string text, SpriteFont font, float maxWidth, float scale)
    {
        if (font.MeasureString(text).X * scale <= maxWidth)
            return text;

        const string suffix = "...";
        if (font.MeasureString(suffix).X * scale > maxWidth)
            return string.Empty;

        int low = 0;
        int high = text.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            string candidate = text[..middle].TrimEnd() + suffix;
            if (font.MeasureString(candidate).X * scale <= maxWidth)
                low = middle;
            else
                high = middle - 1;
        }

        return text[..low].TrimEnd() + suffix;
    }

    private void Fill(SpriteBatch batch, Rectangle rectangle, Color color)
        => batch.Draw(Game1.staminaRect, rectangle, color);

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

    private sealed record MenuGroup(string Category, List<MenuDestination> Destinations);
    private sealed record MenuRow(Rectangle Bounds, string? Category, MenuDestination? Destination);
}
