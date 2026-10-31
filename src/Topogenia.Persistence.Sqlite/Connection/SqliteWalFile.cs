// SqliteWalFile.cs
// Reads, without SQLite, the stamp in the header of a data file and the stamps of the first page that its write-ahead log holds (data B1).

using System.Buffers.Binary;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>The frames of a write-ahead log that SQLite would read, and the stamps of the first page among them.</summary>
/// <param name="HasFrames">True when the log holds at least one committed frame that SQLite would read with the file.</param>
/// <param name="Stamps">The stamp (`user_version`) of each committed frame of the first page, in the order of the log.</param>
internal sealed record WalStamps(bool HasFrames, IReadOnlyList<int> Stamps);

/// <summary>
/// The formats of SQLite (`fileformat2`, sections 1.3 and 4): the header of the first page of a data file holds
/// `user_version` at offset 60 (4 bytes, big-endian). A write-ahead log is a header of 32 bytes (magic, version, page
/// size, checkpoint number, two salts, two checksums) then frames of a header of 24 bytes (page number, size of the
/// database after a commit or 0, the two salts, two checksums) and a page. SQLite reads a frame only when its salts are
/// those of the header and its checksum, cumulated from the header, holds; and only up to the last commit frame among
/// them. The same rules are applied here, from the files alone: nothing is opened by SQLite, nothing is written.
/// </summary>
internal static class SqliteWalFile
{
    private const int HeaderLength = 32;
    private const int FrameHeaderLength = 24;
    private const int StampOffset = 60;
    private const uint MagicLittleEndian = 0x377F0682;
    private const uint MagicBigEndian = 0x377F0683;

    /// <summary>The stamp of a data file as written in the file itself (its log aside); 0 for a file too short to hold one.</summary>
    public static int StampOf(string dataFile)
    {
        Span<byte> header = stackalloc byte[StampOffset + 4];
        using var file = OpenShared(dataFile);
        return file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
            ? 0
            : BinaryPrimitives.ReadInt32BigEndian(header[StampOffset..]);
    }

    /// <summary>The committed frames of the log and their stamps of the first page; no frame when the log is absent or empty.</summary>
    public static WalStamps Read(string walFile)
    {
        if (!File.Exists(walFile))
            return new WalStamps(false, []);
        using var file = OpenShared(walFile);
        var header = new byte[HeaderLength];
        if (file.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false) < HeaderLength)
            return new WalStamps(false, []);
        var magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (magic is not (MagicLittleEndian or MagicBigEndian))
            return new WalStamps(false, []);
        var bigEndian = magic == MagicBigEndian;
        var pageSize = PageSize(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8)));
        var (s1, s2) = Checksum(bigEndian, header.AsSpan(0, 24), 0, 0);
        if (pageSize == 0 || s1 != BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(24)) || s2 != BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(28)))
            return new WalStamps(false, []);
        return ReadFrames(file, header, pageSize, bigEndian, (s1, s2));
    }

    private static WalStamps ReadFrames(FileStream file, byte[] header, int pageSize, bool bigEndian, (uint S1, uint S2) checksum)
    {
        var frame = new byte[FrameHeaderLength + pageSize];
        var committed = new List<int>();
        var pending = new List<int>();
        var hasFrames = false;
        var (s1, s2) = checksum;
        while (file.ReadAtLeast(frame, frame.Length, throwOnEndOfStream: false) == frame.Length)
        {
            if (!frame.AsSpan(8, 8).SequenceEqual(header.AsSpan(16, 8)))
                break;
            (s1, s2) = Checksum(bigEndian, frame.AsSpan(0, 8), s1, s2);
            (s1, s2) = Checksum(bigEndian, frame.AsSpan(FrameHeaderLength), s1, s2);
            if (s1 != BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(16)) || s2 != BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(20)))
                break;
            if (BinaryPrimitives.ReadUInt32BigEndian(frame) == 1)
                pending.Add(BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(FrameHeaderLength + StampOffset)));
            if (BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(4)) != 0)
            {
                // A commit frame: what came before it is read by SQLite.
                hasFrames = true;
                committed.AddRange(pending);
                pending.Clear();
            }
        }
        return new WalStamps(hasFrames, committed);
    }

    /// <summary>The checksum of SQLite over 8-byte steps, the words in the order the magic of the log says.</summary>
    private static (uint, uint) Checksum(bool bigEndian, ReadOnlySpan<byte> bytes, uint s1, uint s2)
    {
        for (var i = 0; i + 8 <= bytes.Length; i += 8)
        {
            var x0 = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes[i..]) : BinaryPrimitives.ReadUInt32LittleEndian(bytes[i..]);
            var x1 = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes[(i + 4)..]) : BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i + 4)..]);
            unchecked
            {
                s1 += x0 + s2;
                s2 += x1 + s1;
            }
        }
        return (s1, s2);
    }

    /// <summary>The page size of the header of a log: a power of two from 512 to 65536 (1 stands for 65536); 0 otherwise.</summary>
    private static int PageSize(uint value)
    {
        var size = value == 1 ? 65536u : value;
        return size is >= 512 and <= 65536 && (size & (size - 1)) == 0 ? (int)size : 0;
    }

    /// <summary>Opens a file for reading while SQLite holds it open (read, write and delete shared).</summary>
    private static FileStream OpenShared(string file) =>
        new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
