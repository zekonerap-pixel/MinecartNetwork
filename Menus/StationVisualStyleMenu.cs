using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecartNetwork.Models;
using MinecartNetwork.Rendering;
using MinecartNetwork.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MinecartNetwork.Menus;

public sealed class StationVisualStyleMenu : IClickableMenu
{
    private const int PreferredWidth = 690;
    private const int PreferredHeight = 590;
    private const int ViewportMargin = 48;
    private const int ButtonCount = 5;
    private const int ButtonHeight = 58;
    private const int ButtonGap = 10;
    private const int ButtonTop = 210;

    private static readonly Rectangle MenuBoxSource = new(0, 256, 60, 60);
    private static readonly Color TextColor = new(86, 22, 12);
    private static readonly Color SubtleTextColor = new(120, 78, 48);
    private static readonly Color ButtonFill = new(255, 235, 177);
    private static readonly Color ButtonHoverFill = new(255, 216, 126);
    private static readonly Color ActiveFill = new(214, 236, 166);

    private readonly IModHelper helper;
    private readonly StationManager stations;
    private readonly ModConfig config;
    private readonly MinecartStation station;
    private readonly Action returnToEditMenu;
    private readonly StationVisualStyleResolver resolver;
    private readonly IReadOnlyList<string> availableStyles;

    private int selectedButtonIndex;
    private bool controllerNavigationActive;

    public StationVisualStyleMenu(
        IModHelper helper,
        StationManager stations,
        LocationRegionService regions,
        ModConfig config,
        MinecartStation station,
        Action returnToEditMenu)
        : base(
            (Game1.uiViewport.Width - GetMenuWidth()) / 2,
            (Game1.uiViewport.Height - GetMenuHeight()) / 2,
            GetMenuWidth(),
            GetMenuHeight(),
            showUpperRightCloseButton: true)
    {
        this.helper = helper;
        this.stations = stations;
        this.config = config;
        this.station = station;
        this.returnToEditMenu = returnToEditMenu;
        this.resolver = new StationVisualStyleResolver(helper, regions, config);
        this.availableStyles = this.resolver.AvailableStyles;
    }

    private bool UseBasicStyle => ModConfig.IsBasicMenuStyle(this.config.MenuStyle);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.controllerNavigationActive = false;

        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
        {
            this.ReturnToEditor();
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
                this.ReturnToEditor();
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
            this.helper.Translation.Get("style.title"),
            this.width - 108
        );

        this.DrawLabel(
            b,
            Game1.dialogueFont,
            title,
            new Vector2(this.xPositionOnScreen + 36, this.yPositionOnScreen + 26),
            this.UseBasicStyle ? Color.Wheat : TextColor
        );

        ResolvedStationVisualStyles effective = this.resolver.Resolve(this.station);
        string mode = this.resolver.GetModeDisplayName(this.station.VisualStyleMode);
        string details = this.helper.Translation.Get("style.details", new
        {
            mode,
            minecart = this.resolver.GetStyleDisplayName(effective.MinecartStyle),
            entrance = this.resolver.GetStyleDisplayName(effective.EntranceStyle),
            tracks = this.resolver.GetStyleDisplayName(effective.TrackStyle)
        });

        Rectangle detailsPanel = new(
            this.xPositionOnScreen + 32,
            this.yPositionOnScreen + 84,
            this.width - 64,
            92
        );

        if (this.UseBasicStyle)
        {
            this.Fill(b, detailsPanel, new Color(49, 43, 40));
            this.Outline(b, detailsPanel, new Color(75, 66, 59), 1);
        }
        else
        {
            this.DrawVanillaBox(b, detailsPanel, new Color(255, 245, 210), drawShadow: false);
        }

        this.DrawWrappedText(
            b,
            details,
            new Vector2(detailsPanel.X + 14, detailsPanel.Y + 13),
            detailsPanel.Width - 28,
            this.UseBasicStyle ? Color.LightGray : SubtleTextColor
        );

        string currentMode = ModConfig.NormalizeStationVisualMode(this.station.VisualStyleMode);
        this.DrawButton(
            b,
            this.GetButtonBounds(0),
            this.helper.Translation.Get("style.use-default"),
            currentMode == ModConfig.StationVisualModeDefault,
            this.IsSelected(0)
        );
        this.DrawButton(
            b,
            this.GetButtonBounds(1),
            this.helper.Translation.Get("style.use-automatic"),
            currentMode == ModConfig.StationVisualModeAutomatic,
            this.IsSelected(1)
        );
        this.DrawButton(
            b,
            this.GetButtonBounds(2),
            this.helper.Translation.Get("style.minecart", new
            {
                style = this.resolver.GetStyleDisplayName(effective.MinecartStyle)
            }),
            currentMode == ModConfig.StationVisualModeCustom,
            this.IsSelected(2)
        );
        this.DrawButton(
            b,
            this.GetButtonBounds(3),
            this.helper.Translation.Get("style.entrance", new
            {
                style = this.resolver.GetStyleDisplayName(effective.EntranceStyle)
            }),
            currentMode == ModConfig.StationVisualModeCustom,
            this.IsSelected(3)
        );
        this.DrawButton(
            b,
            this.GetButtonBounds(4),
            this.helper.Translation.Get("style.tracks", new
            {
                style = this.resolver.GetStyleDisplayName(effective.TrackStyle)
            }),
            currentMode == ModConfig.StationVisualModeCustom,
            this.IsSelected(4)
        );

