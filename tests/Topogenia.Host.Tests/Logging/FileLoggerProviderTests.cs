// FileLoggerProviderTests.cs
// The daily file log of the host: file of the day, next day, 30-day purge, concurrent writing, exceptions by type, level, failures.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Topogenia.Application;
using Topogenia.Host.Logging;

namespace Topogenia.Host.Tests.Logging;

public sealed class FileLoggerProviderTests : IDisposable
{
    private const string Category = "Sample.Category";
    private const string Secret = @"C:\Data\secret.db";
    private static readonly DateTime Day = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("logs-");
    private readonly SettableClock clock = new(Day.AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(987));

    private string Folder => Path.Combine(root.FullName, "logs");

    public void Dispose()
    {
        foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
            file.Attributes = FileAttributes.Normal;
        root.Delete(recursive: true);
    }

    [Fact]
    public void An_entry_is_written_to_the_file_of_its_utc_day_named_after_the_product_in_lower_case()
    {
        using (var services = Start())
            Logger(services).LogInformation("Project {Id} opened", 42);

        var name = ProductIdentity.Name.ToLowerInvariant() + "-20261024.log";
        Assert.Equal([name], Directory.GetFiles(Folder).Select(Path.GetFileName));
        Assert.Equal(["2026-10-24T23:59:59.987Z Information Sample.Category Project 42 opened"], Lines(name));
    }

    [Fact]
    public void The_first_entry_of_the_next_utc_day_goes_to_a_new_file_and_the_previous_file_is_kept()
    {
        using (var services = Start())
        {
            var logger = Logger(services);
            logger.LogInformation("Before midnight");
            clock.UtcNow = Day.AddDays(1);
            logger.LogWarning("After midnight");
        }

        Assert.Equal(["2026-10-24T23:59:59.987Z Information Sample.Category Before midnight"], Lines(FileLoggerProvider.FileNameOf(Day)));
        Assert.Equal(["2026-10-25T00:00:00.000Z Warning Sample.Category After midnight"], Lines(FileLoggerProvider.FileNameOf(Day.AddDays(1))));
    }

    [Fact]
    public void The_start_deletes_the_files_of_the_provider_older_than_30_days_and_leaves_every_other_file()
    {
        Directory.CreateDirectory(Folder);
        var kept = Touch(FileLoggerProvider.FileNameOf(Day.AddDays(-29)));
        var old = Touch(FileLoggerProvider.FileNameOf(Day.AddDays(-31)));
        var foreign = ForeignFiles();

        using (Start())
        {
            Assert.True(File.Exists(kept));
            Assert.False(File.Exists(old));
            Assert.All(foreign, file => Assert.True(File.Exists(file), file));
        }
    }

    [Fact]
    public void The_change_of_day_deletes_the_files_of_the_provider_that_became_older_than_30_days()
    {
        Directory.CreateDirectory(Folder);
        var ageing = Touch(FileLoggerProvider.FileNameOf(Day.AddDays(-30)));
        var kept = Touch(FileLoggerProvider.FileNameOf(Day.AddDays(-28)));
        var foreign = ForeignFiles();

        using var services = Start();
        Assert.True(File.Exists(ageing));
        clock.UtcNow = Day.AddDays(1).AddHours(8);
        Logger(services).LogInformation("Next day");

        Assert.False(File.Exists(ageing));
        Assert.True(File.Exists(kept));
        Assert.All(foreign, file => Assert.True(File.Exists(file), file));
    }

    [Fact]
    public async Task Entries_written_by_many_threads_at_once_are_all_written_whole_one_per_line()
    {
        const int threads = 8;
        const int entries = 250;
        var padding = new string('x', 300);
        using (var services = Start())
        {
            var logger = Logger(services);
            using var start = new Barrier(threads);
            await Task.WhenAll(Enumerable.Range(0, threads).Select(thread => Task.Run(() =>
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                for (var entry = 0; entry < entries; entry++)
                    logger.LogInformation("Thread {Thread} entry {Entry} {Padding}", thread, entry, padding);
            }, TestContext.Current.CancellationToken)));
        }

