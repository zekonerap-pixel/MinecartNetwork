using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MinecartNetwork.Menus;

public sealed class NetworkManagementMenu : IClickableMenu
{
    private const int PreferredWidth = 620;
    private const int PreferredHeight = 360;
    private const int ViewportMargin = 48;

    private static readonly Rectangle MenuBoxSource = new(0, 256, 60, 60);
    private static readonly Color TextColor = new(86, 22, 12);
    private static readonly Color SubtleTextColor = new(120, 78, 48);
    private static readonly Color ButtonFill = new(255, 235, 177);
    private static readonly Color ButtonHoverFill = new(255, 216, 126);

    private readonly IModHelper helper;
    private readonly PlacementManager placement;
    private readonly ModConfig config;
    private bool controllerNavigationActive;

    public NetworkManagementMenu(
        IModHelper helper,
        PlacementManager placement,
        ModConfig config)
        : base(
            (Game1.uiViewport.Width - GetMenuWidth()) / 2,
            (Game1.uiViewport.Height - GetMenuHeight()) / 2,
            GetMenuWidth(),
            GetMenuHeight(),
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.placement = placement;
        this.config = config;
    }

    private bool UseBasicStyle => ModConfig.IsBasicMenuStyle(this.config.MenuStyle);

    private int BuildCost => Math.Max(0, this.config.StationBuildCost);

    private bool CanAfford => Game1.player.Money >= this.BuildCost;

    private Rectangle BuildButton => new(
        this.xPositionOnScreen + 54,
        this.yPositionOnScreen + this.height - 100,
        Math.Max(1, this.width - 108),
        58
    );

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.controllerNavigationActive = false;
        base.receiveLeftClick(x, y, playSound);

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
            return;

