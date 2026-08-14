using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MinecartNetwork.Rendering;

public sealed class MinecartVisualAssets
{
    // Runtime atlases are built from the original PolyCarts spring tilesheet.
    // Four directional frames are stored in order: up, right, down, left.
    public const int EntranceFrameWidth = 48;
    public const int EntranceFrameHeight = 48;
    public const int MinecartFrameWidth = 32;
    public const int MinecartFrameHeight = 32;

    // Runtime track atlas contains vertical then horizontal 16x16 frames.
    public const int TrackFrameSize = 16;

    private const string PolyCartsSheetPath = "assets/polycarts_spring.png";

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
        this.minecart?.Dispose();
        this.tracks?.Dispose();
        this.wallHole?.Dispose();
        this.minecart = null;
        this.tracks = null;
        this.wallHole = null;
    }

    private void EnsureLoaded()
    {
        if (this.loaded)
            return;

        this.loaded = true;

        Texture2D? sourceSheet = this.TryLoadPolyCartsSheet();
        if (sourceSheet is null)
            return;

        this.BuildRuntimeAtlases(sourceSheet);
    }

    private Texture2D? TryLoadPolyCartsSheet()
    {
        try
        {
            return this.helper.ModContent.Load<Texture2D>(PolyCartsSheetPath);
        }
        catch
        {
            return null;
        }
    }

    private void BuildRuntimeAtlases(Texture2D sourceSheet)
    {
        Color[] sourcePixels = new Color[sourceSheet.Width * sourceSheet.Height];
        sourceSheet.GetData(sourcePixels);
        GraphicsDevice graphics = sourceSheet.GraphicsDevice;

        // PolyCarts stores its front/back carts as 16x32 sprites and its side cart as
        // a 32x32 sprite. Keep every source pixel intact and center the narrower views
        // in a transparent 32x32 directional frame.
        Color[] cartPixels = CreateTransparentPixels(MinecartFrameWidth * 4, MinecartFrameHeight);
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(0, 0, 16, 32),
            cartPixels,
            MinecartFrameWidth * 4,
            8,
            0
        );
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(16, 32, 32, 32),
            cartPixels,
            MinecartFrameWidth * 4,
            MinecartFrameWidth,
            0
        );
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(0, 32, 16, 32),
            cartPixels,
            MinecartFrameWidth * 4,
            MinecartFrameWidth * 2 + 8,
            0
        );
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(16, 32, 32, 32),
            cartPixels,
            MinecartFrameWidth * 4,
            MinecartFrameWidth * 3,
            0,
            flipX: true
        );

        this.minecart = new Texture2D(graphics, MinecartFrameWidth * 4, MinecartFrameHeight);
        this.minecart.SetData(cartPixels);

        // Exact vertical/horizontal track tiles from PolyCarts.
        Color[] trackPixels = CreateTransparentPixels(TrackFrameSize * 2, TrackFrameSize);
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(16, 16, 16, 16),
            trackPixels,
            TrackFrameSize * 2,
            0,
            0
        );
        Blit(
            sourcePixels,
            sourceSheet.Width,
            new Rectangle(16, 0, 16, 16),
            trackPixels,
            TrackFrameSize * 2,
            TrackFrameSize,
            0
        );

        this.tracks = new Texture2D(graphics, TrackFrameSize * 2, TrackFrameSize);
        this.tracks.SetData(trackPixels);

        // PolyCarts' timber frame + hanging lamp occupies 48x48 pixels. Its original
        // orientation faces down; the remaining station directions are exact quarter-turns.
        Rectangle entranceSource = new(80, 16, 48, 48);
        Color[] entrancePixels = CreateTransparentPixels(EntranceFrameWidth * 4, EntranceFrameHeight);
        CopyRotatedEntrance(sourcePixels, sourceSheet.Width, entranceSource, entrancePixels, 0, 2); // up
        CopyRotatedEntrance(sourcePixels, sourceSheet.Width, entranceSource, entrancePixels, 1, 3); // right
        CopyRotatedEntrance(sourcePixels, sourceSheet.Width, entranceSource, entrancePixels, 2, 0); // down
        CopyRotatedEntrance(sourcePixels, sourceSheet.Width, entranceSource, entrancePixels, 3, 1); // left

        this.wallHole = new Texture2D(graphics, EntranceFrameWidth * 4, EntranceFrameHeight);
        this.wallHole.SetData(entrancePixels);
    }

    private static void CopyRotatedEntrance(
        Color[] sourcePixels,
        int sourceWidth,
        Rectangle source,
        Color[] destinationPixels,
        int frame,
        int clockwiseQuarterTurns)
    {
        int atlasWidth = EntranceFrameWidth * 4;
        int frameX = frame * EntranceFrameWidth;
        int turns = ((clockwiseQuarterTurns % 4) + 4) % 4;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = sourcePixels[(source.Y + y) * sourceWidth + source.X + x];
                int targetX;
                int targetY;

                switch (turns)
                {
                    case 1:
                        targetX = source.Height - 1 - y;
                        targetY = x;
                        break;
                    case 2:
                        targetX = source.Width - 1 - x;
                        targetY = source.Height - 1 - y;
                        break;
                    case 3:
                        targetX = y;
                        targetY = source.Width - 1 - x;
                        break;
                    default:
                        targetX = x;
                        targetY = y;
                        break;
                }

                destinationPixels[targetY * atlasWidth + frameX + targetX] = pixel;
            }
        }
    }

    private static void Blit(
        Color[] sourcePixels,
        int sourceWidth,
        Rectangle source,
        Color[] destinationPixels,
        int destinationWidth,
        int destinationX,
        int destinationY,
        bool flipX = false)
    {
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                int sourceX = flipX ? source.Width - 1 - x : x;
                Color pixel = sourcePixels[(source.Y + y) * sourceWidth + source.X + sourceX];
                destinationPixels[(destinationY + y) * destinationWidth + destinationX + x] = pixel;
            }
        }
    }

    private static Color[] CreateTransparentPixels(int width, int height)
    {
        var pixels = new Color[width * height];
        Array.Fill(pixels, Color.Transparent);
        return pixels;
    }

    private static int NormalizeDirection(int direction)
    {
        int normalized = direction % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }
}
