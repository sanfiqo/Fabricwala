using SkiaSharp;
using TextileConnect.Infrastructure.Files;

namespace TextileConnect.Tests;

public sealed class ImageVariantMakerTests
{
    private static readonly SkiaImageVariantMaker Maker = new();

    /// <summary>A real PNG of the given size filled with one colour, made with Skia.</summary>
    internal static byte[] Png(int width, int height, SKColor colour, Action<SKCanvas>? draw = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(colour);
        draw?.Invoke(surface.Canvas);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A real JPEG, PNG or WebP image of one colour.</summary>
    internal static byte[] Image(SKEncodedImageFormat format, int width, int height, SKColor colour)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(colour);
        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static SKBitmap Decode(byte[] bytes) => SKBitmap.Decode(bytes)!;

    private static void AssertColour(SKColor actual, SKColor expected, int tolerance = 40)
    {
        Assert.InRange(actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange(actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange(actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
    }

    [Fact]
    public void A_photo_becomes_three_square_webp_copies_and_a_tiny_preview()
    {
        var variants = Maker.Create(new MemoryStream(Png(1200, 800, SKColors.SteelBlue, c => c.DrawCircle(600, 400, 200, new SKPaint { Color = SKColors.OrangeRed }))));

        Assert.NotNull(variants);
        Assert.Equal((1200, 800), (variants.OriginalWidth, variants.OriginalHeight));
        foreach (var (bytes, size) in new[] { (variants.Main, 800), (variants.Card, 400), (variants.Thumb, 120) })
        {
            using var codec = SKCodec.Create(new MemoryStream(bytes));
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.Equal((size, size), (codec.Info.Width, codec.Info.Height));
        }
        Assert.True(variants.Thumb.Length < variants.Card.Length && variants.Card.Length < variants.Main.Length);

        Assert.StartsWith("data:image/jpeg;base64,", variants.Lqip);
        var preview = Convert.FromBase64String(variants.Lqip["data:image/jpeg;base64,".Length..]);
        Assert.True(preview.Length <= 1000, $"the preview is {preview.Length} bytes");
        using var previewImage = Decode(preview);
        Assert.Equal((20, 20), (previewImage.Width, previewImage.Height));
    }

    [Fact]
    public void A_wide_photo_is_fitted_inside_the_square_on_white_and_never_cut()
    {
        var variants = Maker.Create(new MemoryStream(Png(1200, 600, SKColors.Red)))!;
        using var main = Decode(variants.Main);
        // 1200 x 600 fitted into 800 gives 800 x 400: white bands of 200 pixels above and below, the full width in the middle.
        AssertColour(main.GetPixel(400, 5), SKColors.White);
        AssertColour(main.GetPixel(400, 795), SKColors.White);
        AssertColour(main.GetPixel(5, 400), SKColors.Red);
        AssertColour(main.GetPixel(795, 400), SKColors.Red);
        AssertColour(main.GetPixel(400, 400), SKColors.Red);
    }

    [Fact]
    public void A_small_photo_is_centred_and_not_enlarged()
    {
        var variants = Maker.Create(new MemoryStream(Png(100, 50, SKColors.Blue)))!;
        using var main = Decode(variants.Main);
        AssertColour(main.GetPixel(400, 400), SKColors.Blue);   // the 100 x 50 photo sits in the middle of the 800 square
        AssertColour(main.GetPixel(300, 400), SKColors.White);  // and is not stretched to the edges
        AssertColour(main.GetPixel(10, 10), SKColors.White);
        using var card = Decode(variants.Card);
        AssertColour(card.GetPixel(200, 200), SKColors.Blue);
        AssertColour(card.GetPixel(100, 200), SKColors.White);
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft, 2, 1, "RB")]
    [InlineData(SKEncodedOrigin.TopRight, 2, 1, "BR")]       // mirrored left to right
    [InlineData(SKEncodedOrigin.BottomRight, 2, 1, "BR")]    // turned half way round
    [InlineData(SKEncodedOrigin.BottomLeft, 2, 1, "RB")]     // mirrored top to bottom (one row, so unchanged)
    [InlineData(SKEncodedOrigin.RightTop, 1, 2, "RB")]       // turned a quarter clockwise: left side goes to the top
    [InlineData(SKEncodedOrigin.LeftBottom, 1, 2, "BR")]     // turned a quarter the other way: left side goes to the bottom
    [InlineData(SKEncodedOrigin.LeftTop, 1, 2, "RB")]
    [InlineData(SKEncodedOrigin.RightBottom, 1, 2, "BR")]
    public void The_camera_orientation_is_applied(SKEncodedOrigin origin, int width, int height, string order)
    {
        // A photo two pixels wide: red on the left, blue on the right.
        using var source = new SKBitmap(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, SKColors.Red);
        source.SetPixel(1, 0, SKColors.Blue);
        using var result = SkiaImageVariantMaker.Orient(source, origin);

        Assert.Equal((width, height), (result.Width, result.Height));
        var pixels = width == 2 ? new[] { result.GetPixel(0, 0), result.GetPixel(1, 0) } : new[] { result.GetPixel(0, 0), result.GetPixel(0, 1) };
        for (var i = 0; i < 2; i++)
            AssertColour(pixels[i], order[i] == 'R' ? SKColors.Red : SKColors.Blue, 20);
    }

    [Fact]
    public void Bytes_that_are_not_a_readable_image_give_nothing()
    {
        Assert.Null(Maker.Create(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not an image"))));
        Assert.Null(Maker.Create(new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]))); // the start of a PNG and then junk
        Assert.Null(Maker.Create(new MemoryStream([])));
    }

    [Fact]
    public void A_photo_that_claims_to_be_far_too_large_is_refused_before_it_is_decoded()
    {
        // A real PNG header for 20,000 x 20,000 pixels (400 megapixels). No pixel data follows, which is fine: the size is read first.
        static byte[] Chunk(string type, byte[] data)
        {
            var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            var crc = ~body.Aggregate(0xFFFFFFFFu, (current, b) => { current ^= b; for (var k = 0; k < 8; k++) current = (current & 1) != 0 ? 0xEDB88320u ^ (current >> 1) : current >> 1; return current; });
            return [.. System.Net.IPAddress.HostToNetworkOrder(data.Length).ToBytesBigEndian(), .. body, .. ((int)crc).ToBytesBigEndian()];
        }
        var header = new byte[] { 0, 0, 0x4E, 0x20, 0, 0, 0x4E, 0x20, 8, 2, 0, 0, 0 };
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(Chunk("IHDR", header)).ToArray();
        Assert.Null(Maker.Create(new MemoryStream(png)));
    }
}

internal static class BigEndianExtensions
{
    public static byte[] ToBytesBigEndian(this int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
