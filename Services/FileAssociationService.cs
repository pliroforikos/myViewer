using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace myView.Services;

public static class FileAssociationService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static readonly string[] SupportedExtensions = new[]
    {
        // Standard raster
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".bmp", ".dib",
        ".gif",
        ".webp",
        ".tif", ".tiff",
        ".ico",
        // Vectors
        ".svg", ".svgz",
        // Adobe Photoshop
        ".psd", ".psb",
        // Camera RAW
        ".cr2", ".cr3",
        ".nef", ".nrw",
        ".arw", ".srf", ".sr2",
        ".dng",
        ".orf",
        ".rw2",
        ".pef", ".ptx",
        ".raf",
        ".x3f",
        ".srw"
    };

    public static string GetExecutablePath()
    {
        string? procPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
        {
            string fileName = Path.GetFileName(procPath);
            if (!fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return procPath;
            }
        }

        // Fallback for development run
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate = Path.Combine(appDir, "myView.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return procPath ?? candidate;
    }

    /// <summary>
    /// Checks if myView is registered in HKCU Capabilities and RegisteredApplications.
    /// </summary>
    public static bool IsRegistered()
    {
        try
        {
            using var regAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");
            if (regAppsKey?.GetValue("myView") != null)
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Registers myView in HKCU (per-user, no admin rights required) as an official file handler
    /// and capabilities provider for all supported image formats.
    /// </summary>
    public static bool RegisterFileAssociations(out string message)
    {
        try
        {
            string exePath = GetExecutablePath();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                message = "Could not locate myView executable path.";
                return false;
            }

            string progId = "myView.Image";
            string openCommand = $"\"{exePath}\" \"%1\"";
            string iconPath = $"\"{exePath}\",0";

            // 1. Register ProgID in HKCU\Software\Classes\myView.Image
            using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
            {
                progIdKey.SetValue("", "myView Image File");
                progIdKey.SetValue("FriendlyTypeName", "myView Image File");

                using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", iconPath);

                using var shellKey = progIdKey.CreateSubKey(@"shell\open\command");
                shellKey.SetValue("", openCommand);

                using var openKey = progIdKey.CreateSubKey(@"shell\open");
                openKey.SetValue("FriendlyAppName", "myView");
            }

            // 2. Register Application in HKCU\Software\Classes\Applications\myView.exe
            using (var appKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\myView.exe"))
            {
                appKey.SetValue("FriendlyAppName", "myView");

                using var iconKey = appKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", iconPath);

                using var shellKey = appKey.CreateSubKey(@"shell\open\command");
                shellKey.SetValue("", openCommand);

                using var supportedKey = appKey.CreateSubKey("SupportedTypes");
                foreach (var ext in SupportedExtensions)
                {
                    supportedKey.SetValue(ext, "");
                }
            }

            // 3. Register Capabilities for Windows 10/11 Default Apps Settings
            using (var capKey = Registry.CurrentUser.CreateSubKey(@"Software\myView\Capabilities"))
            {
                capKey.SetValue("ApplicationDescription", "myView - Ultra-Fast Image & Vector Viewer for Windows");
                capKey.SetValue("ApplicationName", "myView");
                capKey.SetValue("ApplicationIcon", iconPath);

                using var fileAssocKey = capKey.CreateSubKey("FileAssociations");
                foreach (var ext in SupportedExtensions)
                {
                    fileAssocKey.SetValue(ext, progId);
                }
            }

            // 4. Register in RegisteredApplications
            using (var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                regAppsKey.SetValue("myView", @"Software\myView\Capabilities");
            }

            // 5. Add to OpenWithProgids for each extension so it appears in Explorer "Open with"
            foreach (var ext in SupportedExtensions)
            {
                try
                {
                    using var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids");
                    openWithKey.SetValue(progId, string.Empty);
                }
                catch { }
            }

            // 6. Notify Windows Shell of association change
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            message = "myView registered successfully.";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Opens the Windows Default Apps settings page.
    /// </summary>
    public static void OpenWindowsDefaultAppsSettings()
    {
        try
        {
            // Try Windows 11 target with query string first
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch { }
    }
}
