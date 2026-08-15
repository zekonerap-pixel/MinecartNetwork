using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Rendering;

public sealed class MinecartVisualAssets
{
    // Source atlas geometry. Generated styles always preserve these exact dimensions.
    // Four directional frames are stored in order: up, right, down, left.
    public const int EntranceFrameWidth = 48;
    public const int EntranceFrameHeight = 48;
    public const int MinecartFrameWidth = 32;
    public const int MinecartFrameHeight = 32;

    // tracks.png contains vertical then horizontal 16x16 frames.
    public const int TrackFrameSize = 16;

    private readonly IModHelper helper;

    private readonly Dictionary<string, Texture2D> minecartVariants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> trackVariants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> entranceVariants = new(StringComparer.OrdinalIgnoreCase);

    private Texture2D? minecart;
    private Texture2D? tracks;
    private Texture2D? wallHole;
    private bool loaded;

    public MinecartVisualAssets(IModHelper helper)
    {
        this.helper = helper;

        // Keep config.json functional even when Generic Mod Config Menu isn't installed.
        StationVisualSettings.Apply(helper.ReadConfig<ModConfig>());
    }

    public Texture2D? Minecart
    {
        get
        {
            this.EnsureLoaded();
            return this.GetSelectedTexture(
                this.minecart,
                this.minecartVariants,
                StationVisualSettings.MinecartStyle
            );
        }
    }

    public Texture2D? Tracks
    {
        get
        {
            this.EnsureLoaded();
            return this.GetSelectedTexture(
                this.tracks,
                this.trackVariants,
                StationVisualSettings.TrackStyle
            );
        }
    }

    public Texture2D? WallHole
    {
        get
        {
            this.EnsureLoaded();
            return this.GetSelectedTexture(
                this.wallHole,
                this.entranceVariants,
                StationVisualSettings.EntranceStyle
            );
        }
    }

    public Rectangle GetEntranceSourceRect(int direction)
    {
        int frame = NormalizeDirection(direction);
        return new Rectangle(frame * EntranceFrameWidth, 0, EntranceFrameWidth, EntranceFrameHeight);
    }

    public Rectangle GetMinecartSourceRect(int direction)
    {
        int frame = NormalizeDirection(direction);
        return new Rectangle(frame * MinecartFrameWidth, 0, MinecartFrameWidth, MinecartFrameHeight);
    }

    public Rectangle GetTrackSourceRect(int direction)
    {
        bool vertical = NormalizeDirection(direction) is 0 or 2;
        int x = vertical ? 0 : TrackFrameSize;
        return new Rectangle(x, 0, TrackFrameSize, TrackFrameSize);
    }

    public void Invalidate()
    {
        this.DisposeGenerated(this.minecartVariants);
        this.DisposeGenerated(this.trackVariants);
        this.DisposeGenerated(this.entranceVariants);

        this.loaded = false;
        this.minecart = null;
        this.tracks = null;
        this.wallHole = null;
    }

    private void EnsureLoaded()
    {
        if (this.loaded)
            return;

        this.loaded = true;
        this.minecart = this.TryLoad("assets/minecart.png");
        this.tracks = this.TryLoad("assets/tracks.png");
        this.wallHole = this.TryLoad("assets/mine_entrance.png");

        this.BuildGeneratedVariants(this.minecart, this.minecartVariants);
        this.BuildGeneratedVariants(this.tracks, this.trackVariants);
        this.BuildGeneratedVariants(this.wallHole, this.entranceVariants);
    }

    private Texture2D? TryLoad(string relativePath)
    {
        string diskPath = Path.Combine(
            this.helper.DirectoryPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(diskPath))
            return null;

        try
        {
            return this.helper.ModContent.Load<Texture2D>(relativePath);
        }
        catch
        {
            return null;
        }
    }

    private Texture2D? GetSelectedTexture(
        Texture2D? source,
        IReadOnlyDictionary<string, Texture2D> variants,
        string? requestedStyle)
    {
        if (source is null)
            return null;

        string style = ModConfig.NormalizeStationVisualStyle(requestedStyle);
        if (style == ModConfig.StationVisualLegacyCurrent)
            return source;

        return variants.TryGetValue(style, out Texture2D? texture)
            ? texture
            : source;
    }

