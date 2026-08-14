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
    private const int MenuWidth = 620;
    private const int MenuHeight = 570;
    private const int ButtonHeight = 52;
    private const int ButtonGap = 10;
    private const int ButtonCount = 5;

    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly LocationRegionService regions;
    private readonly PlacementManager placement;
    private readonly MinecartStation station;

    private bool confirmDelete;
    private int selectedButtonIndex;
    private bool controllerNavigationActive;

    public StationEditMenu(
        IModHelper helper,
        StationManager stations,
        LocationRegionService regions,
        PlacementManager placement,
        MinecartStation station)
        : base(
            Game1.uiViewport.Width / 2 - MenuWidth / 2,
            Game1.uiViewport.Height / 2 - MenuHeight / 2,
            MenuWidth,
            MenuHeight,
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.stations = stations;
        this.regions = regions;
        this.placement = placement;
        this.station = station;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        this.controllerNavigationActive = false;

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

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

            case Buttons.A:
                this.ActivateButton(this.selectedButtonIndex);
                return;
        }

        base.receiveGamePadButton(button);
    }

    public override void draw(SpriteBatch b)
    {
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        b.Draw(Game1.staminaRect, viewport, Color.Black * 0.45f);

        Rectangle panel = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        this.Fill(b, panel, new Color(37, 31, 28) * 0.98f);
        this.Outline(b, panel, new Color(121, 88, 60), 5);

        b.DrawString(
            Game1.dialogueFont,
            this.helper.Translation.Get("edit.title"),
            new Vector2(this.xPositionOnScreen + 34, this.yPositionOnScreen + 24),
            Color.Wheat
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
        b.DrawString(
            Game1.smallFont,
            details,
            new Vector2(this.xPositionOnScreen + 36, this.yPositionOnScreen + 82),
            Color.LightGray
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

    private Rectangle RenameButton => this.GetButtonBounds(0);
    private Rectangle CategoryButton => this.GetButtonBounds(1);
    private Rectangle AutoCategoryButton => this.GetButtonBounds(2);
    private Rectangle MoveButton => this.GetButtonBounds(3);
    private Rectangle DeleteButton => this.GetButtonBounds(4);

    private Rectangle GetButtonBounds(int index)
    {
        int x = this.xPositionOnScreen + 48;
        int y = this.yPositionOnScreen + 164 + index * (ButtonHeight + ButtonGap);
        return new Rectangle(x, y, this.width - 96, ButtonHeight);
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
                    this.station
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
                    this.station
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
        Color fill = destructive
            ? (highlighted ? new Color(115, 50, 45) : new Color(82, 43, 40))
            : (highlighted ? new Color(91, 72, 57) : new Color(59, 50, 45));
        Color border = destructive ? new Color(151, 72, 65) : new Color(115, 86, 63);

        this.Fill(b, bounds, fill);
        this.Outline(b, bounds, border, highlighted ? 3 : 2);

        Vector2 size = Game1.smallFont.MeasureString(text);
        b.DrawString(
            Game1.smallFont,
            text,
            new Vector2(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f
            ),
            Color.White
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
}
