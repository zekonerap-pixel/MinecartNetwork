using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MinecartNetwork.Rendering;

public sealed class MinecartVisualAssets
{
    public const int PixelScale = 4;

    // Four directional frames in order: up, right, down, left.
    // The detailed entrance intentionally overhangs vertically while keeping
    // the station's logical construction footprint unchanged.
    public const int EntranceFrameWidth = 28;
    public const int EntranceFrameHeight = 28;
    public const int MinecartFrameWidth = 16;
    public const int MinecartFrameHeight = 16;

    // tracks.png contains vertical then horizontal 16x16 frames.
    public const int TrackFrameSize = 16;

    private readonly IModHelper helper;

    private Texture2D? minecart;
    private Texture2D? tracks;
    private Texture2D? wallHole;
    private bool loaded;

    public MinecartVisualAssets(IModHelper helper)
    {
        this.helper = helper;
    }

    public Texture2D? Minecart
    {
        get
        {
            this.EnsureLoaded();
            return this.minecart;
        }
    }

    public Texture2D? Tracks
    {
        get
        {
            this.EnsureLoaded();
            return this.tracks;
        }
    }

    public Texture2D? WallHole
    {
        get
        {
            this.EnsureLoaded();
            return this.wallHole;
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

    private static int NormalizeDirection(int direction)
    {
        int normalized = direction % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }
}
