// DesktopPackage.cs
// The identity of the package the desktop mode runs from, when it is installed from the Microsoft Store (MSIX), and the folder of its data (ADR 088).

using System.Runtime.InteropServices;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The package of the process, if any (ADR 088). Installed from its MSIX package, the application has the identity of
/// the package: a new file under `%LOCALAPPDATA%` would go to a private folder of the package, while a file already there
/// (the data of the portable version) would be opened in place; so the folder of the product is then the folder of the
/// data of the package, never the one of the portable version. Started from its ZIP, the process has no package.
/// </summary>
public static class DesktopPackage
{
    private const int Success = 0;

    private const int InsufficientBuffer = 122;

    /// <summary>The family name of the package of the process (`Name_publisherhash`), or null without a package.</summary>
    public static string? FamilyName { get; } = ReadFamilyName();

    /// <summary>
    /// The default folder of the product: the folder named after the product under `%LOCALAPPDATA%` without a package; with
    /// one, the same name under the local folder of the data of the package, which its removal erases (Store policy 10.2.7).
    /// </summary>
    public static string DefaultFolderOf(string localApplicationData, string? familyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        return familyName is null
            ? Path.Combine(localApplicationData, Application.ProductIdentity.Name)
            : Path.Combine(localApplicationData, "Packages", familyName, "LocalState", Application.ProductIdentity.Name);
    }

    /// <summary>The name asked twice, its length first; any answer but success (no package: 15700) means no package.</summary>
    private static string? ReadFamilyName()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            return null;
        var length = 0;
        if (NativeMethods.GetCurrentPackageFamilyName(ref length, null) != InsufficientBuffer || length <= 0)
            return null;
        var name = new char[length];
        if (NativeMethods.GetCurrentPackageFamilyName(ref length, name) != Success)
            return null;
        var family = new string(name, 0, Math.Max(0, length - 1));
        return string.IsNullOrWhiteSpace(family) ? null : family;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetCurrentPackageFamilyName(ref int length, [Out] char[]? name);
    }
}
