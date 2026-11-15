// FileIdentity.cs
// The identity of a file on its volume and its number of names, read by its handle: two paths of one file, or a file with a hard link (security MAJ-2, m2).

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>A file as Windows knows it: its volume, its index on the volume, and how many names (hard links) it has.</summary>
internal readonly record struct FileIdentity(uint Volume, ulong Index, uint Links)
{
    /// <summary>
    /// The identity of the file at <paramref name="path"/>, read by `GetFileInformationByHandle` on a handle opened for
    /// reading with every sharing (the file may be open elsewhere); null when the path is no file that opens. Two paths
    /// are one file when their identities are equal, whatever their text (`\\?\`, `\\.\`, short names, a hard link).
    /// </summary>
    public static FileIdentity? Of(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return NativeMethods.GetFileInformationByHandle(handle, out var information)
                ? new FileIdentity(information.VolumeSerialNumber, ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow, information.NumberOfLinks)
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The identity of the file without its count of names: the same file under two paths.</summary>
    public bool IsSameFileAs(FileIdentity other) => Volume == other.Volume && Index == other.Index;

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);
    }

    /// <summary>`BY_HANDLE_FILE_INFORMATION` of Windows; the three times are two 32-bit halves each.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
