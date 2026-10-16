using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using myView.Models;

namespace myView.Services;

public static class MetadataService
{
    public static async Task<ImageExifData> ReadMetadataAsync(string filePath)
    {
        return await Task.Run(() => ReadMetadataInternal(filePath));
    }

    private static ImageExifData ReadMetadataInternal(string filePath)
    {
        var data = new ImageExifData();

        if (!File.Exists(filePath))
            return data;

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();

            if (ifd0 != null || subIfd != null || gps != null)
            {
                data.HasExif = true;
            }

            // IFD0: Camera make, model, software, artist
            if (ifd0 != null)
            {
                data.CameraMake = ifd0.GetDescription(ExifDirectoryBase.TagMake);
                data.CameraModel = ifd0.GetDescription(ExifDirectoryBase.TagModel);
                data.Software = ifd0.GetDescription(ExifDirectoryBase.TagSoftware);
                data.Artist = ifd0.GetDescription(ExifDirectoryBase.TagArtist);
                data.Copyright = ifd0.GetDescription(ExifDirectoryBase.TagCopyright);
            }

            // SubIFD: Exposure settings, lens, shot date
            if (subIfd != null)
            {
                data.ExposureTime = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                data.FNumber = subIfd.GetDescription(ExifDirectoryBase.TagFNumber);
                data.IsoRating = subIfd.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
                data.FocalLength = subIfd.GetDescription(ExifDirectoryBase.TagFocalLength);
                data.FocalLength35mm = subIfd.GetDescription(ExifDirectoryBase.Tag35MMFilmEquivFocalLength);
                data.ExposureBias = subIfd.GetDescription(ExifDirectoryBase.TagExposureBias);
                data.ExposureProgram = subIfd.GetDescription(ExifDirectoryBase.TagExposureProgram);
                data.MeteringMode = subIfd.GetDescription(ExifDirectoryBase.TagMeteringMode);
                data.Flash = subIfd.GetDescription(ExifDirectoryBase.TagFlash);
                data.WhiteBalance = subIfd.GetDescription(ExifDirectoryBase.TagWhiteBalanceMode);
                data.DateTaken = subIfd.GetDescription(ExifDirectoryBase.TagDateTimeOriginal)
                                ?? subIfd.GetDescription(ExifDirectoryBase.TagDateTimeDigitized);
                data.ColorSpace = subIfd.GetDescription(ExifDirectoryBase.TagColorSpace);
                data.LensModel = subIfd.GetDescription(ExifDirectoryBase.TagLensModel);
            }

            // Look for lens info in other directories if missing
            if (string.IsNullOrWhiteSpace(data.LensModel))
            {
                foreach (var dir in directories)
                {
                    var lensTag = dir.Tags.FirstOrDefault(t => t.Name.Contains("Lens", StringComparison.OrdinalIgnoreCase) && !t.Name.Contains("Profile", StringComparison.OrdinalIgnoreCase));
                    if (lensTag != null && !string.IsNullOrWhiteSpace(lensTag.Description))
                    {
                        data.LensModel = lensTag.Description;
                        break;
                    }
                }
            }

            // GPS
            if (gps != null)
            {
                var locationNullable = gps.GetGeoLocation();
                if (locationNullable.HasValue)
                {
                    var loc = locationNullable.Value;
                    if (!loc.IsZero)
                    {
                        data.HasGps = true;
                        data.Latitude = loc.Latitude;
                        data.Longitude = loc.Longitude;
                        data.GpsCoordinatesFormatted = $"{loc.ToDmsString()} ({loc.Latitude:F5}, {loc.Longitude:F5})";
                    }
                }

                string? altDesc = gps.GetDescription(GpsDirectory.TagAltitude);
                if (!string.IsNullOrWhiteSpace(altDesc))
                {
                    data.AltitudeMeters = double.TryParse(altDesc.Replace("metres", "").Replace("m", "").Trim(), out var alt) ? alt : null;
                }
            }

            // Collect all directory groups and tags
            foreach (var dir in directories)
            {
                if (dir.Tags.Count == 0) continue;

                var group = new ExifDirectoryGroup
                {
                    DirectoryName = dir.Name
                };

                foreach (var tag in dir.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag.Description))
                    {
                        group.Tags.Add(new ExifTagEntry
                        {
                            TagName = tag.Name,
                            TagValue = tag.Description
                        });
                    }
                }

                if (group.Tags.Count > 0)
                {
                    data.DirectoryGroups.Add(group);
                }
            }
        }
        catch
        {
            // If reading metadata fails, return whatever was captured without crashing
        }

        return data;
    }
}
