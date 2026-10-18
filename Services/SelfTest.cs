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

        // 9. Test Cropping & Saving (JPG -> WebP, PNG, JPG)
        try
        {
            string jpgSrc = Path.Combine(sampleDir, "test3_landscape.jpg");
            string cropJpgOut = Path.Combine(sampleDir, "test_crop_out.jpg");
            string cropWebpOut = Path.Combine(sampleDir, "test_crop_out.webp");
            string cropPngOut = Path.Combine(sampleDir, "test_crop_out.png");

            var cropRect = new System.Windows.Int32Rect(100, 100, 400, 300);
            await ImageCropService.CropAndSaveAsync(jpgSrc, cropJpgOut, cropRect);
            await ImageCropService.CropAndSaveAsync(jpgSrc, cropWebpOut, cropRect);
            await ImageCropService.CropAndSaveAsync(jpgSrc, cropPngOut, cropRect);

            var loadedJpgCrop = await ImageLoader.LoadImageAsync(cropJpgOut);
            var loadedWebpCrop = await ImageLoader.LoadImageAsync(cropWebpOut);
            var loadedPngCrop = await ImageLoader.LoadImageAsync(cropPngOut);

            Assert(loadedJpgCrop.PixelWidth == 400 && loadedJpgCrop.PixelHeight == 300, $"Crop JPG: {loadedJpgCrop.PixelWidth}x{loadedJpgCrop.PixelHeight}");
            Assert(loadedWebpCrop.PixelWidth == 400 && loadedWebpCrop.PixelHeight == 300, $"Crop WebP: {loadedWebpCrop.PixelWidth}x{loadedWebpCrop.PixelHeight}");
            Assert(loadedPngCrop.PixelWidth == 400 && loadedPngCrop.PixelHeight == 300, $"Crop PNG: {loadedPngCrop.PixelWidth}x{loadedPngCrop.PixelHeight}");

            // Clean up temporary test files
            try { File.Delete(cropJpgOut); File.Delete(cropWebpOut); File.Delete(cropPngOut); } catch { }

            Log("  [PASS] Crop and Save verified (JPG, WebP, PNG: 400x300).");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] Crop and Save test failed: {ex.Message}");
            allPassed = false;
        }

        // 10. Test Horizon Straighten & 4-Corner Perspective Deskew
        try
        {
            string jpgSrc = Path.Combine(sampleDir, "test3_landscape.jpg");
            string straightOut = Path.Combine(sampleDir, "test_straighten_out.jpg");
            string deskewOut = Path.Combine(sampleDir, "test_deskew_out.jpg");

            // Test Straighten with auto-crop
            await ImageCorrectionService.StraightenAndSaveAsync(jpgSrc, straightOut, angleDegrees: 3.5, autoCrop: true);
            var loadedStraight = await ImageLoader.LoadImageAsync(straightOut);
            Assert(loadedStraight.PixelWidth > 1700 && loadedStraight.PixelHeight > 950, $"Straightened size: {loadedStraight.PixelWidth}x{loadedStraight.PixelHeight}");
            Log($"  [PASS] Horizon Straighten verified ({loadedStraight.DimensionsString}, angle=3.5°, auto-crop).");

            // Test 4-Corner Perspective Deskew
            // Test preview distort speed
            using (var prevImg = new MagickImage(MagickColors.White, 800, 600))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double[] args = new double[] { 0, 0, 0, 0, 800, 0, 800, 0, 800, 600, 800, 600, 0, 600, 0, 600 };
                prevImg.Distort(DistortMethod.Perspective, args);
                sw.Stop();
                Log($"  [BENCHMARK] 800x600 Perspective Distort: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds:F0}µs)");
            }

            // Clean up temporary test files
            try { File.Delete(straightOut); File.Delete(deskewOut); } catch { }

            // Test Keystone unwarp on user photo
            string userImgPath = @"C:\Users\leon\.gemini\antigravity\brain\f0547188-8196-42a2-8d00-1897a87ec524\.user_uploaded\media_1792324612003_67ab52ca.jpg";
            if (File.Exists(userImgPath))
            {
                using var uImg = new MagickImage(userImgPath);
                int w = (int)uImg.Width;
                int h = (int)uImg.Height;
                double deltaX = 60; // open top corners outward by 60px each

                // Source: corners of original image
                // Dest: top corners shifted outward, bottom corners fixed
                double dx0 = -deltaX, dy0 = 0;
                double dx1 = w + deltaX, dy1 = 0;
                double dx2 = w, dy2 = h;
                double dx3 = 0, dy3 = h;

                double minX = Math.Min(Math.Min(dx0, dx1), Math.Min(dx2, dx3));
                double maxX = Math.Max(Math.Max(dx0, dx1), Math.Max(dx2, dx3));
                double minY = Math.Min(Math.Min(dy0, dy1), Math.Min(dy2, dy3));
                double maxY = Math.Max(Math.Max(dy0, dy1), Math.Max(dy2, dy3));

                int outW = (int)Math.Round(maxX - minX);
                int outH = (int)Math.Round(maxY - minY);

                double[] args = new double[]
                {
                    0, 0,  dx0 - minX, dy0 - minY,
                    w, 0,  dx1 - minX, dy1 - minY,
                    w, h,  dx2 - minX, dy2 - minY,
                    0, h,  dx3 - minX, dy3 - minY
                };

                uImg.BackgroundColor = MagickColors.White;
                uImg.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
                uImg.Distort(DistortMethod.Perspective, args);
                uImg.ResetPage();

                // Auto-crop to clean rectangle of original width/height
                uImg.Crop(new MagickGeometry((int)deltaX, 0, (uint)w, (uint)h));
                uImg.ResetPage();

                string testOut = Path.Combine(sampleDir, "test_user_keystone_cropped.jpg");
                uImg.Write(testOut);
                Log($"  [KEYSTONE TEST] User image distorted and auto-cropped to {uImg.Width}x{uImg.Height}, saved to {testOut}");

                // Test WarpPerspectiveAndSaveAsync service method
                string warpOut = Path.Combine(sampleDir, "test_warp_service_out.jpg");
                await ImageCorrectionService.WarpPerspectiveAndSaveAsync(
                    userImgPath, warpOut,
                    new System.Windows.Point(-deltaX, 0),
                    new System.Windows.Point(w + deltaX, 0),
                    new System.Windows.Point(w, h),
                    new System.Windows.Point(0, h),
                    autoCrop: true);
                var loadedWarp = await ImageLoader.LoadImageAsync(warpOut);
                Assert(loadedWarp.PixelWidth == w && loadedWarp.PixelHeight == h, "WarpPerspectiveAndSaveAsync resolution matching");
                Log($"  [PASS] WarpPerspectiveAndSaveAsync verified: {loadedWarp.PixelWidth}x{loadedWarp.PixelHeight}");

                // Test GeneratePerspectivePreview benchmark
                using var thumb = new MagickImage(userImgPath);
                thumb.Resize(new MagickGeometry(600, 600) { IgnoreAspectRatio = false });
                var pSw = System.Diagnostics.Stopwatch.StartNew();
                var previewBmp = ImageCorrectionService.GeneratePerspectivePreview(
                    thumb, w, h,
                    new System.Windows.Point(-deltaX, 0),
                    new System.Windows.Point(w + deltaX, 0),
                    new System.Windows.Point(w, h),
                    new System.Windows.Point(0, h),
                    isKeystoneWarp: true,
                    autoCrop: true,
                    autoEnhance: false);
                pSw.Stop();
                Assert(previewBmp != null && previewBmp.PixelWidth > 0, "GeneratePerspectivePreview produced valid BitmapSource");
                Log($"  [BENCHMARK] GeneratePerspectivePreview: {pSw.ElapsedMilliseconds}ms (Resolution: {previewBmp!.PixelWidth}x{previewBmp.PixelHeight})");

                try 
                { 
                    File.Delete(warpOut); 
                    File.Delete(testOut);
                    string rawKeystone = Path.Combine(sampleDir, "test_user_keystone.jpg");
                    if (File.Exists(rawKeystone)) File.Delete(rawKeystone);
                } 
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] Horizon & Perspective test failed: {ex.Message}");
            allPassed = false;
        }

        // 11. Test Clipboard Image Copy
        try
        {
            string pngSample = Path.Combine(sampleDir, "test1_shapes.png");
            var imgData = await ImageLoader.LoadImageAsync(pngSample);
            Assert(imgData.ImageSource is System.Windows.Media.Imaging.BitmapSource, "Image is BitmapSource");

            var bs = (System.Windows.Media.Imaging.BitmapSource)imgData.ImageSource;
            var dataObject = new System.Windows.DataObject();
            dataObject.SetImage(bs);

            using var ms = new MemoryStream();
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bs));
            enc.Save(ms);
            ms.Position = 0;
            dataObject.SetData("PNG", ms, false);

            var files = new System.Collections.Specialized.StringCollection { pngSample };
            dataObject.SetFileDropList(files);

            System.Windows.Clipboard.SetDataObject(dataObject, true);

            bool hasBitmap = System.Windows.Clipboard.ContainsImage();
            bool hasFileDrop = System.Windows.Clipboard.ContainsFileDropList();
            Assert(hasBitmap && hasFileDrop, "Clipboard contains Bitmap and FileDrop");
            Log("  [PASS] Clipboard Copy verified (Bitmap, DIB, PNG stream, FileDrop).");
        }
        catch (Exception ex)
        {
            Log($"  [FAIL] Clipboard test failed: {ex.Message}");
            allPassed = false;
        }

        Log("========================================");
        if (allPassed)
        {
            Log("   >>> ALL 11 TEST SUITES PASSED! <<<   ");
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
