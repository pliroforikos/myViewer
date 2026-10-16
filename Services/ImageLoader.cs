using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using myView.Models;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace myView.Services;

public static class ImageLoader
{
    private static readonly HashSet<string> WicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".bmp", ".dib",
        ".gif",
        ".ico",
        ".webp",
        ".tif", ".tiff"
    };

    private static readonly HashSet<string> VectorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg", ".svgz"
    };

    public static async Task<LoadedImageData> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => LoadImageInternal(filePath, cancellationToken), cancellationToken);
    }

    private static LoadedImageData LoadImageInternal(string filePath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var fi = new FileInfo(filePath);
        if (!fi.Exists)
            throw new FileNotFoundException($"File not found: {filePath}");

        string ext = fi.Extension.ToLowerInvariant();
        long fileSize = fi.Length;

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Vector Path (.svg, .svgz)
        if (VectorExtensions.Contains(ext))
        {
            try
            {
                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = true,
                    TextAsGeometry = false
                };
                var reader = new FileSvgReader(settings);
                var drawingGroup = reader.Read(filePath);
                if (drawingGroup != null)
                {
                    var drawingImage = new DrawingImage(drawingGroup);
                    drawingImage.Freeze();
                    sw.Stop();

                    int width = (int)Math.Ceiling(Math.Max(1, drawingGroup.Bounds.Width));
                    int height = (int)Math.Ceiling(Math.Max(1, drawingGroup.Bounds.Height));

                    return new LoadedImageData
                    {
                        ImageSource = drawingImage,
                        PixelWidth = width,
                        PixelHeight = height,
                        FormatName = "SVG Vector",
                        FileSizeBytes = fileSize,
                        LoadDuration = sw.Elapsed,
                        IsVector = true
                    };
                }
            }
            catch
            {
                // Fall back to MagickImage for complex/unusual SVGs
            }
        }

        // 2. Fast WIC Path (Standard raster images)
        if (WicExtensions.Contains(ext))
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    BitmapSource finalSource = frame;

                    // Inspect EXIF Orientation
                    int rotationDegrees = 0;
                    bool flipH = false;
                    bool flipV = false;

                    try
                    {
                        if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery("/app1/ifd/{ushort=274}"))
                        {
                            object? val = metadata.GetQuery("/app1/ifd/{ushort=274}");
                            if (val is ushort orient)
                            {
                                switch (orient)
                                {
                                    case 2: flipH = true; break;
                                    case 3: rotationDegrees = 180; break;
                                    case 4: flipV = true; break;
                                    case 5: rotationDegrees = 90; flipH = true; break;
                                    case 6: rotationDegrees = 90; break;
                                    case 7: rotationDegrees = 270; flipH = true; break;
                                    case 8: rotationDegrees = 270; break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore metadata read errors and display original frame
                    }

                    if (rotationDegrees != 0 || flipH || flipV)
                    {
                        var transformGroup = new TransformGroup();
                        if (rotationDegrees != 0)
                            transformGroup.Children.Add(new RotateTransform(rotationDegrees));
                        if (flipH)
                            transformGroup.Children.Add(new ScaleTransform(-1, 1));
                        if (flipV)
                            transformGroup.Children.Add(new ScaleTransform(1, -1));

                        var transformed = new TransformedBitmap(frame, transformGroup);
                        transformed.Freeze();
                        finalSource = transformed;
                    }
                    else
                    {
                        finalSource.Freeze();
                    }

                    sw.Stop();

                    string format = ext.TrimStart('.').ToUpperInvariant();
                    return new LoadedImageData
                    {
                        ImageSource = finalSource,
                        PixelWidth = finalSource.PixelWidth,
                        PixelHeight = finalSource.PixelHeight,
                        FormatName = format,
                        FileSizeBytes = fileSize,
                        LoadDuration = sw.Elapsed,
                        IsVector = false
                    };
                }
            }
            catch
            {
                // Fall back to MagickImage if WIC encounters an error
            }
        }

        // 3. Magick.NET Path (RAW camera formats, PSD, PSB, and fallback)
        cancellationToken.ThrowIfCancellationRequested();

        using (var magick = new MagickImage(filePath))
        {
            magick.ColorSpace = ColorSpace.sRGB;
            magick.AutoOrient();

            int width = (int)magick.Width;
            int height = (int)magick.Height;

            byte[] bgraPixels = magick.ToByteArray(MagickFormat.Bgra);
            var bitmap = BitmapSource.Create(
                width,
                height,
                magick.Density.X > 0 ? magick.Density.X : 96,
                magick.Density.Y > 0 ? magick.Density.Y : 96,
                PixelFormats.Bgra32,
                null,
                bgraPixels,
                width * 4);

            bitmap.Freeze();
            sw.Stop();

            string format = magick.Format.ToString();
            return new LoadedImageData
            {
                ImageSource = bitmap,
                PixelWidth = width,
                PixelHeight = height,
                FormatName = format,
                FileSizeBytes = fileSize,
                LoadDuration = sw.Elapsed,
                IsVector = false
            };
        }
    }
}