        var lines = Lines(FileLoggerProvider.FileNameOf(Day));
        Assert.Equal(threads * entries, lines.Count);
        var pattern = new Regex(@"^2026-10-24T23:59:59\.987Z Information Sample\.Category Thread (\d+) entry (\d+) " + padding + "$");
        Assert.All(lines, line => Assert.Matches(pattern, line));
        Assert.Equal(threads * entries, lines.Select(line => pattern.Match(line)).Select(match => match.Groups[1].Value + "/" + match.Groups[2].Value).Distinct().Count());
    }

    [Fact]
    public void An_exception_is_written_by_its_type_and_hresult_only_and_its_inner_exceptions_one_per_line()
    {
        var failure = Thrown(new InvalidOperationException("Cannot open " + Secret, new IOException(Secret + " is locked by an account")));
        using (var services = Start())
            Logger(services).LogError(failure, "Start refused {Code}", "BASE_INACCESSIBLE");

        var text = File.ReadAllText(Path.Combine(Folder, FileLoggerProvider.FileNameOf(Day)));
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cannot open", text, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Thrown), text, StringComparison.Ordinal);
        Assert.Equal(
        [
            "2026-10-24T23:59:59.987Z Error Sample.Category Start refused BASE_INACCESSIBLE",
            "  exception System.InvalidOperationException HResult 0x80131509",
            "  inner System.IO.IOException HResult 0x80131620",
        ], Lines(FileLoggerProvider.FileNameOf(Day)));
    }

    [Fact]
    public void The_exceptions_held_by_an_aggregate_are_each_written_by_their_type_only()
    {
        var failure = new AggregateException(new TimeoutException(Secret), new UnauthorizedAccessException(Secret));
        using (var services = Start())
            Logger(services).LogCritical(failure, "Stopped");

        Assert.Equal(
        [
            "2026-10-24T23:59:59.987Z Critical Sample.Category Stopped",
            "  exception System.AggregateException HResult 0x80131500",
            "  inner System.TimeoutException HResult 0x80131505",
            "  inner System.UnauthorizedAccessException HResult 0x80070005",
        ], Lines(FileLoggerProvider.FileNameOf(Day)));
    }

    [Fact]
    public void A_line_break_in_a_message_is_written_escaped_so_that_an_entry_stays_on_one_line()
    {
        using (var services = Start())
            Logger(services).LogWarning("Name {Name}", "first\r\n2026-10-24T00:00:00.000Z Critical forged");

        Assert.Equal([@"2026-10-24T23:59:59.987Z Warning Sample.Category Name first\r\n2026-10-24T00:00:00.000Z Critical forged"],
            Lines(FileLoggerProvider.FileNameOf(Day)));
    }

    [Theory]
    [InlineData("Logging:LogLevel:Default", "Warning")]
    [InlineData("Logging:File:LogLevel:Default", "Warning")]
    [InlineData("Logging:LogLevel:Sample", "Warning")]
    public void The_minimal_level_of_the_logging_section_is_applied_to_the_file(string key, string level)
    {
        using (var services = Start((key, level)))
        {
            var logger = Logger(services);
            logger.LogInformation("Hidden");
            logger.LogWarning("Shown");
        }

        Assert.Equal(["2026-10-24T23:59:59.987Z Warning Sample.Category Shown"], Lines(FileLoggerProvider.FileNameOf(Day)));
    }

    [Fact]
    public void A_folder_that_cannot_be_created_loses_the_entries_without_any_exception()
    {
        File.WriteAllText(Folder, "a file where the folder should be");

        var failure = Record.Exception(() =>
        {
            using var services = Start();
            Logger(services).LogError(new IOException(Secret), "Lost {Code}", "ERREUR_INTERNE");
        });

        Assert.Null(failure);
        Assert.True(File.Exists(Folder));
    }

    [Fact]
    public void A_locked_file_loses_the_entry_without_any_exception_and_the_next_entry_is_written_once_it_is_released()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, FileLoggerProvider.FileNameOf(Day));
        using var services = Start();
        var logger = Logger(services);
        using (new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Assert.Null(Record.Exception(() => logger.LogInformation("Lost")));
        logger.LogInformation("Written");

        Assert.Equal(["2026-10-24T23:59:59.987Z Information Sample.Category Written"], Lines(Path.GetFileName(path)));
    }

    [Fact]
    public void A_read_only_file_loses_the_entries_without_any_exception()
    {
        Directory.CreateDirectory(Folder);
        var path = Touch(FileLoggerProvider.FileNameOf(Day));
        File.SetAttributes(path, FileAttributes.ReadOnly);

        var failure = Record.Exception(() =>
        {
            using var services = Start();
            Logger(services).LogInformation("Lost");
        });

        Assert.Null(failure);
        Assert.Equal(0, new FileInfo(path).Length);
    }

    private ServiceProvider Start(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<Topogenia.Application.Ports.IClock>(clock)
            .AddLogging(logging => logging.AddConfiguration(configuration.GetSection("Logging")).AddDailyLogFile(Folder))
            .BuildServiceProvider();
        _ = services.GetRequiredService<ILoggerFactory>();
        return services;
    }

    private static ILogger Logger(ServiceProvider services) => services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);

    private List<string> Lines(string name)
    {
        using var stream = new FileStream(Path.Combine(Folder, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private string Touch(string name)
    {
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path, "");
        return path;
    }

    /// <summary>Files of the folder that the provider did not name, some of them close to its names, all of them old.</summary>
    private string[] ForeignFiles()
    {
        var prefix = ProductIdentity.Name.ToLowerInvariant();
        var day = Day.AddDays(-400).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return
        [
            Touch("notes.txt"),
            Touch("other-" + day + ".log"),
            Touch(prefix + "-" + day + ".log.bak"),
            Touch(prefix + "-" + day + "-copy.log"),
            Touch(prefix + "-" + day[..6] + ".log"),
            Touch(prefix + "-abcdefgh.log"),
        ];
    }

    private static Exception Thrown(Exception failure)
    {
        try
        {
            throw failure;
        }
        catch (InvalidOperationException caught)
        {
            return caught;
        }
    }
}
