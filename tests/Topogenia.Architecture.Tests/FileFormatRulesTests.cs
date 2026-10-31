// FileFormatRulesTests.cs
// Form rules of every text file: two-line header, UTF-8, no emoji, CRLF for batch files and LF elsewhere (prompt 10, spec 0.2, ADR 022).

using System.Text;
using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed partial class FileFormatRulesTests
{
    private static readonly string[] Folders =
        ["src", "tests", "web/src", "web/e2e", "web/scripts", "build", "docs", "samples", ".github"];

    private static readonly string[] HeaderExtensions =
    [
        ".cs", ".csproj", ".props", ".proj", ".ts", ".tsx", ".css", ".mjs", ".html", ".md", ".ps1", ".cmd", ".bat", ".yml", ".yaml", ".sql",
        ".editorconfig", ".gitignore", ".gitattributes",
    ];

    private static readonly string[] TextExtensions = [.. HeaderExtensions, ".json", ".pmap", ".svg", ".txt", ".xml", ".config", ".sln"];

    private static IEnumerable<string> Files(string[] extensions) =>
        RepositoryFiles.Enumerate(Folders, extensions)
            .Concat(RepositoryFiles.Shallow(["", "web"], extensions))
            .Where(file => !RepositoryFiles.IsGenerated(file))
            .Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"^\s*(//|/\*|<!--|#|::|REM)\s*(?<text>.*?)\s*(\*/|-->)?\s*$")]
    private static partial Regex CommentLine();

    private static string? CommentText(string line)
    {
        var match = CommentLine().Match(line.TrimStart('﻿'));
        return match.Success ? match.Groups["text"].Value : null;
    }

    /// <summary>First line: the file name; second line: a description of three words or more that does not repeat the name.</summary>
    private static bool HasHeader(IReadOnlyList<string> lines, string fileName)
    {
        var name = lines.Count > 0 ? CommentText(lines[0]) : null;
        var description = lines.Count > 1 ? CommentText(lines[1]) : null;
        return name == fileName
            && description is not null
            && description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3
            && !description.Contains(fileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_file_starts_with_its_name_then_a_description()
    {
        var offenders = Files(HeaderExtensions)
            .Where(file => !HasHeader(
                File.ReadLines(file).Where(line => !line.StartsWith("@echo off", StringComparison.OrdinalIgnoreCase)).Take(2).ToList(),
                Path.GetFileName(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_text_file_is_valid_utf8_without_byte_order_mark()
    {
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var offenders = new List<string>();
        foreach (var file in Files(TextExtensions))
        {
            var bytes = File.ReadAllBytes(file);
            try
            {
                strict.GetString(bytes);
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    offenders.Add(RepositoryFiles.Relative(file) + " (BOM)");
            }
            catch (DecoderFallbackException)
            {
                offenders.Add(RepositoryFiles.Relative(file));
            }
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void No_emoji_anywhere()
    {
        var offenders = Files(TextExtensions)
            .Where(file => SourceDetectors.ContainsEmoji(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Batch_files_use_crlf_line_endings_and_git_keeps_them()
    {
        var offenders = Files([".bat", ".cmd"])
            .Where(file => Regex.IsMatch(File.ReadAllText(file), "(?<!\r)\n"))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
        var attributes = File.ReadAllText(Path.Combine(RepositoryFiles.Root, ".gitattributes"));
        Assert.Contains("*.bat text eol=crlf", attributes, StringComparison.Ordinal);
        Assert.Contains("*.cmd text eol=crlf", attributes, StringComparison.Ordinal);
    }

    /// <summary>Every other text file uses LF (the lock files are written by NuGet itself and keep its line endings).</summary>
    [Fact]
    public void Other_text_files_use_lf_line_endings()
    {
        var offenders = Files(TextExtensions)
            .Where(file => Path.GetExtension(file) is not (".bat" or ".cmd") && Path.GetFileName(file) != "packages.lock.json")
            .Where(file => File.ReadAllText(file).Contains('\r', StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("// Program.cs", "// Entry point of the host", true)]
    [InlineData("// Program.cs", "// x", false)]
    [InlineData("// Program.cs", "// Program.cs again here", false)]
    [InlineData("// Other.cs", "// Entry point of the host", false)]
    [InlineData("<!-- Program.cs -->", "<!-- Single page of the interface -->", true)]
    [InlineData("/* Program.cs */", "/* Base element styles here */", true)]
    public void The_header_rule_accepts_a_real_description_only(string first, string second, bool expected) =>
        Assert.Equal(expected, HasHeader([first, second], "Program.cs"));
}