        if (this.BuildButton.Contains(x, y))
            this.OpenBuildNamingMenu();
    }

    public override void receiveGamePadButton(Buttons button)
    {
        this.controllerNavigationActive = true;

        switch (button)
        {
            case Buttons.B:
                Game1.exitActiveMenu();
                return;

            case Buttons.A:
                this.OpenBuildNamingMenu();
                return;
        }

        base.receiveGamePadButton(button);
    }

    public override void draw(SpriteBatch b)
    {
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        b.Draw(Game1.staminaRect, viewport, Color.Black * (this.UseBasicStyle ? 0.45f : 0.38f));

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

        string title = this.FitText(
            Game1.dialogueFont,
            this.helper.Translation.Get("management.title").ToString(),
            this.width - 108
        );
        this.DrawCenteredText(
            b,
            Game1.dialogueFont,
            title,
            new Rectangle(this.xPositionOnScreen + 40, this.yPositionOnScreen + 24, this.width - 80, 48),
            this.UseBasicStyle ? Color.Wheat : TextColor
        );

        string description = this.FitText(
            Game1.smallFont,
            this.helper.Translation.Get("management.description").ToString(),
            this.width - 120
        );
        this.DrawCenteredText(
            b,
            Game1.smallFont,
            description,
            new Rectangle(this.xPositionOnScreen + 50, this.yPositionOnScreen + 92, this.width - 100, 40),
            this.UseBasicStyle ? Color.LightGray : SubtleTextColor
        );

        string balance = this.helper.Translation.Get("management.balance", new
        {
            money = FormatMoney(Game1.player.Money)
        }).ToString();
        this.DrawCenteredText(
            b,
            Game1.smallFont,
            balance,
            new Rectangle(this.xPositionOnScreen + 50, this.yPositionOnScreen + 142, this.width - 100, 36),
            this.UseBasicStyle ? Color.LightGray : SubtleTextColor
        );

        if (!this.CanAfford)
        {
            string insufficient = this.helper.Translation.Get("management.insufficient-funds", new
            {
                cost = FormatMoney(this.BuildCost)
            }).ToString();
            this.DrawCenteredText(
                b,
                Game1.smallFont,
                this.FitText(Game1.smallFont, insufficient, this.width - 120),
                new Rectangle(this.xPositionOnScreen + 50, this.yPositionOnScreen + 184, this.width - 100, 34),
                this.UseBasicStyle ? Color.Salmon : new Color(158, 56, 40)
            );
        }

        this.DrawBuildButton(b);
        this.upperRightCloseButton?.draw(b);
        this.drawMouse(b);
    }

    private void OpenBuildNamingMenu()
    {
        if (!this.CanAfford)
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(this.helper.Translation.Get("management.insufficient-funds", new
            {
                cost = FormatMoney(this.BuildCost)
            }).ToString());
            return;
        }

        int cost = this.BuildCost;
        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new NamingMenu(
            name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    this.Reopen();
                    return;
                }

                Game1.exitActiveMenu();
                if (this.placement.Begin(name.Trim(), buildCost: cost))
                    return;

                Game1.playSound("cancel");
                if (Game1.activeClickableMenu is null)
                {
                    Game1.showRedMessage(
                        this.helper.Translation.Get("management.could-not-start").ToString()
                    );
                }
            },
            this.helper.Translation.Get("management.name-prompt").ToString(),
            this.helper.Translation.Get("station.default-name").ToString()
        );
    }

    private void Reopen()
    {
        Game1.activeClickableMenu = new NetworkManagementMenu(
            this.helper,
            this.placement,
            this.config
        );
    }

    private void DrawBuildButton(SpriteBatch b)
    {
        Rectangle bounds = this.BuildButton;
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = this.CanAfford && (hovered || this.controllerNavigationActive);

        string text = this.helper.Translation.Get("management.build", new
        {
            cost = FormatMoney(this.BuildCost)
        }).ToString();
        text = this.FitText(Game1.smallFont, text, bounds.Width - 32);

        Color textColor;
        if (this.UseBasicStyle)
        {
            Color fill = !this.CanAfford
                ? new Color(48, 46, 44)
                : highlighted ? new Color(94, 72, 55) : new Color(59, 50, 45);
            Color border = !this.CanAfford
                ? new Color(76, 72, 68)
                : highlighted ? new Color(155, 116, 80) : new Color(115, 86, 63);
            this.Fill(b, bounds, fill);
            this.Outline(b, bounds, border, highlighted ? 3 : 2);
            textColor = this.CanAfford ? Color.White : Color.Gray;
        }
        else
        {
            Color tint = !this.CanAfford
                ? new Color(205, 200, 184)
                : highlighted ? ButtonHoverFill : ButtonFill;
            this.DrawVanillaBox(b, bounds, tint, drawShadow: highlighted);
            textColor = this.CanAfford ? TextColor : Color.Gray;
        }

        this.DrawCenteredText(b, Game1.smallFont, text, bounds, textColor);
    }

    private void DrawCenteredText(
        SpriteBatch b,
        SpriteFont font,
        string text,
        Rectangle bounds,
        Color color)
    {
        Vector2 size = font.MeasureString(text);
        Vector2 position = new(
            bounds.X + (bounds.Width - size.X) / 2f,
            bounds.Y + (bounds.Height - size.Y) / 2f
        );

        if (!this.UseBasicStyle)
        {
            b.DrawString(
                font,
                text,
                position + new Vector2(2f, 2f),
                Color.Black * 0.20f
            );
        }

        b.DrawString(font, text, position, color);
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

    private void Fill(SpriteBatch b, Rectangle bounds, Color color)
    {
        b.Draw(Game1.staminaRect, bounds, color);
    }

    private void Outline(SpriteBatch b, Rectangle bounds, Color color, int thickness)
    {
        this.Fill(b, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        this.Fill(b, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        this.Fill(b, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        this.Fill(b, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private string FitText(SpriteFont font, string text, float maxWidth)
    {
        if (maxWidth <= 0 || font.MeasureString(text).X <= maxWidth)
            return text;

        const string suffix = "...";
        string candidate = text;
        while (candidate.Length > 0 && font.MeasureString(candidate + suffix).X > maxWidth)
            candidate = candidate[..^1];

        return candidate.Length == 0 ? suffix : candidate.TrimEnd() + suffix;
    }

    private static string FormatMoney(int value)
        => Math.Max(0, value).ToString("N0");

    private static int GetMenuWidth()
    {
        int available = Math.Max(1, Game1.uiViewport.Width - ViewportMargin * 2);
        return Math.Min(PreferredWidth, available);
    }

    private static int GetMenuHeight()
    {
        int available = Math.Max(1, Game1.uiViewport.Height - ViewportMargin * 2);
        return Math.Min(PreferredHeight, available);
    }
}
