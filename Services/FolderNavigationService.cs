using System.IO;
using myView.Helpers;
using myView.Models;

namespace myView.Services;

public class FolderNavigationService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard raster formats
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".bmp", ".dib",
        ".gif",
        ".webp",
        ".tif", ".tiff",
        ".ico",

        // Photoshop
        ".psd", ".psb",

        // Vector / Inkscape
        ".svg", ".svgz",

        // RAW camera formats
        ".cr2", ".cr3",          // Canon
        ".nef", ".nrw",          // Nikon
        ".arw", ".srf", ".sr2",  // Sony
        ".dng",                  // Adobe / Leica / Generic RAW
        ".orf",                  // Olympus
        ".rw2",                  // Panasonic Lumix
        ".pef", ".ptx",          // Pentax
        ".raf",                  // Fujifilm
        ".x3f",                  // Sigma
        ".srw"                   // Samsung
    };

    private readonly List<ImageItem> _items = new();
    private int _currentIndex = -1;
    private SortMode _sortMode = SortMode.NameAscending;

    public IReadOnlyList<ImageItem> Items => _items;
    public int CurrentIndex => _currentIndex;
    public int TotalCount => _items.Count;
    public SortMode CurrentSortMode => _sortMode;
    public ImageItem? CurrentItem => (_currentIndex >= 0 && _currentIndex < _items.Count) ? _items[_currentIndex] : null;

    public event Action? SelectionChanged;
    public event Action? FolderReloaded;

    public static bool IsSupported(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    public void LoadFolder(string folderPath, string? targetFilePath = null)
    {
        _items.Clear();
        _currentIndex = -1;

        if (!Directory.Exists(folderPath))
        {
            FolderReloaded?.Invoke();
            SelectionChanged?.Invoke();
            return;
        }

        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            var files = dirInfo.EnumerateFiles()
                               .Where(f => SupportedExtensions.Contains(f.Extension))
                               .Select(ImageItem.FromFileInfo)
                               .ToList();

            _items.AddRange(files);
            ApplySortInternal();

            if (!string.IsNullOrEmpty(targetFilePath))
            {
                int index = _items.FindIndex(i => string.Equals(i.FilePath, targetFilePath, StringComparison.OrdinalIgnoreCase));
                _currentIndex = index >= 0 ? index : (_items.Count > 0 ? 0 : -1);
            }
            else
            {
                _currentIndex = _items.Count > 0 ? 0 : -1;
            }
        }
        catch
        {
            _items.Clear();
            _currentIndex = -1;
        }

        FolderReloaded?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void SetSortMode(SortMode newMode)
    {
        if (_sortMode == newMode && _items.Count > 0) return;

        string? currentPath = CurrentItem?.FilePath;
        _sortMode = newMode;
        ApplySortInternal();

        if (currentPath != null)
        {
            int newIndex = _items.FindIndex(i => string.Equals(i.FilePath, currentPath, StringComparison.OrdinalIgnoreCase));
            _currentIndex = newIndex >= 0 ? newIndex : 0;
        }

        FolderReloaded?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void CycleSortMode()
    {
        var nextMode = _sortMode switch
        {
            SortMode.NameAscending => SortMode.NameDescending,
            SortMode.NameDescending => SortMode.DateDescending,
            SortMode.DateDescending => SortMode.DateAscending,
            SortMode.DateAscending => SortMode.SizeDescending,
            SortMode.SizeDescending => SortMode.SizeAscending,
            _ => SortMode.NameAscending
        };
        SetSortMode(nextMode);
    }

    private void ApplySortInternal()
    {
        switch (_sortMode)
        {
            case SortMode.NameAscending:
                _items.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.FileName, b.FileName));
                break;
            case SortMode.NameDescending:
                _items.Sort((a, b) => NaturalStringComparer.Instance.Compare(b.FileName, a.FileName));
                break;
            case SortMode.DateDescending:
                _items.Sort((a, b) => b.DateModified.CompareTo(a.DateModified));
                break;
            case SortMode.DateAscending:
                _items.Sort((a, b) => a.DateModified.CompareTo(b.DateModified));
                break;
            case SortMode.SizeDescending:
                _items.Sort((a, b) => b.FileSizeBytes.CompareTo(a.FileSizeBytes));
                break;
            case SortMode.SizeAscending:
                _items.Sort((a, b) => a.FileSizeBytes.CompareTo(b.FileSizeBytes));
                break;
        }
    }

    public ImageItem? Next()
    {
        if (_items.Count == 0) return null;
        if (_currentIndex < _items.Count - 1)
        {
            _currentIndex++;
            SelectionChanged?.Invoke();
        }
        else if (_items.Count > 1) // wrap around
        {
            _currentIndex = 0;
            SelectionChanged?.Invoke();
        }
        return CurrentItem;
    }

    public ImageItem? Previous()
    {
        if (_items.Count == 0) return null;
        if (_currentIndex > 0)
        {
            _currentIndex--;
            SelectionChanged?.Invoke();
        }
        else if (_items.Count > 1) // wrap around
        {
            _currentIndex = _items.Count - 1;
            SelectionChanged?.Invoke();
        }
        return CurrentItem;
    }

    public ImageItem? First()
    {
        if (_items.Count == 0) return null;
        if (_currentIndex != 0)
        {
            _currentIndex = 0;
            SelectionChanged?.Invoke();
        }
        return CurrentItem;
    }

    public ImageItem? Last()
    {
        if (_items.Count == 0) return null;
        int lastIndex = _items.Count - 1;
        if (_currentIndex != lastIndex)
        {
            _currentIndex = lastIndex;
            SelectionChanged?.Invoke();
        }
        return CurrentItem;
    }

    public ImageItem? GetRelative(int offset)
    {
        if (_items.Count == 0 || _currentIndex < 0) return null;
        int target = _currentIndex + offset;
        if (target >= 0 && target < _items.Count)
        {
            return _items[target];
        }
        return null;
    }

    public void RemoveCurrent()
    {
        if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
        _items.RemoveAt(_currentIndex);
        if (_currentIndex >= _items.Count)
        {
            _currentIndex = _items.Count - 1;
        }
        FolderReloaded?.Invoke();
        SelectionChanged?.Invoke();
    }
}
