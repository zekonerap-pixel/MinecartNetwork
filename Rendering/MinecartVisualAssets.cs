using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MinecartNetwork.Rendering;

public sealed class MinecartVisualAssets
{
    // Stardew renders 16 source pixels as one 64 px world tile at 4x scale.
    public const int TileSourceSize = 16;
    public const int PixelScale = 4;

    // The tunnel artwork may be larger than one source tile because it is allowed
    // to overhang visually while keeping a one-tile logical footprint.
    public const int EntranceSourceWidth = 20;
    public const int EntranceSourceHeight = 22;

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
        this.wallHole = this.TryLoad("assets/wall_hole.png");
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
}
