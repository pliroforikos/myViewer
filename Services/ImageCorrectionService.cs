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
    /// Warps an image by corner displacement (Keystone / Perspective Stretch) and saves the result.
    /// Maps source rectangle (0,0, W, H) to destination quadrilateral (p0: TL, p1: TR, p2: BR, p3: BL).
    /// </summary>
    public static async Task WarpPerspectiveAndSaveAsync(
        string sourcePath,
        string destPath,
        Point p0, Point p1, Point p2, Point p3,
        bool autoCrop = true,
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

            int w = (int)image.Width;
            int h = (int)image.Height;

            double dx0 = p0.X, dy0 = p0.Y;
            double dx1 = p1.X, dy1 = p1.Y;
            double dx2 = p2.X, dy2 = p2.Y;
            double dx3 = p3.X, dy3 = p3.Y;

            double minX = Math.Min(Math.Min(dx0, dx1), Math.Min(dx2, dx3));
            double maxX = Math.Max(Math.Max(dx0, dx1), Math.Max(dx2, dx3));
            double minY = Math.Min(Math.Min(dy0, dy1), Math.Min(dy2, dy3));
            double maxY = Math.Max(Math.Max(dy0, dy1), Math.Max(dy2, dy3));

            int outW = Math.Max(20, (int)Math.Round(maxX - minX));
            int outH = Math.Max(20, (int)Math.Round(maxY - minY));

            string destExt = Path.GetExtension(destPath).ToLowerInvariant();
            bool isTransparent = destExt == ".png" || destExt == ".webp";
            image.BackgroundColor = isTransparent ? MagickColors.Transparent : MagickColors.White;

            double[] distortArgs = new double[]
            {
                0, 0,  dx0 - minX, dy0 - minY,
                w, 0,  dx1 - minX, dy1 - minY,
                w, h,  dx2 - minX, dy2 - minY,
                0, h,  dx3 - minX, dy3 - minY
            };

            image.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            image.Distort(DistortMethod.Perspective, distortArgs);
            image.ResetPage();

            if (autoCrop)
            {
                double cX = Math.Max(dx0 - minX, dx3 - minX);
                double cRight = Math.Min(dx1 - minX, dx2 - minX);
                double cW = cRight - cX;

                double cY = Math.Max(dy0 - minY, dy1 - minY);
                double cBottom = Math.Min(dy3 - minY, dy2 - minY);
                double cH = cBottom - cY;

                if (cW >= 20 && cH >= 20 && cX >= 0 && cY >= 0 && (cX + cW) <= outW && (cY + cH) <= outH)
                {
                    image.Crop(new MagickGeometry((int)Math.Round(cX), (int)Math.Round(cY), (uint)Math.Round(cW), (uint)Math.Round(cH)));
                    image.ResetPage();
                }
            }

            if (autoEnhance)
            {
                image.AutoLevel();
                image.Sharpen(0, 0.5);
            }

            SaveImageSafely(image, sourcePath, destPath);
        });
    }

    /// <summary>
    /// Generates a fast distorted preview BitmapSource from an in-memory MagickImage thumbnail.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource? GeneratePerspectivePreview(
        MagickImage? baseThumb,
        double origW, double origH,
        Point p0, Point p1, Point p2, Point p3,
        bool isKeystoneWarp,
        bool autoCrop,
        bool autoEnhance)
    {
        if (baseThumb == null || origW <= 0 || origH <= 0) return null;

        double tw = (double)baseThumb.Width;
        double th = (double)baseThumb.Height;

        using var copy = baseThumb.Clone();
        copy.BackgroundColor = MagickColors.White;

        if (isKeystoneWarp)
        {
            double sx = tw / origW;
            double sy = th / origH;

            double dx0 = p0.X * sx, dy0 = p0.Y * sy;
            double dx1 = p1.X * sx, dy1 = p1.Y * sy;
            double dx2 = p2.X * sx, dy2 = p2.Y * sy;
            double dx3 = p3.X * sx, dy3 = p3.Y * sy;

            double minX = Math.Min(Math.Min(dx0, dx1), Math.Min(dx2, dx3));
            double maxX = Math.Max(Math.Max(dx0, dx1), Math.Max(dx2, dx3));
            double minY = Math.Min(Math.Min(dy0, dy1), Math.Min(dy2, dy3));
            double maxY = Math.Max(Math.Max(dy0, dy1), Math.Max(dy2, dy3));

            int outW = Math.Max(10, (int)Math.Round(maxX - minX));
            int outH = Math.Max(10, (int)Math.Round(maxY - minY));

            double[] distortArgs = new double[]
            {
                0, 0,    dx0 - minX, dy0 - minY,
                tw, 0,   dx1 - minX, dy1 - minY,
                tw, th,  dx2 - minX, dy2 - minY,
                0, th,   dx3 - minX, dy3 - minY
            };

            copy.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            copy.Distort(DistortMethod.Perspective, distortArgs);
            copy.ResetPage();

            if (autoCrop)
            {
                double cX = Math.Max(dx0 - minX, dx3 - minX);
                double cRight = Math.Min(dx1 - minX, dx2 - minX);
                double cW = cRight - cX;

                double cY = Math.Max(dy0 - minY, dy1 - minY);
                double cBottom = Math.Min(dy3 - minY, dy2 - minY);
                double cH = cBottom - cY;

                if (cW >= 10 && cH >= 10 && cX >= 0 && cY >= 0 && (cX + cW) <= outW && (cY + cH) <= outH)
                {
                    copy.Crop(new MagickGeometry((int)Math.Round(cX), (int)Math.Round(cY), (uint)Math.Round(cW), (uint)Math.Round(cH)));
                    copy.ResetPage();
                }
            }
        }
        else
        {
            // Quad crop mode
            double sx = tw / origW;
            double sy = th / origH;

            double q0x = p0.X * sx, q0y = p0.Y * sy;
            double q1x = p1.X * sx, q1y = p1.Y * sy;
            double q2x = p2.X * sx, q2y = p2.Y * sy;
            double q3x = p3.X * sx, q3y = p3.Y * sy;

            double wTop = Math.Sqrt(Math.Pow(q1x - q0x, 2) + Math.Pow(q1y - q0y, 2));
            double wBottom = Math.Sqrt(Math.Pow(q2x - q3x, 2) + Math.Pow(q2y - q3y, 2));
            int outW = Math.Max(10, (int)Math.Round(Math.Max(wTop, wBottom)));

            double hLeft = Math.Sqrt(Math.Pow(q3x - q0x, 2) + Math.Pow(q3y - q0y, 2));
            double hRight = Math.Sqrt(Math.Pow(q2x - q1x, 2) + Math.Pow(q2y - q1y, 2));
            int outH = Math.Max(10, (int)Math.Round(Math.Max(hLeft, hRight)));

            double[] distortArgs = new double[]
            {
                q0x, q0y,  0, 0,
                q1x, q1y,  outW, 0,
                q2x, q2y,  outW, outH,
                q3x, q3y,  0, outH
            };

            copy.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            copy.Distort(DistortMethod.Perspective, distortArgs);
            copy.ResetPage();
        }

        if (autoEnhance)
        {
            copy.AutoLevel();
            copy.Sharpen(0, 0.5);
        }

        using var ms = new MemoryStream();
        copy.Write(ms, MagickFormat.Bmp);
        ms.Position = 0;

        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
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
