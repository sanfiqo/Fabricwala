using SkiaSharp;
using TextileConnect.Application.Files;

namespace TextileConnect.Infrastructure.Files;

/// <summary>Makes the photo copies with SkiaSharp (MIT licence; native code for Windows and Linux is in the packages).</summary>
public sealed class SkiaImageVariantMaker : IImageVariantMaker
{
    public const int MainSize = 800, CardSize = 400, ThumbSize = 120, LqipSize = 20;
    private const int WebpQuality = 80;
    private const int LqipMaxBytes = 1000;

    public ImageVariants? Create(Stream original)
    {
        try
        {
            using var buffer = new MemoryStream();
            original.CopyTo(buffer);
            buffer.Position = 0;
            using var codec = SKCodec.Create(buffer);
            if (codec is null) return null;
            if ((long)codec.Info.Width * codec.Info.Height > IImageVariantMaker.MaxPixels) return null;
            using var decoded = SKBitmap.Decode(codec);
            if (decoded is null) return null;
            using var upright = Orient(decoded, codec.EncodedOrigin);

            return new ImageVariants(
                Webp(upright, MainSize), Webp(upright, CardSize), Webp(upright, ThumbSize), Lqip(upright),
                upright.Width, upright.Height);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null; // not an image Skia can read
        }
    }

    /// <summary>Returns the photo turned the way the camera meant it to be seen (EXIF orientation). Phone photos are often stored sideways with a note to rotate them.</summary>
    public static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        // x' = ScaleX*x + SkewX*y + TransX and y' = SkewY*x + ScaleY*y + TransY, for the eight orientations of the EXIF standard.
        var (scaleX, skewX, transX, skewY, scaleY, transY, swap) = origin switch
        {
            SKEncodedOrigin.TopRight => (-1f, 0f, w, 0f, 1f, 0f, false),
            SKEncodedOrigin.BottomRight => (-1f, 0f, w, 0f, -1f, h, false),
            SKEncodedOrigin.BottomLeft => (1f, 0f, 0f, 0f, -1f, h, false),
            SKEncodedOrigin.LeftTop => (0f, 1f, 0f, 1f, 0f, 0f, true),
            SKEncodedOrigin.RightTop => (0f, -1f, h, 1f, 0f, 0f, true),
            SKEncodedOrigin.RightBottom => (0f, -1f, h, -1f, 0f, w, true),
            SKEncodedOrigin.LeftBottom => (0f, 1f, 0f, -1f, 0f, w, true),
            _ => (1f, 0f, 0f, 0f, 1f, 0f, false)
        };
        var result = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(new SKMatrix { ScaleX = scaleX, SkewX = skewX, TransX = transX, SkewY = skewY, ScaleY = scaleY, TransY = transY, Persp0 = 0, Persp1 = 0, Persp2 = 1 });
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    /// <summary>The photo fitted inside a size x size square on white, centred, never cut and never enlarged.</summary>
    private static SKImage Square(SKBitmap upright, int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        var scale = Math.Min(1f, Math.Min((float)size / upright.Width, (float)size / upright.Height));
        float width = upright.Width * scale, height = upright.Height * scale;
        using var image = SKImage.FromBitmap(upright);
        canvas.DrawImage(image, SKRect.Create((size - width) / 2f, (size - height) / 2f, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return surface.Snapshot();
    }

    private static byte[] Webp(SKBitmap upright, int size)
    {
        using var image = Square(upright, size);
        using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality) ?? throw new InvalidOperationException("WebP encoding failed.");
        return data.ToArray();
    }

    private static string Lqip(SKBitmap upright)
    {
        using var image = Square(upright, LqipSize);
        byte[] bytes = [];
        foreach (var quality in new[] { 40, 25, 10 })
        {
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality) ?? throw new InvalidOperationException("JPEG encoding failed.");
            bytes = data.ToArray();
            if (bytes.Length <= LqipMaxBytes) break;
        }
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }
}
