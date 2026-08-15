using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MinecartNetwork.Menus;

public sealed class StationEditMenu : IClickableMenu
{
    private const int PreferredMenuWidth = 620;
    private const int PreferredMenuHeight = 570;
    private const int ViewportMargin = 48;
    private const int PreferredButtonHeight = 54;
    private const int PreferredButtonGap = 10;
    private const int ButtonCount = 5;
    private const int ButtonAreaTop = 172;
    private const int ButtonAreaBottomMargin = 32;

    private static readonly Rectangle MenuBoxSource = new(0, 256, 60, 60);
    private static readonly Color TextColor = new(86, 22, 12);
    private static readonly Color SubtleTextColor = new(120, 78, 48);
    private static readonly Color ButtonFill = new(255, 235, 177);
    private static readonly Color ButtonHoverFill = new(255, 216, 126);
    private static readonly Color DangerFill = new(255, 201, 178);
    private static readonly Color DangerHoverFill = new(244, 166, 145);
    private static readonly Color DangerTextColor = new(132, 42, 33);

    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly LocationRegionService regions;
    private readonly PlacementManager placement;
    private readonly MinecartStation station;
    private readonly Action? returnToPreviousMenu;

    private bool confirmDelete;
    private int selectedButtonIndex;
    private bool controllerNavigationActive;

    public StationEditMenu(
        IModHelper helper,
        StationManager stations,
        LocationRegionService regions,
        PlacementManager placement,
        MinecartStation station,
        Action? returnToPreviousMenu = null)
        : base(
            (Game1.uiViewport.Width - GetMenuWidth()) / 2,
            (Game1.uiViewport.Height - GetMenuHeight()) / 2,
            GetMenuWidth(),
            GetMenuHeight(),
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.stations = stations;
        this.regions = regions;
        this.placement = placement;
        this.station = station;
        this.returnToPreviousMenu = returnToPreviousMenu;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.controllerNavigationActive = false;

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
        {
            this.ReturnToPreviousMenu();
            return;
        }

        base.receiveLeftClick(x, y, playSound);

        for (int i = 0; i < ButtonCount; i++)
        {
            if (!this.GetButtonBounds(i).Contains(x, y))
                continue;

            this.selectedButtonIndex = i;
            this.ActivateButton(i);
            return;
        }
    }

    public override void receiveGamePadButton(Buttons button)
    {
        this.controllerNavigationActive = true;

        switch (button)
        {
            case Buttons.B:
                this.ReturnToPreviousMenu();
                return;

            case Buttons.DPadUp:
            case Buttons.LeftThumbstickUp:
                this.MoveSelection(-1);
                return;

            case Buttons.DPadDown:
            case Buttons.LeftThumbstickDown:
                this.MoveSelection(1);
                return;

            case Buttons.A:
                this.ActivateButton(this.selectedButtonIndex);
                return;
        }

        base.receiveGamePadButton(button);
    }

