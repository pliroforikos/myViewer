using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using myView.Models;
using myView.Services;

namespace myView;

public partial class MainWindow : Window
{
    private readonly FolderNavigationService _folderNav = new();
    private readonly ImageCacheService _cache = new(12);

    private LoadedImageData? _currentData;
    private ImageExifData? _currentExif;
    private List<ExifDirectoryGroup> _allExifGroups = new();

    // Pan & Zoom state
    private double _scale = 1.0;
    private double _centerX = 0.0;
    private double _centerY = 0.0;
    private int _rotation = 0;
    private bool _flipH = false;
    private bool _isFitMode = true;

    // Mouse pan state
    private bool _isPanning = false;
    private Point _lastMousePos;

    // HUD auto-hide timer
    private readonly DispatcherTimer _hudTimer = new();
    private bool _isMouseOverHud = false;

    // Fullscreen state
    private bool _isFullscreen = false;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;

    // Crop state
    private bool _isCropMode = false;
    private Rect _cropPixelRect = Rect.Empty;
    private Rect _cropRectScreen = Rect.Empty;
    private enum CropDragMode { None, Creating, Moving, ResizeNW, ResizeNE, ResizeSW, ResizeSE, ResizeN, ResizeS, ResizeW, ResizeE }
    private CropDragMode _cropDragMode = CropDragMode.None;
    private Point _cropDragStartPoint;
    private Rect _cropRectStart;
    private readonly DispatcherTimer _toastTimer = new();

    // CancellationToken for current decode
    private CancellationTokenSource? _loadCts;

