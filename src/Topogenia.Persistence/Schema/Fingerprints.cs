// Fingerprints.cs
// SHA-256 fingerprints of the migration sources and of the schema described by the catalogue (spec 4.4, 1.x `MiseAJourBase`).

using System.Security.Cryptography;
using System.Text;

namespace Topogenia.Persistence.Schema;

/// <summary>Fingerprints in lower-case hexadecimal, computed on texts with LF line ends and no byte order mark.</summary>
public static class Fingerprints
{
    /// <summary>Length of a fingerprint: 64 hexadecimal characters.</summary>
    public const int Length = 64;

    /// <summary>The fingerprint of a text, its line ends normalised to LF first (the 1.x normalisation).</summary>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalised = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }

    /// <summary>The fingerprint of a set of lines, whatever their order (ordinal sort first).</summary>
    public static string OfLines(IEnumerable<string> lines) => Of(string.Join('\n', lines.Order(StringComparer.Ordinal)));
}