    public override void draw(SpriteBatch b)
    {
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        b.Draw(Game1.staminaRect, viewport, Color.Black * 0.38f);

        Rectangle panel = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        this.DrawVanillaBox(b, panel, Color.White, drawShadow: true);

        string title = this.FitText(
            Game1.dialogueFont,
            this.helper.Translation.Get("edit.title"),
            this.width - 108
        );
        this.DrawTextWithShadow(
            b,
            Game1.dialogueFont,
            title,
            new Vector2(this.xPositionOnScreen + 36, this.yPositionOnScreen + 26),
            TextColor
        );

        string effectiveCategory = this.regions.GetStationCategory(this.station);
        string categoryMode = this.station.UseAutomaticCategory
            ? this.helper.Translation.Get("edit.mode-auto")
            : this.helper.Translation.Get("edit.mode-manual");
        string details = this.helper.Translation.Get("edit.details", new
        {
            name = this.station.Name,
            category = effectiveCategory,
            mode = categoryMode
        });

        Rectangle detailsPanel = new(
            this.xPositionOnScreen + 32,
            this.yPositionOnScreen + 84,
            this.width - 64,
            50
        );
        this.DrawVanillaBox(b, detailsPanel, new Color(255, 245, 210), drawShadow: false);

        details = this.FitText(Game1.smallFont, details, detailsPanel.Width - 24);
        b.DrawString(
            Game1.smallFont,
            details,
            new Vector2(detailsPanel.X + 12, detailsPanel.Y + 14),
            SubtleTextColor
        );

        this.DrawButton(b, this.RenameButton, this.helper.Translation.Get("edit.rename"), false, this.IsSelected(0));
        this.DrawButton(b, this.CategoryButton, this.helper.Translation.Get("edit.category"), false, this.IsSelected(1));
        this.DrawButton(
            b,
            this.AutoCategoryButton,
            this.station.UseAutomaticCategory
                ? this.helper.Translation.Get("edit.category-auto-enabled")
                : this.helper.Translation.Get("edit.category-auto-enable"),
            false,
            this.IsSelected(2)
        );
        this.DrawButton(b, this.MoveButton, this.helper.Translation.Get("edit.move"), false, this.IsSelected(3));
        this.DrawButton(
            b,
            this.DeleteButton,
            this.confirmDelete
                ? this.helper.Translation.Get("edit.delete-confirm")
                : this.helper.Translation.Get("edit.delete"),
            true,
            this.IsSelected(4)
        );

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

    private Rectangle RenameButton => this.GetButtonBounds(0);
    private Rectangle CategoryButton => this.GetButtonBounds(1);
    private Rectangle AutoCategoryButton => this.GetButtonBounds(2);
    private Rectangle MoveButton => this.GetButtonBounds(3);
    private Rectangle DeleteButton => this.GetButtonBounds(4);

    private int CurrentButtonGap => this.height >= PreferredMenuHeight
        ? PreferredButtonGap
        : 6;

    private int CurrentButtonHeight
    {
        get
        {
            int availableHeight = Math.Max(
                ButtonCount * 24,
                this.height - ButtonAreaTop - ButtonAreaBottomMargin
            );
            int heightWithoutGaps = availableHeight - this.CurrentButtonGap * (ButtonCount - 1);
            return Math.Clamp(heightWithoutGaps / ButtonCount, 24, PreferredButtonHeight);
        }
    }

    private Rectangle GetButtonBounds(int index)
    {
        int x = this.xPositionOnScreen + 48;
        int y = this.yPositionOnScreen + ButtonAreaTop
            + index * (this.CurrentButtonHeight + this.CurrentButtonGap);
        return new Rectangle(x, y, Math.Max(1, this.width - 96), this.CurrentButtonHeight);
    }

    private bool IsSelected(int index)
    {
        return this.controllerNavigationActive && this.selectedButtonIndex == index;
    }

    private void MoveSelection(int delta)
    {
        this.confirmDelete = false;
        this.selectedButtonIndex = Math.Clamp(this.selectedButtonIndex + delta, 0, ButtonCount - 1);
        Game1.playSound("shiny4");
    }

    private void ActivateButton(int index)
    {
        switch (index)
        {
            case 0:
                this.confirmDelete = false;
                this.OpenRenameMenu();
                return;

            case 1:
                this.confirmDelete = false;
                this.OpenCategoryMenu();
                return;

            case 2:
                this.confirmDelete = false;
                bool enabled = !this.station.UseAutomaticCategory;
                this.stations.SetAutomaticCategory(this.station.Id, enabled);
                Game1.playSound("smallSelect");
                return;

            case 3:
                this.confirmDelete = false;
                Game1.exitActiveMenu();
                this.placement.BeginMove(this.station);
                return;

            case 4:
                if (!this.confirmDelete)
                {
                    this.confirmDelete = true;
                    Game1.playSound("smallSelect");
                    return;
                }

                bool removed = this.stations.Remove(this.station.Id);
                Game1.playSound(removed ? "trashcan" : "cancel");
                if (removed)
                    Game1.exitActiveMenu();
                else
                    this.confirmDelete = false;
                return;
        }
    }

    private void ReturnToPreviousMenu()
    {
        this.confirmDelete = false;
        Game1.playSound("bigDeSelect");

        if (this.returnToPreviousMenu is not null)
            this.returnToPreviousMenu();
        else
            Game1.exitActiveMenu();
    }

    private void OpenRenameMenu()
    {
        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new NamingMenu(
            name =>
            {
                if (!string.IsNullOrWhiteSpace(name))
                    this.stations.UpdateName(this.station.Id, name.Trim());

                Game1.activeClickableMenu = new StationEditMenu(
                    this.helper,
                    this.stations,
                    this.regions,
                    this.placement,
                    this.station,
                    this.returnToPreviousMenu
                );
            },
            this.helper.Translation.Get("edit.rename-prompt"),
            this.station.Name
        );
    }

    private void OpenCategoryMenu()
    {
        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new NamingMenu(
            category =>
            {
                if (!string.IsNullOrWhiteSpace(category))
                    this.stations.SetManualCategory(this.station.Id, category.Trim());

                Game1.activeClickableMenu = new StationEditMenu(
                    this.helper,
                    this.stations,
                    this.regions,
                    this.placement,
                    this.station,
                    this.returnToPreviousMenu
                );
            },
            this.helper.Translation.Get("edit.category-prompt"),
            this.regions.GetStationCategory(this.station)
        );
    }

    private void DrawButton(SpriteBatch b, Rectangle bounds, string text, bool destructive, bool selected)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;

        Color tint = destructive
            ? (highlighted ? DangerHoverFill : DangerFill)
            : (highlighted ? ButtonHoverFill : ButtonFill);

        this.DrawVanillaBox(b, bounds, tint, drawShadow: highlighted);

        string fittedText = this.FitText(Game1.smallFont, text, bounds.Width - 32);
        Vector2 size = Game1.smallFont.MeasureString(fittedText);
        Color textColor = destructive ? DangerTextColor : TextColor;

        this.DrawTextWithShadow(
            b,
            Game1.smallFont,
            fittedText,
            new Vector2(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f
            ),
            textColor,
            shadowAlpha: highlighted ? 0.28f : 0.18f
        );
    }

    private void DrawVanillaBox(SpriteBatch b, Rectangle bounds, Color tint, bool drawShadow)
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
            1f,
            drawShadow
        );
    }

    private void DrawTextWithShadow(
        SpriteBatch b,
        SpriteFont font,
        string text,
        Vector2 position,
        Color color,
        float scale = 1f,
        float shadowAlpha = 0.25f)
    {
        b.DrawString(
            font,
            text,
            position + new Vector2(2f, 2f),
            Color.Black * shadowAlpha,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f
        );
        b.DrawString(
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f
        );
    }

    private string FitText(SpriteFont font, string text, float maxWidth, float scale = 1f)
    {
        if (maxWidth <= 0 || font.MeasureString(text).X * scale <= maxWidth)
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
}
