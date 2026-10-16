using System.IO;
using ImageMagick;
using myView.Models;

namespace myView.Services;

public static class SelfTest
{
    public static async Task<bool> RunAllTestsAsync()
    {
        var log = new List<string>();
        void Log(string msg) { Console.WriteLine(msg); log.Add(msg); }

        Log("========================================");
        Log("        myView SELF-TEST SUITE          ");
        Log("========================================");

        string sampleDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "SampleImages");
        if (!Directory.Exists(sampleDir))
        {
            sampleDir = Path.Combine(Directory.GetCurrentDirectory(), "SampleImages");
        }

        Directory.CreateDirectory(sampleDir);

        // 1. Generate PSD with ImageMagick if missing
        string psdPath = Path.Combine(sampleDir, "test5_photoshop.psd");
        if (!File.Exists(psdPath))
        {
            Console.WriteLine("Generating test PSD via Magick.NET...");
            using var psdImg = new MagickImage(MagickColors.MidnightBlue, 1024, 768);
            psdImg.Format = MagickFormat.Psd;
            psdImg.Write(psdPath);
            Log("  ✓ test5_photoshop.psd generated.");
        }

        bool allPassed = true;

        // 2. Test PNG
        try
        {
            string pngPath = Path.Combine(sampleDir, "test1_shapes.png");
            var png = await ImageLoader.LoadImageAsync(pngPath);
            Assert(png.PixelWidth == 800 && png.PixelHeight == 600, $"PNG Dimensions: {png.PixelWidth}x{png.PixelHeight}");
            Log($"  [PASS] PNG loaded in {png.LoadDuration.TotalMilliseconds:F2}ms ({png.DimensionsString})");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] PNG test failed: {ex.Message}");
            allPassed = false;
        }

        // 3. Test BMP
        try
        {
            string bmpPath = Path.Combine(sampleDir, "test2_bitmap.bmp");
            var bmp = await ImageLoader.LoadImageAsync(bmpPath);
            Assert(bmp.PixelWidth == 640 && bmp.PixelHeight == 480, $"BMP Dimensions: {bmp.PixelWidth}x{bmp.PixelHeight}");
            Log($"  [PASS] BMP loaded in {bmp.LoadDuration.TotalMilliseconds:F2}ms ({bmp.DimensionsString})");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] BMP test failed: {ex.Message}");
            allPassed = false;
        }

        // 4. Test JPG & EXIF
        try
        {
            string jpgPath = Path.Combine(sampleDir, "test3_landscape.jpg");
            var jpg = await ImageLoader.LoadImageAsync(jpgPath);
            Assert(jpg.PixelWidth == 1920 && jpg.PixelHeight == 1080, $"JPG Dimensions: {jpg.PixelWidth}x{jpg.PixelHeight}");
            Log($"  [PASS] JPG loaded in {jpg.LoadDuration.TotalMilliseconds:F2}ms ({jpg.DimensionsString})");

            var exif = await MetadataService.ReadMetadataAsync(jpgPath);
            Assert(exif.CameraMake == "Sony" && exif.CameraModel == "ILCE-7M4", $"EXIF: {exif.CameraMake} {exif.CameraModel}");
            Log($"  [PASS] EXIF parsed: Make={exif.CameraMake}, Model={exif.CameraModel}, Date={exif.DateTaken}");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] JPG/EXIF test failed: {ex.Message}");
            allPassed = false;
        }

        // 5. Test SVG (Inkscape vector graphic)
        try
        {
            string svgPath = Path.Combine(sampleDir, "test4_inkscape.svg");
            var svg = await ImageLoader.LoadImageAsync(svgPath);
            Assert(svg.IsVector == true, "SVG IsVector flag");
            Log($"  [PASS] SVG Vector loaded in {svg.LoadDuration.TotalMilliseconds:F2}ms ({svg.DimensionsString})");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] SVG test failed: {ex.Message}");
            allPassed = false;
        }

        // 6. Test PSD (Photoshop document)
        try
        {
            var psd = await ImageLoader.LoadImageAsync(psdPath);
            Assert(psd.PixelWidth == 1024 && psd.PixelHeight == 768, $"PSD Dimensions: {psd.PixelWidth}x{psd.PixelHeight}");
            Log($"  [PASS] PSD loaded in {psd.LoadDuration.TotalMilliseconds:F2}ms ({psd.DimensionsString}, Format={psd.FormatName})");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] PSD test failed: {ex.Message}");
            allPassed = false;
        }

        // 7. Test Folder Navigation & Sorting
        try
        {
            var nav = new FolderNavigationService();
            nav.LoadFolder(sampleDir);
            Assert(nav.TotalCount >= 5, $"Folder items count: {nav.TotalCount}");
            Log($"  [PASS] Folder navigation: scanned {nav.TotalCount} supported image files");

            // Navigation: Next / Prev / Home / End
            nav.First();
            string firstFile = nav.CurrentItem!.FileName;
            nav.Last();
            string lastFile = nav.CurrentItem!.FileName;
            Assert(firstFile != lastFile, "First != Last");
            Log($"  [PASS] Navigation: First='{firstFile}', Last='{lastFile}'");

            // Sort modes
            nav.SetSortMode(SortMode.NameAscending);
            nav.First();
            string firstAsc = nav.CurrentItem!.FileName;
            nav.SetSortMode(SortMode.NameDescending);
            nav.First();
            string firstDesc = nav.CurrentItem!.FileName;
            Assert(firstAsc != firstDesc, "NameAsc != NameDesc");
            Log($"  [PASS] Sorting: NameAsc 1st='{firstAsc}', NameDesc 1st='{firstDesc}'");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] Navigation/Sorting test failed: {ex.Message}");
            allPassed = false;
        }

        // 8. Test In-Memory Cache
        try
        {
            var cache = new ImageCacheService(3);
            string pngPath = Path.Combine(sampleDir, "test1_shapes.png");
            var png = await ImageLoader.LoadImageAsync(pngPath);
            cache.Put(pngPath, png);

            var retrieved = cache.Get(pngPath);
            Assert(retrieved != null && retrieved.PixelWidth == png.PixelWidth, "Cache retrieval");
            Log("  [PASS] LRU Cache hit and retrieval verified.");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] Cache test failed: {ex.Message}");
            allPassed = false;
        }

        Log("========================================");
        if (allPassed)
        {
            Log("   >>> ALL 8 TEST SUITES PASSED! <<<    ");
        }
        else
        {
            Log("   >>> SOME TESTS FAILED! <<<           ");
        }
        Log("========================================");

        try
        {
            File.WriteAllLines("test_results.log", log);
        }
        catch { }

        return allPassed;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"Assertion failed: {message}");
    }
}
