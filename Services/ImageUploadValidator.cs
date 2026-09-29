namespace LaundryHub2._0.Services;

public static class ImageUploadValidator
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public record ValidationResult(bool IsValid, string? ErrorMessage, string? ValidatedExtension);

    /// <summary>
    /// Validates file size (max 10MB), client extension, client MIME type,
    /// and verifies actual magic bytes/signatures for JPEG, PNG, and WebP.
    /// </summary>
    public static async Task<ValidationResult> ValidateImageAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return new ValidationResult(false, "Please upload a valid image file.", null);
        }

        if (file.Length > MaxPhotoBytes)
        {
            return new ValidationResult(false, "The image is too large. Maximum size is 10 MB.", null);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return new ValidationResult(false, "The uploaded file is not a valid JPEG, PNG, or WebP image.", null);
        }

        if (!string.IsNullOrEmpty(file.ContentType) && !AllowedMimeTypes.Contains(file.ContentType))
        {
            return new ValidationResult(false, "The uploaded file is not a valid JPEG, PNG, or WebP image.", null);
        }

        // Inspect header bytes (up to first 16 bytes)
        byte[] header = new byte[16];
        int bytesRead;
        using (var stream = file.OpenReadStream())
        {
            bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        }

        if (bytesRead < 12)
        {
            return new ValidationResult(false, "The uploaded file is malformed or not a valid image.", null);
        }

        // Magic byte checks:
        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new ValidationResult(true, null, ".jpg");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytesRead >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return new ValidationResult(true, null, ".png");
        }

        // WebP: RIFF (bytes 0..3: 52 49 46 46) ... WEBP (bytes 8..11: 57 45 42 50)
        if (bytesRead >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return new ValidationResult(true, null, ".webp");
        }

        return new ValidationResult(false, "The uploaded file is not a valid JPEG, PNG, or WebP image.", null);
    }
}