        this.upperRightCloseButton?.draw(b);
        this.drawMouse(b);
    }

    private void ActivateButton(int index)
    {
        switch (index)
        {
            case 0:
                this.stations.SetVisualStyleMode(this.station.Id, ModConfig.StationVisualModeDefault);
                Game1.playSound("smallSelect");
                return;

            case 1:
                this.stations.SetVisualStyleMode(this.station.Id, ModConfig.StationVisualModeAutomatic);
                Game1.playSound("smallSelect");
                return;

            case 2:
                this.stations.SetMinecartVisualStyle(
                    this.station.Id,
                    this.GetNextStyle(this.resolver.Resolve(this.station).MinecartStyle)
                );
                Game1.playSound("shiny4");
                return;

            case 3:
                this.stations.SetEntranceVisualStyle(
                    this.station.Id,
                    this.GetNextStyle(this.resolver.Resolve(this.station).EntranceStyle)
                );
                Game1.playSound("shiny4");
                return;

            case 4:
                this.stations.SetTrackVisualStyle(
                    this.station.Id,
                    this.GetNextStyle(this.resolver.Resolve(this.station).TrackStyle)
                );
                Game1.playSound("shiny4");
                return;
        }
    }

    private string GetNextStyle(string current)
    {
        if (this.availableStyles.Count == 0)
            return ModConfig.StationVisualLegacyCurrent;

        int index = -1;
        for (int i = 0; i < this.availableStyles.Count; i++)
        {
            if (this.availableStyles[i].Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        return this.availableStyles[(index + 1 + this.availableStyles.Count) % this.availableStyles.Count];
    }

    private void ReturnToEditor()
    {
        Game1.playSound("bigDeSelect");
        this.returnToEditMenu();
    }

    private void MoveSelection(int delta)
    {
        this.selectedButtonIndex = Math.Clamp(this.selectedButtonIndex + delta, 0, ButtonCount - 1);
        Game1.playSound("shiny4");
    }

    private bool IsSelected(int index)
        => this.controllerNavigationActive && this.selectedButtonIndex == index;

    private Rectangle GetButtonBounds(int index)
    {
        int x = this.xPositionOnScreen + 48;
        int availableHeight = this.height - ButtonTop - 32;
        int gap = this.height >= PreferredHeight ? ButtonGap : 6;
        int height = Math.Clamp(
            (availableHeight - gap * (ButtonCount - 1)) / ButtonCount,
            30,
            ButtonHeight
        );
        int y = this.yPositionOnScreen + ButtonTop + index * (height + gap);
        return new Rectangle(x, y, Math.Max(1, this.width - 96), height);
    }

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

    private void DrawButton(SpriteBatch b, Rectangle bounds, string text, bool active, bool selected)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool highlighted = hovered || selected;
        string fitted = this.FitText(Game1.smallFont, text, bounds.Width - 32);
        Vector2 size = Game1.smallFont.MeasureString(fitted);

        if (this.UseBasicStyle)
        {
            Color fill = active
                ? new Color(73, 86, 55)
                : (highlighted ? new Color(94, 72, 55) : new Color(59, 50, 45));
            Color border = highlighted
                ? new Color(155, 116, 80)
                : new Color(115, 86, 63);
            this.Fill(b, bounds, fill);
            this.Outline(b, bounds, border, highlighted ? 3 : 2);
            b.DrawString(
                Game1.smallFont,
                fitted,
                new Vector2(
                    bounds.X + (bounds.Width - size.X) / 2f,
                    bounds.Y + (bounds.Height - size.Y) / 2f
                ),
                Color.White
            );
            return;
        }

        Color tint = active ? ActiveFill : (highlighted ? ButtonHoverFill : ButtonFill);
        this.DrawVanillaBox(b, bounds, tint, drawShadow: highlighted);
        this.DrawLabel(
            b,
            Game1.smallFont,
            fitted,
            new Vector2(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f
            ),
            TextColor
        );
    }

    private void DrawWrappedText(SpriteBatch b, string text, Vector2 position, float maxWidth, Color color)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string line = string.Empty;
        float y = position.Y;

        foreach (string word in words)
        {
            string candidate = line.Length == 0 ? word : $"{line} {word}";
            if (Game1.smallFont.MeasureString(candidate).X > maxWidth && line.Length > 0)
            {
                b.DrawString(Game1.smallFont, line, new Vector2(position.X, y), color);
                y += Game1.smallFont.LineSpacing;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            b.DrawString(Game1.smallFont, line, new Vector2(position.X, y), color);
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

    private void DrawLabel(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color)
    {
        if (!this.UseBasicStyle)
        {
            b.DrawString(font, text, position + new Vector2(2f, 2f), Color.Black * 0.22f);
        }
        b.DrawString(font, text, position, color);
    }

    private string FitText(SpriteFont font, string text, float maxWidth)
    {
        if (maxWidth <= 0 || font.MeasureString(text).X <= maxWidth)
            return text;

        const string suffix = "...";
        int low = 0;
        int high = text.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            string candidate = text[..middle].TrimEnd() + suffix;
            if (font.MeasureString(candidate).X <= maxWidth)
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
}
