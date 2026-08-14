using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecartNetwork.Models;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MinecartNetwork.Menus;

public sealed class StationEditMenu : IClickableMenu
{
    private const int MenuWidth = 620;
    private const int MenuHeight = 500;
    private const int ButtonHeight = 58;
    private const int ButtonGap = 14;

    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly PlacementManager placement;
    private readonly MinecartStation station;

    private bool confirmDelete;

    public StationEditMenu(
        IModHelper helper,
        StationManager stations,
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
        this.placement = placement;
        this.station = station;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

        if (this.RenameButton.Contains(x, y))
        {
            this.confirmDelete = false;
            this.OpenRenameMenu();
            return;
        }

        if (this.CategoryButton.Contains(x, y))
        {
            this.confirmDelete = false;
            this.OpenCategoryMenu();
            return;
        }

        if (this.MoveButton.Contains(x, y))
        {
            this.confirmDelete = false;
            Game1.exitActiveMenu();
            this.placement.BeginMove(this.station);
            return;
        }

        if (this.DeleteButton.Contains(x, y))
        {
            if (!this.confirmDelete)
            {
                this.confirmDelete = true;
                Game1.playSound("smallSelect");
                return;
            }

            bool removed = this.stations.Remove(this.station.Id);
            Game1.playSound(removed ? "trashcan" : "cancel");
            Game1.exitActiveMenu();
        }
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

        string details = this.helper.Translation.Get("edit.details", new
        {
            name = this.station.Name,
            category = this.station.Category
        });
        b.DrawString(
            Game1.smallFont,
            details,
            new Vector2(this.xPositionOnScreen + 36, this.yPositionOnScreen + 84),
            Color.LightGray
        );

        this.DrawButton(b, this.RenameButton, this.helper.Translation.Get("edit.rename"), false);
        this.DrawButton(b, this.CategoryButton, this.helper.Translation.Get("edit.category"), false);
        this.DrawButton(b, this.MoveButton, this.helper.Translation.Get("edit.move"), false);
        this.DrawButton(
            b,
            this.DeleteButton,
            this.confirmDelete
                ? this.helper.Translation.Get("edit.delete-confirm")
                : this.helper.Translation.Get("edit.delete"),
            true
        );

        this.upperRightCloseButton?.draw(b);
        this.drawMouse(b);
    }

    private Rectangle RenameButton => this.GetButtonBounds(0);
    private Rectangle CategoryButton => this.GetButtonBounds(1);
    private Rectangle MoveButton => this.GetButtonBounds(2);
    private Rectangle DeleteButton => this.GetButtonBounds(3);

    private Rectangle GetButtonBounds(int index)
    {
        int x = this.xPositionOnScreen + 48;
        int y = this.yPositionOnScreen + 146 + index * (ButtonHeight + ButtonGap);
        return new Rectangle(x, y, this.width - 96, ButtonHeight);
    }

    private void OpenRenameMenu()
    {
        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new NamingMenu(
            name =>
            {
                if (!string.IsNullOrWhiteSpace(name))
                    this.stations.UpdateDetails(this.station.Id, name.Trim(), this.station.Category);

                Game1.activeClickableMenu = new StationEditMenu(
                    this.helper,
                    this.stations,
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
                    this.stations.UpdateDetails(this.station.Id, this.station.Name, category.Trim());

                Game1.activeClickableMenu = new StationEditMenu(
                    this.helper,
                    this.stations,
                    this.placement,
                    this.station
                );
            },
            this.helper.Translation.Get("edit.category-prompt"),
            this.station.Category
        );
    }

    private void DrawButton(SpriteBatch b, Rectangle bounds, string text, bool destructive)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        Color fill = destructive
            ? (hovered ? new Color(115, 50, 45) : new Color(82, 43, 40))
            : (hovered ? new Color(91, 72, 57) : new Color(59, 50, 45));
        Color border = destructive ? new Color(151, 72, 65) : new Color(115, 86, 63);

        this.Fill(b, bounds, fill);
        this.Outline(b, bounds, border, hovered ? 3 : 2);

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
