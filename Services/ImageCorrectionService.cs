using System.IO;
using System.Windows;
using ImageMagick;

namespace myView.Services;

public static class ImageCorrectionService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".webp",
        ".gif",
        ".bmp",
        ".tif", ".tiff"
    };

    public static bool CanCorrect(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext) ||
               ext.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Performs 4-corner perspective unwarping (deskew) on a photographed document.
    /// Maps quadrilateral (p0: TL, p1: TR, p2: BR, p3: BL) to a flat rectangle.
    /// </summary>
    public static async Task DeskewPerspectiveAndSaveAsync(
        string sourcePath,
        string destPath,
        Point p0, Point p1, Point p2, Point p3,
        int rotation = 0,
        bool flipH = false,
        bool autoEnhance = false)
    {
        await Task.Run(() =>
        {
            using var image = new MagickImage(sourcePath);

            try
            {
                image.AutoOrient();
            }
            catch { }

            if (flipH) image.Flop();
            if (rotation != 0) image.Rotate(rotation);

            // Compute target rectangular dimensions based on quad edge lengths
            double wTop = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            double wBottom = Math.Sqrt(Math.Pow(p2.X - p3.X, 2) + Math.Pow(p2.Y - p3.Y, 2));
            int outWidth = Math.Max(20, (int)Math.Round(Math.Max(wTop, wBottom)));

            double hLeft = Math.Sqrt(Math.Pow(p3.X - p0.X, 2) + Math.Pow(p3.Y - p0.Y, 2));
            double hRight = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            int outHeight = Math.Max(20, (int)Math.Round(Math.Max(hLeft, hRight)));

            // 8 coordinate pairs: source quad -> destination rectangle
            double[] distortArgs = new double[]
            {
                p0.X, p0.Y,  0, 0,
                p1.X, p1.Y,  outWidth, 0,
                p2.X, p2.Y,  outWidth, outHeight,
                p3.X, p3.Y,  0, outHeight
            };

            image.SetArtifact("distort:viewport", $"{outWidth}x{outHeight}+0+0");
            image.Distort(DistortMethod.Perspective, distortArgs);
            image.ResetPage();

            if (autoEnhance)
            {
                // Subtle document enhancement for scanned text readability
                image.AutoLevel();
                image.Sharpen(0, 0.5);
            }

            SaveImageSafely(image, sourcePath, destPath);
        });
    }

    /// <summary>
    /// Rotates the image by an arbitrary angle to straighten the horizon.
    /// If autoCrop is true, crops the maximum inscribed rectangle with identical aspect ratio.
    /// </summary>
    public static async Task StraightenAndSaveAsync(
        string sourcePath,
        string destPath,
        double angleDegrees,
        bool autoCrop = true,
        int rotation = 0,
        bool flipH = false)
    {
        await Task.Run(() =>
        {
            using var image = new MagickImage(sourcePath);

            try
            {
                image.AutoOrient();
            }
            catch { }

            if (flipH) image.Flop();
            if (rotation != 0) image.Rotate(rotation);

            double origW = image.Width;
            double origH = image.Height;

            string destExt = Path.GetExtension(destPath).ToLowerInvariant();
            if (destExt == ".png" || destExt == ".webp")
            {
                image.BackgroundColor = MagickColors.Transparent;
            }
            else
            {
                image.BackgroundColor = MagickColors.White;
            }

            if (Math.Abs(angleDegrees) > 0.001)
            {
                image.Rotate(angleDegrees);
                image.ResetPage();

                if (autoCrop)
                {
                    CropMaxInscribed(image, origW, origH, angleDegrees);
                }
            }

            SaveImageSafely(image, sourcePath, destPath);
        });
    }

    private static void CropMaxInscribed(MagickImage image, double origW, double origH, double angleDegrees)
    {
        double rad = Math.Abs(angleDegrees) * (Math.PI / 180.0);
        // Normalize angle to [0, pi/2)
        rad = rad % (Math.PI / 2.0);
        if (rad < 0.0001) return;

        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        // Bounding box size after rotation
        double rotW = image.Width;
        double rotH = image.Height;

        // Inscribed rectangle maintaining original aspect ratio
        double w1 = (origW * origH) / (origH * cos + origW * sin);
        double w2 = (origW * origH) / (origH * cos + (Math.Pow(origH, 2) / origW) * sin);
        double cropW = Math.Min(w1, w2);
        double cropH = cropW * (origH / origW);

        cropW = Math.Clamp(cropW, 10, rotW);
        cropH = Math.Clamp(cropH, 10, rotH);

        double cropX = Math.Max(0, (rotW - cropW) / 2.0);
        double cropY = Math.Max(0, (rotH - cropH) / 2.0);

        image.Crop(new MagickGeometry((int)cropX, (int)cropY, (uint)cropW, (uint)cropH));
        image.ResetPage();
    }

    private static void SaveImageSafely(MagickImage image, string sourcePath, string destPath)
    {
        string destExt = Path.GetExtension(destPath).ToLowerInvariant();
        if (destExt == ".jpg" || destExt == ".jpeg")
        {
            image.Format = MagickFormat.Jpeg;
            image.Quality = 95;
        }
        else if (destExt == ".webp")
        {
            image.Format = MagickFormat.WebP;
            image.Quality = 92;
        }
        else if (destExt == ".png")
        {
            image.Format = MagickFormat.Png;
        }
        else if (destExt == ".gif")
        {
            image.Format = MagickFormat.Gif;
        }
        else if (destExt == ".bmp")
        {
            image.Format = MagickFormat.Bmp;
        }

        bool isOverwrite = string.Equals(
            Path.GetFullPath(sourcePath),
            Path.GetFullPath(destPath),
            StringComparison.OrdinalIgnoreCase);

        if (isOverwrite)
        {
            string dir = Path.GetDirectoryName(destPath) ?? ".";
            string tempFile = Path.Combine(dir, $"~myView_corr_{Guid.NewGuid():N}{destExt}");
            try
            {
                image.Write(tempFile);
                File.Move(tempFile, destPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
        else
        {
            image.Write(destPath);
        }
    }
}
