namespace ImageConverter.Models;

/// <summary>
/// Represents supported image formats for conversion.
/// </summary>
public enum ImageFormat
{
    Jpeg,
    Png,
    WebP,
    Gif,
    Bmp,
    Tiff,
    Ico,
    Svg  // Note: Can only convert FROM SVG, not TO SVG
}

/// <summary>
/// Extension methods and utilities for ImageFormat.
/// </summary>
public static class ImageFormatExtensions
{
    public static string GetDisplayName(this ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "JPEG (.jpg)",
        ImageFormat.Png => "PNG (.png)",
        ImageFormat.WebP => "WebP (.webp)",
        ImageFormat.Gif => "GIF (.gif)",
        ImageFormat.Bmp => "Bitmap (.bmp)",
        ImageFormat.Tiff => "TIFF (.tiff)",
        ImageFormat.Ico => "Icon (.ico)",
        ImageFormat.Svg => "SVG (.svg)",
        _ => format.ToString()
    };

    public static string GetFileExtension(this ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Png => ".png",
        ImageFormat.WebP => ".webp",
        ImageFormat.Gif => ".gif",
        ImageFormat.Bmp => ".bmp",
        ImageFormat.Tiff => ".tiff",
        ImageFormat.Ico => ".ico",
        ImageFormat.Svg => ".svg",
        _ => ".png"
    };

    public static string GetMimeType(this ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        ImageFormat.WebP => "image/webp",
        ImageFormat.Gif => "image/gif",
        ImageFormat.Bmp => "image/bmp",
        ImageFormat.Tiff => "image/tiff",
        ImageFormat.Ico => "image/x-icon",
        ImageFormat.Svg => "image/svg+xml",
        _ => "image/png"
    };

    public static bool SupportsQuality(this ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => true,
        ImageFormat.WebP => true,
        _ => false
    };

    public static bool SupportsTransparency(this ImageFormat format) => format switch
    {
        ImageFormat.Png => true,
        ImageFormat.WebP => true,
        ImageFormat.Gif => true,
        ImageFormat.Ico => true,
        ImageFormat.Svg => true,
        _ => false
    };

    /// <summary>
    /// Check if the format can be used as a conversion target.
    /// SVG cannot be a target since it's vector-based.
    /// </summary>
    public static bool CanBeConversionTarget(this ImageFormat format) => format switch
    {
        ImageFormat.Svg => false,  // Cannot convert TO SVG (vector format)
        _ => true
    };

    /// <summary>
    /// Get all formats that can be used as conversion targets (excludes SVG).
    /// </summary>
    public static IEnumerable<ImageFormat> GetConvertibleTargetFormats()
    {
        return Enum.GetValues<ImageFormat>().Where(f => f.CanBeConversionTarget());
    }
}
