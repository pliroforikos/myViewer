using System.Windows.Media;

namespace myView.Models;

public class LoadedImageData
{
    public required ImageSource ImageSource { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public required string FormatName { get; init; }
    public long FileSizeBytes { get; init; }
    public TimeSpan LoadDuration { get; init; }
    public bool IsVector { get; init; }

    public double Megapixels => (PixelWidth * (double)PixelHeight) / 1_000_000.0;

    public string DimensionsString => $"{PixelWidth} × {PixelHeight}" + (Megapixels > 0 ? $" ({Megapixels:F1} MP)" : string.Empty);

    public string AspectRatioString
    {
        get
        {
            if (PixelWidth <= 0 || PixelHeight <= 0) return string.Empty;
            int gcd = GCD(PixelWidth, PixelHeight);
            int rw = PixelWidth / gcd;
            int rh = PixelHeight / gcd;
            // If aspect ratio numbers are reasonable (< 50)
            if (rw < 50 && rh < 50)
                return $"{rw}:{rh}";
            return $"{PixelWidth / (double)PixelHeight:F2}:1";
        }
    }

    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}
