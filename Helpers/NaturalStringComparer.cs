using System.Runtime.InteropServices;

namespace myView.Helpers;

public class NaturalStringComparer : IComparer<string?>
{
    public static readonly NaturalStringComparer Instance = new();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string? psz1, string? psz2);

    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        try
        {
            return StrCmpLogicalW(x, y);
        }
        catch
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
