namespace TextileConnect.Application.Files;

/// <summary>Square WebP copies of one photo and a tiny preview. Main is 800, Card 400, Thumb 120 pixels wide and high.</summary>
public sealed record ImageVariants(byte[] Main, byte[] Card, byte[] Thumb, string Lqip, int OriginalWidth, int OriginalHeight);

public interface IImageVariantMaker
{
    /// <summary>
    /// Makes the variants of a JPEG, PNG or WebP photo. The photo is turned upright using its camera orientation, fitted inside the square on a white background
    /// (never cut, never enlarged), and encoded as WebP. Lqip is a data URL of a 20 pixel wide JPEG, about 1 KB or less, for a blurred placeholder.
    /// Returns null when the bytes are not a readable image or are larger than <see cref="MaxPixels"/> pixels.
    /// </summary>
    ImageVariants? Create(Stream original);

    /// <summary>Largest photo that is processed (about 50 megapixels); anything bigger could use too much memory.</summary>
    const long MaxPixels = 50_000_000;
}
