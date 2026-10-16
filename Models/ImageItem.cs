using System.IO;

namespace myView.Models;

public class ImageItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string DirectoryPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime DateModified { get; init; }
    public DateTime DateCreated { get; init; }
    public string Extension { get; init; } = string.Empty;

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            if (FileSizeBytes < 1024 * 1024 * 1024)
                return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
            return $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public static ImageItem FromFileInfo(FileInfo fi)
    {
        return new ImageItem
        {
            FilePath = fi.FullName,
            FileName = fi.Name,
            DirectoryPath = fi.DirectoryName ?? string.Empty,
            FileSizeBytes = fi.Length,
            DateModified = fi.LastWriteTime,
            DateCreated = fi.CreationTime,
            Extension = fi.Extension.ToLowerInvariant()
        };
    }
}