    private void BuildGeneratedVariants(Texture2D? source, IDictionary<string, Texture2D> target)
    {
        if (source is null)
            return;

        foreach (string style in ModConfig.StationVisualStyles)
        {
            if (style == ModConfig.StationVisualLegacyCurrent)
                continue;

            VisualPalette? palette = GetPalette(style);
            if (palette is null)
                continue;

            target[style] = this.GenerateVariant(source, palette.Value);
        }
    }

    private Texture2D GenerateVariant(Texture2D source, VisualPalette palette)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            if (pixel.A == 0)
                continue;

            float r = pixel.R / 255f;
            float g = pixel.G / 255f;
            float b = pixel.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float saturation = max <= 0.0001f ? 0f : (max - min) / max;
            float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;

            bool glow = pixel.R > 175
                && pixel.G > 115
                && pixel.B < 115
                && pixel.R + pixel.G > 330;

            Color replacement;
            if (glow)
            {
                float whiteMix = Math.Clamp((luminance - 0.55f) * 0.9f, 0f, 0.5f);
                replacement = Mix(palette.Glow, new Color(255, 255, 245), whiteMix);
            }
            else if (saturation < 0.23f)
            {
                replacement = Ramp(
                    palette.MetalDark,
                    palette.MetalMid,
                    palette.MetalLight,
                    luminance
                );
            }
            else
            {
                replacement = Ramp(
                    palette.WarmDark,
                    palette.WarmMid,
                    palette.WarmLight,
                    luminance
                );
            }

            replacement.A = pixel.A;
            pixels[i] = replacement;
        }

        var result = new Texture2D(
            Game1.graphics.GraphicsDevice,
            source.Width,
            source.Height,
            false,
            SurfaceFormat.Color
        );
        result.SetData(pixels);
        return result;
    }

    private static Color Ramp(Color dark, Color middle, Color light, float luminance)
    {
        luminance = Math.Clamp(luminance, 0f, 1f);
        if (luminance < 0.48f)
            return Mix(dark, middle, luminance / 0.48f);

        return Mix(middle, light, (luminance - 0.48f) / 0.52f);
    }

    private static Color Mix(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount),
            (byte)255
        );
    }

    private static VisualPalette? GetPalette(string style)
    {
        return style switch
        {
            ModConfig.StationVisualRustic => new VisualPalette(
                new Color(44, 24, 14),
                new Color(126, 74, 37),
                new Color(216, 151, 68),
                new Color(50, 47, 42),
                new Color(110, 108, 99),
                new Color(196, 191, 170),
                new Color(255, 218, 112)
            ),
            ModConfig.StationVisualCopper => new VisualPalette(
                new Color(55, 22, 12),
                new Color(151, 64, 28),
                new Color(231, 128, 51),
                new Color(67, 31, 23),
                new Color(157, 74, 47),
                new Color(229, 145, 95),
                new Color(255, 198, 90)
            ),
            ModConfig.StationVisualDarkIron => new VisualPalette(
                new Color(35, 24, 21),
                new Color(83, 59, 48),
                new Color(151, 114, 82),
                new Color(26, 29, 33),
                new Color(69, 77, 84),
                new Color(143, 154, 158),
                new Color(255, 182, 94)
            ),
            ModConfig.StationVisualMoss => new VisualPalette(
                new Color(35, 30, 18),
                new Color(91, 91, 39),
                new Color(170, 157, 71),
                new Color(34, 49, 38),
                new Color(74, 103, 72),
                new Color(147, 166, 116),
                new Color(238, 232, 120)
            ),
            ModConfig.StationVisualCrystal => new VisualPalette(
                new Color(30, 25, 48),
                new Color(76, 68, 119),
                new Color(147, 139, 210),
                new Color(24, 42, 55),
                new Color(61, 114, 138),
                new Color(149, 213, 224),
                new Color(186, 246, 255)
            ),
            _ => null
        };
    }

    private void DisposeGenerated(IDictionary<string, Texture2D> variants)
    {
        foreach (Texture2D texture in variants.Values)
            texture.Dispose();

        variants.Clear();
    }

    private static int NormalizeDirection(int direction)
    {
        int normalized = direction % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }

    private readonly record struct VisualPalette(
        Color WarmDark,
        Color WarmMid,
        Color WarmLight,
        Color MetalDark,
        Color MetalMid,
        Color MetalLight,
        Color Glow
    );
}
