using System.IO;
using System.Windows;
using ImageMagick;

namespace myView.Services;

public static class ImageCropService
{
    private static readonly HashSet<string> SupportedCropExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".webp",
        ".gif",
        ".bmp",
        ".tif", ".tiff"
    };

    /// <summary>
    /// Checks whether the given file extension supports cropping.
    /// </summary>
    public static bool CanCrop(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string ext = Path.GetExtension(filePath);
        return SupportedCropExtensions.Contains(ext) ||
               // Also allow camera RAW & Photoshop to be cropped and saved to JPG/PNG/WebP
               ext.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Crops the image and saves it to the target destination.
    /// Preserves EXIF, ICC profiles and color space when saving.
    /// </summary>
    public static async Task CropAndSaveAsync(
        string sourcePath,
        string destPath,
        Int32Rect cropRect,
        int rotation = 0,
        bool flipH = false)
    {
        await Task.Run(() =>
        {
            using var image = new MagickImage(sourcePath);

            // 1. Normalize orientation from EXIF metadata so pixel dimensions match viewer display
            try
            {
                image.AutoOrient();
            }
            catch
            {
                // Ignore if metadata orientation is missing or invalid
            }

            // 2. Apply user-requested transformations from viewer
            if (flipH)
            {
                image.Flop();
            }

            if (rotation != 0)
            {
                image.Rotate(rotation);
            }

            // 3. Ensure crop coordinates are valid within the transformed image bounds
            int imgW = (int)image.Width;
            int imgH = (int)image.Height;

            int x = Math.Clamp(cropRect.X, 0, Math.Max(0, imgW - 1));
            int y = Math.Clamp(cropRect.Y, 0, Math.Max(0, imgH - 1));
            int w = Math.Clamp(cropRect.Width, 1, imgW - x);
            int h = Math.Clamp(cropRect.Height, 1, imgH - y);

            image.Crop(new MagickGeometry(x, y, (uint)w, (uint)h));
            image.ResetPage();

            // 4. Set appropriate format settings
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

            // 5. Atomic write: If overwriting source, write to a temp file first then replace
            bool isOverwrite = string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destPath),
                StringComparison.OrdinalIgnoreCase);

            if (isOverwrite)
            {
                string dir = Path.GetDirectoryName(destPath) ?? ".";
                string tempFile = Path.Combine(dir, $"~myView_crop_{Guid.NewGuid():N}{destExt}");
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
        });
    }
}
