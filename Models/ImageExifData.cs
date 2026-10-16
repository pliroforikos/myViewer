namespace myView.Models;

public class ImageExifData
{
    public bool HasExif { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public string? ExposureTime { get; set; }
    public string? FNumber { get; set; }
    public string? IsoRating { get; set; }
    public string? FocalLength { get; set; }
    public string? FocalLength35mm { get; set; }
    public string? ExposureBias { get; set; }
    public string? ExposureProgram { get; set; }
    public string? MeteringMode { get; set; }
    public string? Flash { get; set; }
    public string? WhiteBalance { get; set; }
    public string? DateTaken { get; set; }
    public string? Software { get; set; }
    public string? ColorSpace { get; set; }
    public string? Copyright { get; set; }
    public string? Artist { get; set; }

    // GPS
    public bool HasGps { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public string? GpsCoordinatesFormatted { get; set; }
    public string? GoogleMapsUrl => HasGps && Latitude.HasValue && Longitude.HasValue
        ? $"https://www.google.com/maps?q={Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : null;

    // All tags grouped by directory
    public List<ExifDirectoryGroup> DirectoryGroups { get; } = new();

    public string SummaryText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(CameraModel)) parts.Add(CameraModel);
            if (!string.IsNullOrWhiteSpace(LensModel)) parts.Add(LensModel);
            if (!string.IsNullOrWhiteSpace(FocalLength)) parts.Add(FocalLength);
            if (!string.IsNullOrWhiteSpace(FNumber)) parts.Add(FNumber);
            if (!string.IsNullOrWhiteSpace(ExposureTime)) parts.Add(ExposureTime);
            if (!string.IsNullOrWhiteSpace(IsoRating)) parts.Add(IsoRating);
            return parts.Count > 0 ? string.Join(" • ", parts) : "No camera EXIF metadata";
        }
    }
}

public class ExifDirectoryGroup
{
    public required string DirectoryName { get; init; }
    public List<ExifTagEntry> Tags { get; } = new();
}

public class ExifTagEntry
{
    public required string TagName { get; init; }
    public required string TagValue { get; init; }
}
