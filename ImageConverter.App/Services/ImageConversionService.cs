using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Svg.Skia;
using ImageFormat = ImageConverter.Models.ImageFormat;
using ImageInfo = ImageConverter.Models.ImageInfo;
using ResizeMode = ImageConverter.Models.ResizeMode;
using ConversionOptions = ImageConverter.Models.ConversionOptions;
using ConversionResult = ImageConverter.Models.ConversionResult;
using static ImageConverter.Models.ImageFormatExtensions;

namespace ImageConverter.Services;

/// <summary>
/// Service for converting images between formats with quality and size control.
/// </summary>
public class ImageConversionService
{
    private static readonly string[] SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".svg"
    ];

    /// <summary>
    /// Check if a file extension is supported for conversion.
    /// </summary>
    public static bool IsSupportedFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    /// <summary>
    /// Check if a file is an SVG file.
    /// </summary>
    public static bool IsSvgFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".svg";
    }

    /// <summary>
    /// Get information about an image file.
    /// </summary>
    public async Task<ImageInfo> GetImageInfoAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        // Handle SVG files specially
        if (IsSvgFile(filePath))
        {
            return await GetSvgImageInfoAsync(filePath, fileInfo);
        }

        using var image = await Image.LoadAsync(filePath);

        var hasTransparency = image.Metadata.DecodedImageFormat?.Name switch
        {
            "PNG" => true,
            "GIF" => true,
            "WEBP" => true,
            _ => false
        };

        return new ImageInfo
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension.ToLowerInvariant(),
            Directory = fileInfo.DirectoryName ?? "",
            FileSizeBytes = fileInfo.Length,
            Width = image.Width,
            Height = image.Height,
            DetectedFormat = image.Metadata.DecodedImageFormat?.Name ?? "Unknown",
            HasTransparency = hasTransparency
        };
    }

    /// <summary>
    /// Get information about an SVG file.
    /// </summary>
    private async Task<ImageInfo> GetSvgImageInfoAsync(string filePath, FileInfo fileInfo)
    {
        return await Task.Run(() =>
        {
            using var svg = new SKSvg();
            svg.Load(filePath);
            
            var picture = svg.Picture;
            var bounds = picture?.CullRect ?? SKRect.Empty;

            return new ImageInfo
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                Extension = fileInfo.Extension.ToLowerInvariant(),
                Directory = fileInfo.DirectoryName ?? "",
                FileSizeBytes = fileInfo.Length,
                Width = (int)bounds.Width,
                Height = (int)bounds.Height,
                DetectedFormat = "SVG",
                HasTransparency = true  // SVG typically supports transparency
            };
        });
    }

    /// <summary>
    /// Convert an image with the specified options.
    /// </summary>
    public async Task<ConversionResult> ConvertAsync(string sourcePath, ConversionOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
            {
                return ConversionResult.Failed($"Source file not found: {sourcePath}");
            }

            // Check if trying to convert TO SVG (not supported)
            if (options.TargetFormat == ImageFormat.Svg)
            {
                return ConversionResult.Failed("Cannot convert to SVG format. SVG is a vector format and cannot be created from raster images.");
            }

            // Handle SVG source files specially
            if (IsSvgFile(sourcePath))
            {
                return await ConvertSvgAsync(sourcePath, options, stopwatch);
            }

            // Load the image
            using var image = await Image.LoadAsync(sourcePath);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Apply resize if needed
            ApplyResize(image, options);

            // Determine output path
            var outputPath = GetOutputPath(sourcePath, options);

            // Check if we should overwrite
            if (File.Exists(outputPath) && !options.OverwriteExisting)
            {
                outputPath = GetUniqueFilePath(outputPath);
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Get encoder for target format
            var encoder = GetEncoder(options);

            // If target size is specified, use iterative quality adjustment
            if (options.TargetSizeKb > 0 && options.TargetFormat.SupportsQuality())
            {
                await SaveWithTargetSizeAsync(image, outputPath, options);
            }
            else
            {
                await image.SaveAsync(outputPath, encoder);
            }

            stopwatch.Stop();

            var outputInfo = new FileInfo(outputPath);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                OriginalSizeBytes = sourceInfo.Length,
                NewSizeBytes = outputInfo.Length,
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight,
                NewWidth = image.Width,
                NewHeight = image.Height,
                ConversionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ConversionResult.Failed($"Conversion failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert an SVG file to a raster format.
    /// </summary>
    private async Task<ConversionResult> ConvertSvgAsync(string sourcePath, ConversionOptions options, Stopwatch stopwatch)
    {
        return await Task.Run(async () =>
        {
            var sourceInfo = new FileInfo(sourcePath);
            
            using var svg = new SKSvg();
            svg.Load(sourcePath);
            
            var picture = svg.Picture;
            if (picture == null)
            {
                return ConversionResult.Failed("Failed to parse SVG file.");
            }

            var bounds = picture.CullRect;
            var originalWidth = (int)bounds.Width;
            var originalHeight = (int)bounds.Height;
            
            // Calculate target dimensions based on resize options
            int targetWidth = originalWidth;
            int targetHeight = originalHeight;
            
            if (options.ResizeMode != ResizeMode.None)
            {
                switch (options.ResizeMode)
                {
                    case ResizeMode.ExactSize:
                        targetWidth = options.TargetWidth > 0 ? options.TargetWidth : originalWidth;
                        targetHeight = options.TargetHeight > 0 ? options.TargetHeight : originalHeight;
                        break;
                    case ResizeMode.MaxWidth when options.TargetWidth > 0:
                        targetWidth = options.TargetWidth;
                        targetHeight = options.MaintainAspectRatio
                            ? (int)(originalHeight * ((double)options.TargetWidth / originalWidth))
                            : originalHeight;
                        break;
                    case ResizeMode.MaxHeight when options.TargetHeight > 0:
                        targetHeight = options.TargetHeight;
                        targetWidth = options.MaintainAspectRatio
                            ? (int)(originalWidth * ((double)options.TargetHeight / originalHeight))
                            : originalWidth;
                        break;
                    case ResizeMode.Percentage when options.TargetWidth > 0:
                        var scale = options.TargetWidth / 100.0;
                        targetWidth = (int)(originalWidth * scale);
                        targetHeight = (int)(originalHeight * scale);
                        break;
                }
            }

            // Ensure minimum dimensions
            targetWidth = Math.Max(1, targetWidth);
            targetHeight = Math.Max(1, targetHeight);

            // Create bitmap and render SVG
            using var bitmap = new SKBitmap(targetWidth, targetHeight);
            using var canvas = new SKCanvas(bitmap);
            
            // Fill with white background for formats that don't support transparency
            if (!options.TargetFormat.SupportsTransparency())
            {
                canvas.Clear(SKColors.White);
            }
            else
            {
                canvas.Clear(SKColors.Transparent);
            }
            
            // Scale to fit
            var scaleX = targetWidth / bounds.Width;
            var scaleY = targetHeight / bounds.Height;
            canvas.Scale(scaleX, scaleY);
            canvas.Translate(-bounds.Left, -bounds.Top);
            
            canvas.DrawPicture(picture);

            // Determine output path
            var outputPath = GetOutputPath(sourcePath, options);

            // Check if we should overwrite
            if (File.Exists(outputPath) && !options.OverwriteExisting)
            {
                outputPath = GetUniqueFilePath(outputPath);
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Convert SKBitmap to ImageSharp Image for encoding
            using var pixmap = bitmap.PeekPixels();
            var imageInfo = pixmap.Info;
            var data = pixmap.GetPixels();
            
            // Create ImageSharp image from raw pixel data
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(
                pixmap.GetPixelSpan(), 
                imageInfo.Width, 
                imageInfo.Height);

            // Get encoder for target format
            var encoder = GetEncoder(options);

            // Save the image
            await image.SaveAsync(outputPath, encoder);

            stopwatch.Stop();
            var outputInfo = new FileInfo(outputPath);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                OriginalSizeBytes = sourceInfo.Length,
                NewSizeBytes = outputInfo.Length,
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight,
                NewWidth = targetWidth,
                NewHeight = targetHeight,
                ConversionTime = stopwatch.Elapsed
            };
        });
    }

    /// <summary>
    /// Apply resize transformations based on options.
    /// </summary>
    private static void ApplyResize(Image image, ConversionOptions options)
    {
        if (options.ResizeMode == ResizeMode.None)
            return;

        int newWidth = image.Width;
        int newHeight = image.Height;

        switch (options.ResizeMode)
        {
            case ResizeMode.ExactSize:
                newWidth = options.TargetWidth > 0 ? options.TargetWidth : image.Width;
                newHeight = options.TargetHeight > 0 ? options.TargetHeight : image.Height;
                break;

            case ResizeMode.MaxWidth when options.TargetWidth > 0 && image.Width > options.TargetWidth:
                newWidth = options.TargetWidth;
                newHeight = options.MaintainAspectRatio
                    ? (int)(image.Height * ((double)options.TargetWidth / image.Width))
                    : image.Height;
                break;

            case ResizeMode.MaxHeight when options.TargetHeight > 0 && image.Height > options.TargetHeight:
                newHeight = options.TargetHeight;
                newWidth = options.MaintainAspectRatio
                    ? (int)(image.Width * ((double)options.TargetHeight / image.Height))
                    : image.Width;
                break;

            case ResizeMode.Percentage when options.TargetWidth > 0:
                var scale = options.TargetWidth / 100.0;
                newWidth = (int)(image.Width * scale);
                newHeight = (int)(image.Height * scale);
                break;
        }

        if (newWidth != image.Width || newHeight != image.Height)
        {
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }
    }

    /// <summary>
    /// Save image with iterative quality adjustment to meet target size.
    /// </summary>
    private static async Task SaveWithTargetSizeAsync(Image image, string outputPath, ConversionOptions options)
    {
        var targetBytes = options.TargetSizeKb * 1024;
        var quality = options.Quality;
        var minQuality = 5;
        var maxQuality = 100;

        // Binary search for optimal quality
        for (int i = 0; i < 10 && minQuality < maxQuality; i++)
        {
            using var memoryStream = new MemoryStream();
            var testOptions = options with { Quality = quality };
            var encoder = GetEncoder(testOptions);

            await image.SaveAsync(memoryStream, encoder);

            if (memoryStream.Length <= targetBytes)
            {
                // File is small enough, try higher quality
                minQuality = quality + 1;
            }
            else
            {
                // File is too large, try lower quality
                maxQuality = quality - 1;
            }

            quality = (minQuality + maxQuality) / 2;
        }

        // Save with final quality
        var finalEncoder = GetEncoder(options with { Quality = Math.Max(minQuality - 1, 5) });
        await image.SaveAsync(outputPath, finalEncoder);
    }

    /// <summary>
    /// Get the appropriate encoder for the target format.
    /// </summary>
    private static IImageEncoder GetEncoder(ConversionOptions options)
    {
        return options.TargetFormat switch
        {
            ImageFormat.Jpeg => new JpegEncoder { Quality = options.Quality },
            ImageFormat.Png => new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression },
            ImageFormat.WebP => new WebpEncoder { Quality = options.Quality },
            ImageFormat.Gif => new GifEncoder(),
            ImageFormat.Bmp => new BmpEncoder(),
            ImageFormat.Tiff => new TiffEncoder(),
            ImageFormat.Ico => new PngEncoder(), // ICO is complex, save as PNG
            _ => new PngEncoder()
        };
    }

    /// <summary>
    /// Get the output path based on options or source path.
    /// </summary>
    private static string GetOutputPath(string sourcePath, ConversionOptions options)
    {
        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            return options.OutputPath;
        }

        var directory = Path.GetDirectoryName(sourcePath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var newExtension = options.TargetFormat.GetFileExtension();

        return Path.Combine(directory, $"{nameWithoutExt}{newExtension}");
    }

    /// <summary>
    /// Get a unique file path by appending a number if file exists.
    /// </summary>
    private static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        int counter = 1;
        string newPath;

        do
        {
            newPath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
            counter++;
        } while (File.Exists(newPath) && counter < 1000);

        return newPath;
    }
}
