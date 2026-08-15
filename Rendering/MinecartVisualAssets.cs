using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MinecartNetwork.Rendering;

public sealed class MinecartVisualAssets
{
    // Source atlas geometry. Every style keeps these exact dimensions.
    // Four directional frames are stored in order: up, right, down, left.
    public const int EntranceFrameWidth = 48;
    public const int EntranceFrameHeight = 48;
    public const int MinecartFrameWidth = 32;
    public const int MinecartFrameHeight = 32;

    // tracks.png contains vertical then horizontal 16x16 frames.
    public const int TrackFrameSize = 16;

    public const int MinecartAtlasWidth = MinecartFrameWidth * 4;
    public const int MinecartAtlasHeight = MinecartFrameHeight;
    public const int EntranceAtlasWidth = EntranceFrameWidth * 4;
    public const int EntranceAtlasHeight = EntranceFrameHeight;
    public const int TrackAtlasWidth = TrackFrameSize * 2;
    public const int TrackAtlasHeight = TrackFrameSize;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    private readonly Dictionary<string, Texture2D> minecartVariants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> trackVariants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> entranceVariants = new(StringComparer.OrdinalIgnoreCase);

    private Texture2D? minecart;
    private Texture2D? tracks;
    private Texture2D? wallHole;
    private bool loaded;

    public MinecartVisualAssets(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;

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

    public static string[] GetAvailableStyles(IModHelper helper)
    {
        var styles = new List<string>
        {
            ModConfig.StationVisualLegacyCurrent
        };

        string stylesRoot = Path.Combine(helper.DirectoryPath, "assets", "styles");
        if (!Directory.Exists(stylesRoot))
            return styles.ToArray();

        foreach (string directory in Directory
                     .EnumerateDirectories(stylesRoot)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            string? style = Path.GetFileName(directory);
            if (!ModConfig.IsSafeStationVisualStyleName(style))
                continue;

            bool complete = File.Exists(Path.Combine(directory, "minecart.png"))
                && File.Exists(Path.Combine(directory, "mine_entrance.png"))
                && File.Exists(Path.Combine(directory, "tracks.png"));

            if (complete && !styles.Contains(style, StringComparer.OrdinalIgnoreCase))
                styles.Add(style);
        }

        return styles.ToArray();
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
        this.minecartVariants.Clear();
        this.trackVariants.Clear();
        this.entranceVariants.Clear();

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

        // Original/current artwork.
        this.minecart = this.TryLoadValidated(
            "assets/minecart.png",
            MinecartAtlasWidth,
            MinecartAtlasHeight,
            "current minecart"
        );
        this.tracks = this.TryLoadValidated(
            "assets/tracks.png",
            TrackAtlasWidth,
            TrackAtlasHeight,
            "current tracks"
        );
        this.wallHole = this.TryLoadValidated(
            "assets/mine_entrance.png",
            EntranceAtlasWidth,
            EntranceAtlasHeight,
            "current entrance"
        );

        string[] availableStyles = GetAvailableStyles(this.helper);
        foreach (string style in availableStyles)
        {
            if (style.Equals(ModConfig.StationVisualLegacyCurrent, StringComparison.OrdinalIgnoreCase))
                continue;

            this.TryAddVariant(
                this.minecartVariants,
                style,
                $"assets/styles/{style}/minecart.png",
                MinecartAtlasWidth,
                MinecartAtlasHeight
            );
            this.TryAddVariant(
                this.trackVariants,
                style,
                $"assets/styles/{style}/tracks.png",
                TrackAtlasWidth,
                TrackAtlasHeight
            );
            this.TryAddVariant(
                this.entranceVariants,
                style,
                $"assets/styles/{style}/mine_entrance.png",
                EntranceAtlasWidth,
                EntranceAtlasHeight
            );
        }

        string loadedStyles = string.Join(
            ", ",
            availableStyles.Where(style =>
                style.Equals(ModConfig.StationVisualLegacyCurrent, StringComparison.OrdinalIgnoreCase)
                || (this.minecartVariants.ContainsKey(style)
                    && this.trackVariants.ContainsKey(style)
                    && this.entranceVariants.ContainsKey(style)))
        );

        this.monitor.Log(
            $"Station visual styles recognized: {loadedStyles}.",
            LogLevel.Debug
        );
    }

    private void TryAddVariant(
        IDictionary<string, Texture2D> target,
        string style,
        string relativePath,
        int expectedWidth,
        int expectedHeight)
    {
        Texture2D? texture = this.TryLoadValidated(
            relativePath,
            expectedWidth,
            expectedHeight,
            $"'{style}'"
        );

        if (texture is not null)
            target[style] = texture;
    }

    private Texture2D? TryLoadValidated(
        string relativePath,
        int expectedWidth,
        int expectedHeight,
        string label)
    {
        string diskPath = Path.Combine(
            this.helper.DirectoryPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(diskPath))
        {
            this.monitor.Log(
                $"Station visual asset not found for {label}: {relativePath}",
                LogLevel.Warn
            );
            return null;
        }

        try
        {
            Texture2D texture = this.helper.ModContent.Load<Texture2D>(relativePath);
            if (texture.Width != expectedWidth || texture.Height != expectedHeight)
            {
                this.monitor.Log(
                    $"Ignoring station visual asset '{relativePath}': expected {expectedWidth}x{expectedHeight}, got {texture.Width}x{texture.Height}.",
                    LogLevel.Warn
                );
                return null;
            }

            return texture;
        }
        catch (Exception ex)
        {
            this.monitor.Log(
                $"Couldn't load station visual asset '{relativePath}': {ex.Message}",
                LogLevel.Warn
            );
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
        if (style.Equals(ModConfig.StationVisualLegacyCurrent, StringComparison.OrdinalIgnoreCase))
            return source;

        if (variants.TryGetValue(style, out Texture2D? texture))
            return texture;

        this.monitor.Log(
            $"Station visual style '{style}' isn't available for this asset; using the current sprite instead.",
            LogLevel.Trace
        );
        return source;
    }

    private static int NormalizeDirection(int direction)
    {
        int normalized = direction % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }
}
