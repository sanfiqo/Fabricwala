namespace TextileConnect.Api;

/// <summary>
/// What an uploaded file really is, read from its first bytes. The file name and the content type that a browser sends are chosen by the sender,
/// so they prove nothing: a script renamed to .jpg would otherwise be stored as a photo.
/// </summary>
public static class UploadRules
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxDocumentBytes = 10 * 1024 * 1024;
    public const int HeadLength = 12;

    /// <summary>JPEG, PNG or WebP from the first bytes, or null.</summary>
    public static (string? ContentType, string Extension) IdentifyImage(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ("image/png", ".png");
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("image/webp", ".webp");
        return (null, string.Empty);
    }

    /// <summary>A scanned or photographed business document: PDF, JPEG or PNG. Null for anything else.</summary>
    public static (string? ContentType, string Extension) IdentifyDocument(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-') return ("application/pdf", ".pdf");
        var image = IdentifyImage(b);
        return image.ContentType is "image/jpeg" or "image/png" ? image : (null, string.Empty);
    }

    /// <summary>The first bytes of the file (fewer when the file is shorter).</summary>
    public static async Task<byte[]> ReadHeadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var head = new byte[HeadLength];
        var read = 0;
        await using var stream = file.OpenReadStream();
        while (read < head.Length)
        {
            var count = await stream.ReadAsync(head.AsMemory(read), cancellationToken);
            if (count == 0) break;
            read += count;
        }
        return head[..read];
    }
}
