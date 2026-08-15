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
        this.minecart = this.TryLoad("assets/minecart.png");
        this.tracks = this.TryLoad("assets/tracks.png");
        this.wallHole = this.TryLoad("assets/mine_entrance.png");

        // Real PNG variants stored in the repository. They preserve the exact same
        // atlas dimensions and source rectangles as the original artwork.
        foreach (string style in ModConfig.StationVisualStyles)
        {
            if (style == ModConfig.StationVisualLegacyCurrent)
                continue;

            this.TryAddVariant(
                this.minecartVariants,
                style,
                $"assets/styles/{style}/minecart.png"
            );
            this.TryAddVariant(
                this.trackVariants,
                style,
                $"assets/styles/{style}/tracks.png"
            );
            this.TryAddVariant(
                this.entranceVariants,
                style,
                $"assets/styles/{style}/mine_entrance.png"
            );
        }
    }

    private void TryAddVariant(
        IDictionary<string, Texture2D> target,
        string style,
        string relativePath)
    {
        Texture2D? texture = this.TryLoad(relativePath);
        if (texture is not null)
            target[style] = texture;
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

    private static int NormalizeDirection(int direction)
    {
        int normalized = direction % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }
}