    public MainWindow()
    {
        InitializeComponent();

        _folderNav.SelectionChanged += FolderNav_SelectionChanged;
        _folderNav.FolderReloaded += FolderNav_FolderReloaded;

        _hudTimer.Interval = TimeSpan.FromSeconds(2.5);
        _hudTimer.Tick += (s, e) =>
        {
            if (!_isMouseOverHud && BottomHudBar.Visibility == Visibility.Visible)
            {
                BottomHudBar.Opacity = 0.0;
            }
        };
        _hudTimer.Start();

        _toastTimer.Interval = TimeSpan.FromSeconds(3.0);
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            ToastBadge.Visibility = Visibility.Collapsed;
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Check command-line arguments
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            OpenSpecificFile(args[1]);
        }
        else if (args.Length > 1 && Directory.Exists(args[1]))
        {
            OpenFolder(args[1]);
        }
    }

    #region File & Folder Loading

    public void OpenSpecificFile(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            _folderNav.LoadFolder(dir, filePath);
        }
    }

    public void OpenFolder(string folderPath)
    {
        _folderNav.LoadFolder(folderPath);
    }

    private void FolderNav_FolderReloaded()
    {
        UpdateSortButtonLabel();
    }

    private async void FolderNav_SelectionChanged()
    {
        var item = _folderNav.CurrentItem;
        if (item == null)
        {
            DisplayImage.Source = null;
            _currentData = null;
            _currentExif = null;
            EmptyStateOverlay.Visibility = Visibility.Visible;
            CounterBadge.Visibility = Visibility.Collapsed;
            PillMetaInfo.Visibility = Visibility.Collapsed;
            TxtFileName.Text = string.Empty;
            TxtHudIndex.Text = "0 / 0";
            return;
        }

        EmptyStateOverlay.Visibility = Visibility.Collapsed;
        CounterBadge.Visibility = Visibility.Visible;
        PillMetaInfo.Visibility = Visibility.Visible;

        int current1Based = _folderNav.CurrentIndex + 1;
        int total = _folderNav.TotalCount;
        TxtCounter.Text = $"{current1Based} / {total}";
        TxtHudIndex.Text = $"{current1Based} / {total}";
        TxtFileName.Text = item.FileName;
        Title = $"{item.FileName} - myView";

        // Prefetch adjacent images (+1, +2, -1, -2) for instant response
        PrefetchNeighbors();

        // Load image
        await DisplayImageItemAsync(item);
    }

    private void PrefetchNeighbors()
    {
        var paths = new List<string>();
        for (int offset = 1; offset <= 2; offset++)
        {
            var next = _folderNav.GetRelative(offset);
            if (next != null) paths.Add(next.FilePath);

            var prev = _folderNav.GetRelative(-offset);
            if (prev != null) paths.Add(prev.FilePath);
        }
        _cache.Prefetch(paths);
    }

    private async Task DisplayImageItemAsync(ImageItem item)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        // Reset transforms
        _rotation = 0;
        _flipH = false;
        _isFitMode = true;

        // Check in-memory cache first (0ms latency!)
        var cached = _cache.Get(item.FilePath);
        if (cached != null)
        {
            ApplyLoadedImage(cached);
            LoadExifDataAsync(item.FilePath);
            return;
        }

        // Show subtle non-blocking loading indicator
        LoadingBadge.Visibility = Visibility.Visible;

        try
        {
            var data = await ImageLoader.LoadImageAsync(item.FilePath, token);
            _cache.Put(item.FilePath, data);

            if (!token.IsCancellationRequested)
            {
                ApplyLoadedImage(data);
                LoadExifDataAsync(item.FilePath);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation when user skips quickly
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to display image:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            LoadingBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyLoadedImage(LoadedImageData data)
    {
        if (_isCropMode)
        {
            ExitCropMode();
        }

        _currentData = data;
        DisplayImage.Source = data.ImageSource;
        DisplayImage.Width = data.PixelWidth;
        DisplayImage.Height = data.PixelHeight;

        TxtResolution.Text = data.DimensionsString;
        TxtFileSize.Text = FormatBytes(data.FileSizeBytes);
        TxtFormat.Text = data.FormatName;
        TxtSpeed.Text = $"⚡ {data.LoadDuration.TotalMilliseconds:F1} ms";

        FitToScreen();
    }

    private async void LoadExifDataAsync(string filePath)
    {
        var exif = await MetadataService.ReadMetadataAsync(filePath);
        _currentExif = exif;
        _allExifGroups = exif.DirectoryGroups;

        UpdateExifUi(exif);
    }

    private void UpdateExifUi(ImageExifData exif)
    {
        ExifCameraModel.Text = !string.IsNullOrWhiteSpace(exif.CameraModel) ? exif.CameraModel : "Camera info unavailable";
        ExifLensModel.Text = !string.IsNullOrWhiteSpace(exif.LensModel) ? exif.LensModel : "Lens info unavailable";

        ExifShutter.Text = !string.IsNullOrWhiteSpace(exif.ExposureTime) ? exif.ExposureTime : "-";
        ExifAperture.Text = !string.IsNullOrWhiteSpace(exif.FNumber) ? exif.FNumber : "-";
        ExifIso.Text = !string.IsNullOrWhiteSpace(exif.IsoRating) ? (exif.IsoRating.StartsWith("ISO") ? exif.IsoRating : $"ISO {exif.IsoRating}") : "-";
        ExifFocal.Text = !string.IsNullOrWhiteSpace(exif.FocalLength) ? exif.FocalLength : "-";
        ExifBias.Text = !string.IsNullOrWhiteSpace(exif.ExposureBias) ? exif.ExposureBias : "0 EV";

        ExifDateTaken.Text = !string.IsNullOrWhiteSpace(exif.DateTaken) ? exif.DateTaken : (_currentData != null ? "-" : "-");

        if (_currentData != null)
        {
            ExifDimensions.Text = $"{_currentData.PixelWidth} × {_currentData.PixelHeight}";
            ExifMegapixels.Text = $"{_currentData.Megapixels:F1} MP";
            ExifAspectRatio.Text = _currentData.AspectRatioString;
            ExifFileSize.Text = FormatBytes(_currentData.FileSizeBytes);
            ExifFormat.Text = _currentData.FormatName;
            ExifColorSpace.Text = !string.IsNullOrWhiteSpace(exif.ColorSpace) ? exif.ColorSpace : "sRGB";
        }

        ExifFlash.Text = !string.IsNullOrWhiteSpace(exif.Flash) ? exif.Flash : "-";
        ExifWhiteBalance.Text = !string.IsNullOrWhiteSpace(exif.WhiteBalance) ? exif.WhiteBalance : "-";
        ExifMetering.Text = !string.IsNullOrWhiteSpace(exif.MeteringMode) ? exif.MeteringMode : "-";
        ExifProgram.Text = !string.IsNullOrWhiteSpace(exif.ExposureProgram) ? exif.ExposureProgram : "-";

        if (exif.HasGps && !string.IsNullOrWhiteSpace(exif.GpsCoordinatesFormatted))
        {
            CardGps.Visibility = Visibility.Visible;
            ExifGpsCoords.Text = exif.GpsCoordinatesFormatted;
        }
        else
        {
            CardGps.Visibility = Visibility.Collapsed;
        }

        FilterExifGroups(TxtTagSearch.Text);
    }

    private void FilterExifGroups(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            ItemsExifGroups.ItemsSource = _allExifGroups;
            return;
        }

        var filtered = new List<ExifDirectoryGroup>();
        foreach (var group in _allExifGroups)
        {
            var matchingTags = group.Tags.Where(t =>
                t.TagName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                t.TagValue.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingTags.Count > 0)
            {
                var newGroup = new ExifDirectoryGroup { DirectoryName = group.DirectoryName };
                newGroup.Tags.AddRange(matchingTags);
                filtered.Add(newGroup);
            }
        }
        ItemsExifGroups.ItemsSource = filtered;
    }

    private void TxtTagSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterExifGroups(TxtTagSearch.Text);
    }

    #endregion

    #region Pan & Zoom Mechanics

    private void FitToScreen()
    {
        if (_currentData == null) return;

        double viewportW = ViewportBorder.ActualWidth;
        double viewportH = ViewportBorder.ActualHeight;

        if (viewportW <= 0 || viewportH <= 0)
        {
            Dispatcher.BeginInvoke(new Action(FitToScreen), DispatcherPriority.Loaded);
            return;
        }

        // Calculate dimensions accounting for 90/270 rotation
        bool isRotated90 = (_rotation % 180 != 0);
        double effW = isRotated90 ? _currentData.PixelHeight : _currentData.PixelWidth;
        double effH = isRotated90 ? _currentData.PixelWidth : _currentData.PixelHeight;

        if (effW <= 0 || effH <= 0) return;

        // Add padding on edges so photo fits comfortably within borders
        double padX = Math.Min(16.0, viewportW * 0.02);
        double padY = Math.Min(16.0, viewportH * 0.02);
        double availW = Math.Max(10.0, viewportW - padX * 2);
        double availH = Math.Max(10.0, viewportH - padY * 2);

        double scaleX = availW / effW;
        double scaleY = availH / effH;
        _scale = Math.Min(scaleX, scaleY);

        // Center image center exactly at viewport center
        _centerX = viewportW / 2.0;
        _centerY = viewportH / 2.0;
        _isFitMode = true;

        ApplyTransforms();
    }

    private void NativeSize()
    {
        if (_currentData == null) return;

        double viewportW = ViewportBorder.ActualWidth;
        double viewportH = ViewportBorder.ActualHeight;

        _scale = 1.0;
        _centerX = viewportW > 0 ? viewportW / 2.0 : _currentData.PixelWidth / 2.0;
        _centerY = viewportH > 0 ? viewportH / 2.0 : _currentData.PixelHeight / 2.0;
        _isFitMode = false;

        ApplyTransforms();
    }

    private void ZoomAtPoint(Point mousePos, double factor)
    {
        if (_currentData == null) return;

        double oldScale = _scale;
        double newScale = Math.Clamp(_scale * factor, 0.01, 50.0);
        if (Math.Abs(newScale - oldScale) < 0.00001) return;

        double actualRatio = newScale / oldScale;

        // Anchor zoom around cursor position: mousePos remains stationary on screen
        _centerX = mousePos.X + (_centerX - mousePos.X) * actualRatio;
        _centerY = mousePos.Y + (_centerY - mousePos.Y) * actualRatio;
        _scale = newScale;
        _isFitMode = false;

        ApplyTransforms();
    }

    private void ApplyTransforms()
    {
        if (_currentData == null) return;

        var matrix = Matrix.Identity;

        // 1. Move image center to origin (0, 0)
        matrix.Translate(-_currentData.PixelWidth / 2.0, -_currentData.PixelHeight / 2.0);

        // 2. Horizontal flip around center
        if (_flipH)
            matrix.Scale(-1.0, 1.0);

        // 3. Rotate around center
        if (_rotation != 0)
            matrix.Rotate(_rotation);

        // 4. Scale around center
        matrix.Scale(_scale, _scale);

        // 5. Place image center at screen position (_centerX, _centerY)
        matrix.Translate(_centerX, _centerY);

        ImageMatrixTransform.Matrix = matrix;

        TxtZoomLevel.Text = $"{Math.Round(_scale * 100)}%";

        if (_isCropMode)
        {
            UpdateCropScreenFromPixels();
        }
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        Point mousePos = e.GetPosition(ViewportBorder);
        double factor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
        ZoomAtPoint(mousePos, factor);
        RevealHud();
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                // Double-click: toggle Fit to Screen vs Native Size (1:1)
                if (_isFitMode)
                {
                    NativeSize();
                }
                else
                {
                    FitToScreen();
                }
            }
            else
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(ViewportBorder);
                ViewportBorder.CaptureMouse();
                ViewportBorder.Cursor = Cursors.Hand;
            }
        }
        RevealHud();
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            Point current = e.GetPosition(ViewportBorder);
            double deltaX = current.X - _lastMousePos.X;
            double deltaY = current.Y - _lastMousePos.Y;

            _centerX += deltaX;
            _centerY += deltaY;
            _lastMousePos = current;
            _isFitMode = false;

            ApplyTransforms();
        }
        RevealHud();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _isPanning)
        {
            _isPanning = false;
            ViewportBorder.ReleaseMouseCapture();
            ViewportBorder.Cursor = Cursors.Arrow;
        }
    }

    private void ViewportBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isFitMode)
        {
            FitToScreen();
        }
        else if (_isCropMode)
        {
            UpdateCropScreenFromPixels();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isFitMode)
        {
            FitToScreen();
        }
        else if (_isCropMode)
        {
            UpdateCropScreenFromPixels();
        }
    }

    #endregion

    #region Keyboard Navigation & Shortcuts

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't capture shortcuts if typing in search box
        if (TxtTagSearch.IsFocused) return;

        if (_isCropMode)
        {
            if (e.Key == Key.Escape || e.Key == Key.C)
            {
                ExitCropMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                BtnCropSaveAs_Click(sender, e);
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
            case Key.Space:
            case Key.PageDown:
                _folderNav.Next();
                RevealHud();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Back:
            case Key.PageUp:
                _folderNav.Previous();
                RevealHud();
                e.Handled = true;
                break;

            case Key.Home:
                _folderNav.First();
                RevealHud();
                e.Handled = true;
                break;

            case Key.End:
                _folderNav.Last();
                RevealHud();
                e.Handled = true;
                break;

            case Key.OemPlus:
            case Key.Add:
                Point center = new(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0);
                ZoomAtPoint(center, 1.25);
                RevealHud();
                e.Handled = true;
                break;

            case Key.OemMinus:
            case Key.Subtract:
                Point centerOut = new(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0);
                ZoomAtPoint(centerOut, 1.0 / 1.25);
                RevealHud();
                e.Handled = true;
                break;

            case Key.F:
            case Key.D0:
            case Key.NumPad0:
                FitToScreen();
                RevealHud();
                e.Handled = true;
                break;

            case Key.D1:
            case Key.NumPad1:
                NativeSize();
                RevealHud();
                e.Handled = true;
                break;

            case Key.R:
                _rotation = (_rotation + 90) % 360;
                if (_isFitMode) FitToScreen(); else ApplyTransforms();
                RevealHud();
                e.Handled = true;
                break;

            case Key.L:
                _rotation = (_rotation + 270) % 360;
                if (_isFitMode) FitToScreen(); else ApplyTransforms();
                RevealHud();
                e.Handled = true;
                break;

            case Key.H:
                _flipH = !_flipH;
                ApplyTransforms();
                RevealHud();
                e.Handled = true;
                break;

            case Key.S:
                _folderNav.CycleSortMode();
                UpdateSortButtonLabel();
                RevealHud();
                e.Handled = true;
                break;

            case Key.I:
                ToggleExifSidebar();
                RevealHud();
                e.Handled = true;
                break;

            case Key.C:
                ToggleCropMode();
                e.Handled = true;
                break;

            case Key.F11:
                ToggleFullscreen();
                RevealHud();
                e.Handled = true;
                break;

            case Key.O:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers == ModifierKeys.None)
                {
                    PromptOpenFile();
                    e.Handled = true;
                }
                break;

            case Key.Delete:
                PromptDeleteCurrent();
                e.Handled = true;
                break;

            case Key.F1:
                if (AboutOverlay.Visibility == Visibility.Visible)
                {
                    AboutOverlay.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateDefaultStatusUi();
                    AboutOverlay.Visibility = Visibility.Visible;
                }
                e.Handled = true;
                break;

            case Key.Escape:
                if (AboutOverlay.Visibility == Visibility.Visible)
                {
                    AboutOverlay.Visibility = Visibility.Collapsed;
                }
                else if (ExifSidebar.Visibility == Visibility.Visible)
                {
                    ExifSidebar.Visibility = Visibility.Collapsed;
                }
                else if (_isFullscreen)
                {
                    ToggleFullscreen();
                }
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region HUD & Sidebar Controls

    private void RevealHud()
    {
        BottomHudBar.Opacity = 1.0;
        _hudTimer.Stop();
        _hudTimer.Start();
    }

    private void BottomHud_MouseEnter(object sender, MouseEventArgs e)
    {
        _isMouseOverHud = true;
        BottomHudBar.Opacity = 1.0;
    }

    private void BottomHud_MouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOverHud = false;
        _hudTimer.Stop();
        _hudTimer.Start();
    }

    private void BtnFirst_Click(object sender, RoutedEventArgs e) => _folderNav.First();
    private void BtnPrev_Click(object sender, RoutedEventArgs e) => _folderNav.Previous();
    private void BtnNext_Click(object sender, RoutedEventArgs e) => _folderNav.Next();
    private void BtnLast_Click(object sender, RoutedEventArgs e) => _folderNav.Last();

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        Point center = new(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0);
        ZoomAtPoint(center, 1.25);
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        Point center = new(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0);
        ZoomAtPoint(center, 1.0 / 1.25);
    }

    private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => NativeSize();
    private void BtnFit_Click(object sender, RoutedEventArgs e) => FitToScreen();
    private void BtnNative_Click(object sender, RoutedEventArgs e) => NativeSize();

    private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
    {
        _rotation = (_rotation + 270) % 360;
        if (_isFitMode) FitToScreen(); else ApplyTransforms();
    }

    private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
    {
        _rotation = (_rotation + 90) % 360;
        if (_isFitMode) FitToScreen(); else ApplyTransforms();
    }

    private void BtnFlipH_Click(object sender, RoutedEventArgs e)
    {
        _flipH = !_flipH;
        ApplyTransforms();
    }

    private void BtnCrop_Click(object sender, RoutedEventArgs e) => ToggleCropMode();

    private void BtnSort_Click(object sender, RoutedEventArgs e)
    {
        _folderNav.CycleSortMode();
        UpdateSortButtonLabel();
    }

    private void UpdateSortButtonLabel()
    {
        TxtSortMode.Text = _folderNav.CurrentSortMode switch
        {
            SortMode.NameAscending => "Name ▲",
            SortMode.NameDescending => "Name ▼",
            SortMode.DateDescending => "Date ▼",
            SortMode.DateAscending => "Date ▲",
            SortMode.SizeDescending => "Size ▼",
            SortMode.SizeAscending => "Size ▲",
            _ => "Sort"
        };
    }

    private void BtnExif_Click(object sender, RoutedEventArgs e) => ToggleExifSidebar();
    private void BtnCloseExif_Click(object sender, RoutedEventArgs e) => ExifSidebar.Visibility = Visibility.Collapsed;

    private void ToggleExifSidebar()
    {
        ExifSidebar.Visibility = (ExifSidebar.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnCopyExif_Click(object sender, RoutedEventArgs e)
    {
        if (_currentExif == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- Image Metadata: {_folderNav.CurrentItem?.FileName} ---");
        if (_currentData != null)
        {
            sb.AppendLine($"Resolution: {_currentData.DimensionsString}");
            sb.AppendLine($"Format: {_currentData.FormatName}");
            sb.AppendLine($"File Size: {FormatBytes(_currentData.FileSizeBytes)}");
        }
        sb.AppendLine($"Camera: {_currentExif.CameraMake} {_currentExif.CameraModel}");
        sb.AppendLine($"Lens: {_currentExif.LensModel}");
        sb.AppendLine($"Exposure: {_currentExif.ExposureTime}, {_currentExif.FNumber}, {_currentExif.IsoRating}, {_currentExif.FocalLength}");
        sb.AppendLine($"Date Shot: {_currentExif.DateTaken}");
        if (_currentExif.HasGps)
        {
            sb.AppendLine($"GPS: {_currentExif.GpsCoordinatesFormatted}");
        }

        sb.AppendLine();
        sb.AppendLine("--- All EXIF Tags ---");
        foreach (var group in _currentExif.DirectoryGroups)
        {
            sb.AppendLine($"[{group.DirectoryName}]");
            foreach (var tag in group.Tags)
            {
                sb.AppendLine($"  {tag.TagName}: {tag.TagValue}");
            }
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Metadata copied to clipboard!", "myView", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private void BtnOpenMap_Click(object sender, RoutedEventArgs e)
    {
        if (_currentExif?.GoogleMapsUrl != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _currentExif.GoogleMapsUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore process launch errors
            }
        }
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            TopHeaderOverlay.Visibility = Visibility.Collapsed;
            _isFullscreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            TopHeaderOverlay.Visibility = Visibility.Visible;
            _isFullscreen = false;
        }
        FitToScreen();
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e) => PromptOpenFile();

    private void PromptOpenFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Image - myView",
            Filter = "All Supported Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff;*.ico;*.psd;*.psb;*.svg;*.svgz;*.cr2;*.cr3;*.nef;*.nrw;*.arw;*.dng;*.orf;*.rw2;*.pef;*.raf;*.x3f;*.srw|" +
                     "Standard Images (*.jpg;*.png;*.bmp;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif|" +
                     "RAW Camera Files (*.cr2;*.cr3;*.nef;*.arw;*.dng;...)|*.cr2;*.cr3;*.nef;*.nrw;*.arw;*.dng;*.orf;*.rw2;*.pef;*.raf;*.x3f;*.srw|" +
                     "Photoshop (*.psd;*.psb)|*.psd;*.psb|" +
                     "Inkscape / Vector (*.svg;*.svgz)|*.svg;*.svgz|" +
                     "All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            OpenSpecificFile(ofd.FileName);
        }
    }

    private void PromptDeleteCurrent()
    {
        var item = _folderNav.CurrentItem;
        if (item == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete this file?\n\n{item.FileName}",
            "Delete Confirmation - myView",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                File.Delete(item.FilePath);
                _folderNav.RemoveCurrent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete file:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Window TitleBar Controls

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        PathMaximize.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "IconRestore" : "IconMaximize");
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    #endregion

    #region About Dialog

    private void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        UpdateDefaultStatusUi();
        AboutOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateDefaultStatusUi()
    {
        if (FileAssociationService.IsRegistered())
        {
            TxtDefaultStatus.Text = "✓ Το myView είναι καταχωρημένο στα Windows. Πατήστε για άνοιγμα των ρυθμίσεων προεπιλογής.";
            TxtBtnSetAsDefault.Text = "⚙️ Ρυθμίσεις Προεπιλογής";
        }
        else
        {
            TxtDefaultStatus.Text = "Καταχώριση στα Windows και άνοιγμα ρυθμίσεων προεπιλογής για όλα τα υποστηριζόμενα αρχεία.";
            TxtBtnSetAsDefault.Text = "🛠️ Ορισμός ως Προεπιλογή";
        }
    }

    private void BtnSetAsDefault_Click(object sender, RoutedEventArgs e)
    {
        bool ok = FileAssociationService.RegisterFileAssociations(out string message);
        UpdateDefaultStatusUi();

        if (ok)
        {
            FileAssociationService.OpenWindowsDefaultAppsSettings();
            MessageBox.Show(
                "Το myView καταχωρήθηκε επιτυχώς στο σύστημα!\n\n" +
                "Ανοίγουν αυτόματα οι Ρυθμίσεις Προεπιλεγμένων Εφαρμογών των Windows.\n" +
                "Επιλέξτε το 'myView' και πατήστε 'Ορισμός ως προεπιλογή' (Set default) για να ολοκληρώσετε.",
                "Προεπιλεγμένη Εφαρμογή - myView",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                $"Αποτυχία καταχώρισης στο σύστημα:\n{message}",
                "myView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BtnCloseAbout_Click(object sender, RoutedEventArgs e)
    {
        AboutOverlay.Visibility = Visibility.Collapsed;
    }

    private void AboutOverlay_BackgroundMouseDown(object sender, MouseButtonEventArgs e)
    {
        AboutOverlay.Visibility = Visibility.Collapsed;
    }

    private void AboutCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // Prevent click on card from closing dialog
    }

    private void BtnOpenPlarg_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://plarg.gr") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open website:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOpenLicense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://creativecommons.org/licenses/by-nc/4.0/deed.el") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open license:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region Crop & Save Operations

    private void ToggleCropMode()
    {
        if (_isCropMode)
            ExitCropMode();
        else
            EnterCropMode();
    }

    private void EnterCropMode()
    {
        if (_currentData == null) return;

        GetOrientedDimensions(out double imgW, out double imgH);
        if (imgW <= 0 || imgH <= 0) return;

        _isCropMode = true;

        // Hide standard HUD and EXIF
        BottomHudBar.Visibility = Visibility.Collapsed;
        ExifSidebar.Visibility = Visibility.Collapsed;

        // Show Crop Canvas & Toolbar
        CropCanvas.Visibility = Visibility.Visible;
        CropToolbar.Visibility = Visibility.Visible;

        // Default crop box: 80% centered in image pixels
        double initW = Math.Round(imgW * 0.8);
        double initH = Math.Round(imgH * 0.8);
        double initX = Math.Round((imgW - initW) / 2.0);
        double initY = Math.Round((imgH - initH) / 2.0);

        _cropPixelRect = new Rect(initX, initY, initW, initH);
        UpdateCropScreenFromPixels();
    }

    private void ExitCropMode()
    {
        _isCropMode = false;
        _cropDragMode = CropDragMode.None;
        CropCanvas.ReleaseMouseCapture();
        CropCanvas.Visibility = Visibility.Collapsed;
        CropToolbar.Visibility = Visibility.Collapsed;
        BottomHudBar.Visibility = Visibility.Visible;
        RevealHud();
    }

    private void GetOrientedDimensions(out double w, out double h)
    {
        if (_currentData == null)
        {
            w = 0;
            h = 0;
            return;
        }

        if (_rotation == 90 || _rotation == 270)
        {
            w = _currentData.PixelHeight;
            h = _currentData.PixelWidth;
        }
        else
        {
            w = _currentData.PixelWidth;
            h = _currentData.PixelHeight;
        }
    }

    private Point ImageToScreen(Point imgPt)
    {
        GetOrientedDimensions(out double w, out double h);
        double sx = (imgPt.X - w / 2.0) * _scale + _centerX;
        double sy = (imgPt.Y - h / 2.0) * _scale + _centerY;
        return new Point(sx, sy);
    }

    private Point ScreenToImage(Point scrPt)
    {
        GetOrientedDimensions(out double w, out double h);
        double ix = (scrPt.X - _centerX) / _scale + w / 2.0;
        double iy = (scrPt.Y - _centerY) / _scale + h / 2.0;
        return new Point(ix, iy);
    }

    private void UpdateCropScreenFromPixels()
    {
        if (!_isCropMode || _currentData == null || _cropPixelRect.IsEmpty)
            return;

        GetOrientedDimensions(out double imgW, out double imgH);

        double px = Math.Clamp(_cropPixelRect.X, 0, Math.Max(0, imgW - 1));
        double py = Math.Clamp(_cropPixelRect.Y, 0, Math.Max(0, imgH - 1));
        double pw = Math.Clamp(_cropPixelRect.Width, 1, imgW - px);
        double ph = Math.Clamp(_cropPixelRect.Height, 1, imgH - py);
        _cropPixelRect = new Rect(px, py, pw, ph);

        Point p1 = ImageToScreen(new Point(px, py));
        Point p2 = ImageToScreen(new Point(px + pw, py + ph));

        double sx = Math.Min(p1.X, p2.X);
        double sy = Math.Min(p1.Y, p2.Y);
        double sw = Math.Abs(p2.X - p1.X);
        double sh = Math.Abs(p2.Y - p1.Y);

        _cropRectScreen = new Rect(sx, sy, sw, sh);
        UpdateCropUi();
    }

    private void UpdateCropPixelsFromScreen()
    {
        GetOrientedDimensions(out double imgW, out double imgH);

        Point p1 = ScreenToImage(new Point(_cropRectScreen.Left, _cropRectScreen.Top));
        Point p2 = ScreenToImage(new Point(_cropRectScreen.Right, _cropRectScreen.Bottom));

        double px = Math.Clamp(Math.Round(Math.Min(p1.X, p2.X)), 0, Math.Max(0, imgW - 1));
        double py = Math.Clamp(Math.Round(Math.Min(p1.Y, p2.Y)), 0, Math.Max(0, imgH - 1));
        double pw = Math.Clamp(Math.Round(Math.Abs(p2.X - p1.X)), 1, imgW - px);
        double ph = Math.Clamp(Math.Round(Math.Abs(p2.Y - p1.Y)), 1, imgH - py);

        _cropPixelRect = new Rect(px, py, pw, ph);
        UpdateCropUi();
    }

    private void UpdateCropUi()
    {
        if (!_isCropMode || _currentData == null) return;

        double viewportW = ViewportBorder.ActualWidth;
        double viewportH = ViewportBorder.ActualHeight;

        double x = _cropRectScreen.X;
        double y = _cropRectScreen.Y;
        double w = Math.Max(1, _cropRectScreen.Width);
        double h = Math.Max(1, _cropRectScreen.Height);

        // Position CropBox
        Canvas.SetLeft(CropBox, x);
        Canvas.SetTop(CropBox, y);
        CropBox.Width = w;
        CropBox.Height = h;

        // Position the 4 dark shades
        Canvas.SetLeft(CropShadeTop, 0);
        Canvas.SetTop(CropShadeTop, 0);
        CropShadeTop.Width = viewportW;
        CropShadeTop.Height = Math.Max(0, y);

        Canvas.SetLeft(CropShadeBottom, 0);
        Canvas.SetTop(CropShadeBottom, Math.Min(viewportH, y + h));
        CropShadeBottom.Width = viewportW;
        CropShadeBottom.Height = Math.Max(0, viewportH - (y + h));

        Canvas.SetLeft(CropShadeLeft, 0);
        Canvas.SetTop(CropShadeLeft, Math.Max(0, y));
        CropShadeLeft.Width = Math.Max(0, x);
        CropShadeLeft.Height = h;

        Canvas.SetLeft(CropShadeRight, Math.Min(viewportW, x + w));
        Canvas.SetTop(CropShadeRight, Math.Max(0, y));
        CropShadeRight.Width = Math.Max(0, viewportW - (x + w));
        CropShadeRight.Height = h;

        // Position 8 Handles
        double cx = x + w / 2.0;
        double cy = y + h / 2.0;

        Canvas.SetLeft(HandleNW, x - 5); Canvas.SetTop(HandleNW, y - 5);
        Canvas.SetLeft(HandleNE, x + w - 5); Canvas.SetTop(HandleNE, y - 5);
        Canvas.SetLeft(HandleSW, x - 5); Canvas.SetTop(HandleSW, y + h - 5);
        Canvas.SetLeft(HandleSE, x + w - 5); Canvas.SetTop(HandleSE, y + h - 5);

        Canvas.SetLeft(HandleN, cx - 5); Canvas.SetTop(HandleN, y - 5);
        Canvas.SetLeft(HandleS, cx - 5); Canvas.SetTop(HandleS, y + h - 5);
        Canvas.SetLeft(HandleW, x - 5); Canvas.SetTop(HandleW, cy - 5);
        Canvas.SetLeft(HandleE, x + w - 5); Canvas.SetTop(HandleE, cy - 5);

        // Update dimensions text
        TxtCropDimensions.Text = $"{(int)_cropPixelRect.Width} × {(int)_cropPixelRect.Height} px";
    }

    private CropDragMode GetCropHitTestMode(Point pt)
    {
        if (_cropRectScreen.IsEmpty) return CropDragMode.None;

        double x = _cropRectScreen.X;
        double y = _cropRectScreen.Y;
        double w = _cropRectScreen.Width;
        double h = _cropRectScreen.Height;
        double tol = 9.0;

        bool nearLeft = Math.Abs(pt.X - x) <= tol;
        bool nearRight = Math.Abs(pt.X - (x + w)) <= tol;
        bool nearTop = Math.Abs(pt.Y - y) <= tol;
        bool nearBottom = Math.Abs(pt.Y - (y + h)) <= tol;
        bool insideX = pt.X >= x - tol && pt.X <= x + w + tol;
        bool insideY = pt.Y >= y - tol && pt.Y <= y + h + tol;

        if (nearLeft && nearTop) return CropDragMode.ResizeNW;
        if (nearRight && nearTop) return CropDragMode.ResizeNE;
        if (nearLeft && nearBottom) return CropDragMode.ResizeSW;
        if (nearRight && nearBottom) return CropDragMode.ResizeSE;
        if (nearTop && insideX) return CropDragMode.ResizeN;
        if (nearBottom && insideX) return CropDragMode.ResizeS;
        if (nearLeft && insideY) return CropDragMode.ResizeW;
        if (nearRight && insideY) return CropDragMode.ResizeE;

        if (_cropRectScreen.Contains(pt)) return CropDragMode.Moving;

        return CropDragMode.None;
    }

    private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        Point pt = e.GetPosition(CropCanvas);
        _cropDragStartPoint = pt;
        _cropRectStart = _cropRectScreen;

        _cropDragMode = GetCropHitTestMode(pt);
        if (_cropDragMode == CropDragMode.None)
        {
            _cropDragMode = CropDragMode.Creating;
            _cropRectScreen = new Rect(pt.X, pt.Y, 1, 1);
            UpdateCropPixelsFromScreen();
        }

        CropCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        Point pt = e.GetPosition(CropCanvas);

        if (_cropDragMode == CropDragMode.None)
        {
            var mode = GetCropHitTestMode(pt);
            CropCanvas.Cursor = mode switch
            {
                CropDragMode.ResizeNW => Cursors.SizeNWSE,
                CropDragMode.ResizeSE => Cursors.SizeNWSE,
                CropDragMode.ResizeNE => Cursors.SizeNESW,
                CropDragMode.ResizeSW => Cursors.SizeNESW,
                CropDragMode.ResizeN => Cursors.SizeNS,
                CropDragMode.ResizeS => Cursors.SizeNS,
                CropDragMode.ResizeW => Cursors.SizeWE,
                CropDragMode.ResizeE => Cursors.SizeWE,
                CropDragMode.Moving => Cursors.SizeAll,
                _ => Cursors.Cross
            };
            return;
        }

        double deltaX = pt.X - _cropDragStartPoint.X;
        double deltaY = pt.Y - _cropDragStartPoint.Y;

        GetOrientedDimensions(out double imgW, out double imgH);
        Point scrTopLeft = ImageToScreen(new Point(0, 0));
        Point scrBottomRight = ImageToScreen(new Point(imgW, imgH));
        double minScrX = Math.Min(scrTopLeft.X, scrBottomRight.X);
        double maxScrX = Math.Max(scrTopLeft.X, scrBottomRight.X);
        double minScrY = Math.Min(scrTopLeft.Y, scrBottomRight.Y);
        double maxScrY = Math.Max(scrTopLeft.Y, scrBottomRight.Y);

        Rect newRect = _cropRectStart;

        switch (_cropDragMode)
        {
            case CropDragMode.Creating:
                double x1 = Math.Clamp(Math.Min(_cropDragStartPoint.X, pt.X), minScrX, maxScrX);
                double y1 = Math.Clamp(Math.Min(_cropDragStartPoint.Y, pt.Y), minScrY, maxScrY);
                double x2 = Math.Clamp(Math.Max(_cropDragStartPoint.X, pt.X), minScrX, maxScrX);
                double y2 = Math.Clamp(Math.Max(_cropDragStartPoint.Y, pt.Y), minScrY, maxScrY);
                newRect = new Rect(x1, y1, Math.Max(4, x2 - x1), Math.Max(4, y2 - y1));
                break;

            case CropDragMode.Moving:
                double nx = Math.Clamp(_cropRectStart.X + deltaX, minScrX, Math.Max(minScrX, maxScrX - _cropRectStart.Width));
                double ny = Math.Clamp(_cropRectStart.Y + deltaY, minScrY, Math.Max(minScrY, maxScrY - _cropRectStart.Height));
                newRect = new Rect(nx, ny, _cropRectStart.Width, _cropRectStart.Height);
                break;

            case CropDragMode.ResizeNW:
                double rxNW = Math.Clamp(_cropRectStart.X + deltaX, minScrX, _cropRectStart.Right - 8);
                double ryNW = Math.Clamp(_cropRectStart.Y + deltaY, minScrY, _cropRectStart.Bottom - 8);
                newRect = new Rect(rxNW, ryNW, _cropRectStart.Right - rxNW, _cropRectStart.Bottom - ryNW);
                break;

            case CropDragMode.ResizeNE:
                double rxNE = Math.Clamp(_cropRectStart.Right + deltaX, _cropRectStart.Left + 8, maxScrX);
                double ryNE = Math.Clamp(_cropRectStart.Y + deltaY, minScrY, _cropRectStart.Bottom - 8);
                newRect = new Rect(_cropRectStart.Left, ryNE, rxNE - _cropRectStart.Left, _cropRectStart.Bottom - ryNE);
                break;

            case CropDragMode.ResizeSW:
                double rxSW = Math.Clamp(_cropRectStart.X + deltaX, minScrX, _cropRectStart.Right - 8);
                double rySW = Math.Clamp(_cropRectStart.Bottom + deltaY, _cropRectStart.Top + 8, maxScrY);
                newRect = new Rect(rxSW, _cropRectStart.Top, _cropRectStart.Right - rxSW, rySW - _cropRectStart.Top);
                break;

            case CropDragMode.ResizeSE:
                double rxSE = Math.Clamp(_cropRectStart.Right + deltaX, _cropRectStart.Left + 8, maxScrX);
                double rySE = Math.Clamp(_cropRectStart.Bottom + deltaY, _cropRectStart.Top + 8, maxScrY);
                newRect = new Rect(_cropRectStart.Left, _cropRectStart.Top, rxSE - _cropRectStart.Left, rySE - _cropRectStart.Top);
                break;

            case CropDragMode.ResizeN:
                double ryN = Math.Clamp(_cropRectStart.Y + deltaY, minScrY, _cropRectStart.Bottom - 8);
                newRect = new Rect(_cropRectStart.Left, ryN, _cropRectStart.Width, _cropRectStart.Bottom - ryN);
                break;

            case CropDragMode.ResizeS:
                double ryS = Math.Clamp(_cropRectStart.Bottom + deltaY, _cropRectStart.Top + 8, maxScrY);
                newRect = new Rect(_cropRectStart.Left, _cropRectStart.Top, _cropRectStart.Width, ryS - _cropRectStart.Top);
                break;

            case CropDragMode.ResizeW:
                double rxW = Math.Clamp(_cropRectStart.X + deltaX, minScrX, _cropRectStart.Right - 8);
                newRect = new Rect(rxW, _cropRectStart.Top, _cropRectStart.Right - rxW, _cropRectStart.Height);
                break;

            case CropDragMode.ResizeE:
                double rxE = Math.Clamp(_cropRectStart.Right + deltaX, _cropRectStart.Left + 8, maxScrX);
                newRect = new Rect(_cropRectStart.Left, _cropRectStart.Top, rxE - _cropRectStart.Left, _cropRectStart.Height);
                break;
        }

        _cropRectScreen = newRect;
        UpdateCropPixelsFromScreen();
    }

    private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_cropDragMode != CropDragMode.None)
        {
            _cropDragMode = CropDragMode.None;
            CropCanvas.ReleaseMouseCapture();
        }
    }

    private async void BtnCropSaveAs_Click(object sender, RoutedEventArgs e)
    {
        var item = _folderNav.CurrentItem;
        if (item == null || _currentData == null || _cropPixelRect.IsEmpty) return;

        string currentPath = item.FilePath;
        string dir = Path.GetDirectoryName(currentPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string baseName = Path.GetFileNameWithoutExtension(currentPath);
        string currentExt = Path.GetExtension(currentPath).ToLowerInvariant();

        var sfd = new SaveFileDialog
        {
            Title = "Αποθήκευση Περικομμένης Εικόνας (Save Cropped Image)",
            InitialDirectory = dir,
            FileName = $"{baseName}_crop{currentExt}",
            Filter = "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                     "PNG Image (*.png)|*.png|" +
                     "WebP Image (*.webp)|*.webp|" +
                     "GIF Image (*.gif)|*.gif|" +
                     "Bitmap Image (*.bmp)|*.bmp|" +
                     "All Files (*.*)|*.*"
        };

        sfd.FilterIndex = currentExt switch
        {
            ".png" => 2,
            ".webp" => 3,
            ".gif" => 4,
            ".bmp" => 5,
            _ => 1
        };

        if (sfd.ShowDialog(this) == true)
        {
            string destPath = sfd.FileName;
            var rect = new Int32Rect((int)_cropPixelRect.X, (int)_cropPixelRect.Y, (int)_cropPixelRect.Width, (int)_cropPixelRect.Height);

            try
            {
                LoadingBadge.Visibility = Visibility.Visible;
                await ImageCropService.CropAndSaveAsync(currentPath, destPath, rect, _rotation, _flipH);
                ExitCropMode();

                string destDir = Path.GetDirectoryName(destPath) ?? "";
                if (string.Equals(dir, destDir, StringComparison.OrdinalIgnoreCase))
                {
                    _folderNav.LoadFolder(destDir, destPath);
                }
                else
                {
                    OpenSpecificFile(destPath);
                }

                ShowToast($"✓ Αποθηκεύτηκε: {Path.GetFileName(destPath)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα κατά την αποθήκευση:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void BtnCropOverwrite_Click(object sender, RoutedEventArgs e)
    {
        var item = _folderNav.CurrentItem;
        if (item == null || _currentData == null || _cropPixelRect.IsEmpty) return;

        string currentPath = item.FilePath;
        string fileName = Path.GetFileName(currentPath);

        var result = MessageBox.Show(
            $"Είστε βέβαιοι ότι θέλετε να αντικαταστήσετε το αρχικό αρχείο;\n\n{fileName}\n\nΗ ενέργεια αυτή θα αποθηκεύσει την περικομμένη εικόνα πάνω στο αρχικό αρχείο.",
            "Επιβεβαίωση Αντικατάστασης - myView",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var rect = new Int32Rect((int)_cropPixelRect.X, (int)_cropPixelRect.Y, (int)_cropPixelRect.Width, (int)_cropPixelRect.Height);

        try
        {
            LoadingBadge.Visibility = Visibility.Visible;
            await ImageCropService.CropAndSaveAsync(currentPath, currentPath, rect, _rotation, _flipH);

            _cache.Remove(currentPath);

            _rotation = 0;
            _flipH = false;

            ExitCropMode();

            await DisplayImageItemAsync(item);
            ShowToast("✓ Το αρχείο αντικαταστάθηκε επιτυχώς!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Σφάλμα κατά την αντικατάσταση:\n{ex.Message}", "myView", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnCropCancel_Click(object sender, RoutedEventArgs e)
    {
        ExitCropMode();
    }

    private void ShowToast(string message)
    {
        TxtToastMessage.Text = message;
        ToastBadge.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    #endregion

    #region Drag and Drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string first = files[0];
                if (Directory.Exists(first))
                {
                    OpenFolder(first);
                }
                else if (File.Exists(first))
                {
                    OpenSpecificFile(first);
                }
            }
        }
    }

    #endregion

    #region Helpers

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    #endregion
}