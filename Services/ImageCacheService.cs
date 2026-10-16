using System.Collections.Concurrent;
using myView.Models;

namespace myView.Services;

public class ImageCacheService
{
    private readonly int _maxCapacity;
    private readonly ConcurrentDictionary<string, LoadedImageData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lruList = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _prefetchCts;

    public ImageCacheService(int maxCapacity = 10)
    {
        _maxCapacity = maxCapacity;
    }

    public LoadedImageData? Get(string filePath)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(filePath, out var data))
            {
                _lruList.Remove(filePath);
                _lruList.AddFirst(filePath);
                return data;
            }
        }
        return null;
    }

    public void Put(string filePath, LoadedImageData data)
    {
        lock (_lock)
        {
            if (_cache.ContainsKey(filePath))
            {
                _cache[filePath] = data;
                _lruList.Remove(filePath);
                _lruList.AddFirst(filePath);
                return;
            }

            if (_cache.Count >= _maxCapacity)
            {
                var oldest = _lruList.Last?.Value;
                if (oldest != null)
                {
                    _lruList.RemoveLast();
                    _cache.TryRemove(oldest, out _);
                }
            }

            _cache[filePath] = data;
            _lruList.AddFirst(filePath);
        }
    }

    public void Prefetch(IEnumerable<string> filePaths)
    {
        _prefetchCts?.Cancel();
        _prefetchCts?.Dispose();
        _prefetchCts = new CancellationTokenSource();
        var token = _prefetchCts.Token;

        var pathsToLoad = filePaths.Where(p => !string.IsNullOrEmpty(p) && !_cache.ContainsKey(p)).ToList();
        if (pathsToLoad.Count == 0) return;

        Task.Run(async () =>
        {
            foreach (var path in pathsToLoad)
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    var data = await ImageLoader.LoadImageAsync(path, token);
                    Put(path, data);
                }
                catch
                {
                    // Ignore prefetch errors
                }
            }
        }, token);
    }

    public void Clear()
    {
        _prefetchCts?.Cancel();
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }
}
