// SourceRulesTests.Paths.cs
// S-5.4-13 on the source text: the exact list of the files of the shipped code that open or look at a path, none of them reading a document, and the samples of the detectors of files; the exception of ADR 104 has its own file of the class.

namespace Topogenia.Architecture.Tests;

public sealed partial class SourceRulesTests
{
    /// <summary>
    /// S-5.4-13 (F-06): the server never follows the location entered for a document, a UNC path being a share of another
    /// machine. Only the files below open a file or a folder by its path, or look at one (<see cref="ShippedCodeDetectors.FileSystemAccesses"/>:
    /// the APIs of files, the helpers of paths that ask the disk, the connection strings of SQLite, the start of a
    /// process, XML, `file` URIs...), each for a path of its own that the operator, the command line or the product
    /// chooses, never one entered in a map, with one exception named and held by
    /// <see cref="Only_the_route_of_the_folder_hands_the_location_of_a_document_to_its_port"/>: <see cref="FolderShell"/> (ADR 104).
    /// </summary>
    private static readonly string[] FileSystemFiles =
    [
        // The desktop mode: its folders and data file, the protection keys of its cookie, its settings, its backups.
        "src/Topogenia.Host/Desktop/DesktopDatabase.cs", "src/Topogenia.Host/Desktop/DesktopPaths.cs", "src/Topogenia.Host/Desktop/DesktopRecovery.cs",
        "src/Topogenia.Host/Desktop/DesktopServices.cs", "src/Topogenia.Host/Desktop/DesktopSettings.cs",
        // The named lock of the desktop mode: the full path of its data file, the one of the settings of the operator, of
        // which the name of the lock is a hash (ADR 029, ADR 050).
        "src/Topogenia.Host/Desktop/DesktopInstance.cs",
        // The folder of the executable, where the host reads its settings and its pages.
        "src/Topogenia.Host/Composition/HostApplication.cs",
        // The daily log, in the folder of the settings (ADR 045).
        "src/Topogenia.Host/Logging/FileLoggerProvider.cs",
        // The server mode: the folder of the keys that protect the secret of the assistant (ADR 089).
        "src/Topogenia.Host/Server/ServerServices.cs",
        // The files named on the command line by the operator (--export, --import, --check).
        "src/Topogenia.Host/Startup/ExchangeCommands.cs", "src/Topogenia.Host/Startup/HostCommands.cs",
        // The browser of the user, opened by the shell on the local address of the desktop mode (spec 2 D10).
        "src/Topogenia.Host/Startup/StartupService.cs",
        // The exception to S-5.4-13 (ADR 104): the folder that holds a document, looked at and given to the shell in desktop
        // mode on a click of the person; the one listed file that is given a path entered in a map, through its port only.
        FolderShell,
        // The console opened, without a console, for the question of the restore only: its standard streams (ADR 090).
        "src/Topogenia.Host/Startup/ConsoleWindow.cs",
        // The scripts of the migrations, embedded in the assembly.
        "src/Topogenia.Persistence/Schema/MigrationSources.cs",
        // The instructions of the assistant and the documentation of the formats, embedded in the assembly of the connectors (3.0).
        "src/Topogenia.Assistant/AssistantInstructions.cs",
        // The SQLite file of the desktop mode, its connection, its journal, its backups and the checks of its folder (ADR 035, ADR 041).
        "src/Topogenia.Persistence.Sqlite/SqliteEngine.cs",
        "src/Topogenia.Persistence.Sqlite/Backup/SqliteBackupService.cs", "src/Topogenia.Persistence.Sqlite/Backup/SqliteRestore.cs",
        "src/Topogenia.Persistence.Sqlite/Connection/SqliteDataFileGuard.cs", "src/Topogenia.Persistence.Sqlite/Connection/SqliteJournalCheck.cs",
        "src/Topogenia.Persistence.Sqlite/Connection/SqliteWalFile.cs", "src/Topogenia.Persistence.Sqlite/Files/DataFileEnvironment.cs",
        "src/Topogenia.Persistence.Sqlite/Files/DataFileUse.cs", "src/Topogenia.Persistence.Sqlite/Files/FileIdentity.cs",
        "src/Topogenia.Persistence.Sqlite/Files/FolderExposure.cs",
        // The check of the place of that data file: its full path, short names expanded, and the folders that the
        // environment of the account names as synchronised; a local drive only, never a share (ADR 035).
        "src/Topogenia.Persistence.Sqlite/Files/DataFilePath.cs",
    ];

    /// <summary>
    /// The listed files read no document (<see cref="ShippedCodeDetectors.DocumentReads"/>: the record, its input, its
    /// address, the body of its route, the document of a `.pmap`), and no file that reads one uses a type declared in a
    /// listed file (a helper that takes a path, called with the location entered). The flows of data are not followed
    /// further (a location handed on as a plain string through a third file), and one such flow exists on purpose: the
    /// port of the folder, declared outside the listed files, by which one route hands the location of a document to
    /// <see cref="FolderShell"/> (ADR 104); the rules of the other file of this class hold it to that route, and its
    /// surface to what it is today. `DocumentLocationNeverOpenedTests` of the host proves at the run, on every route that
    /// writes, reads, copies or exports a document, that the location is never opened nor looked at, and
    /// `DocumentFolderRoutesTests` what the route of the folder does with it.
    /// </summary>
    [Fact]
    public void Shipped_code_opens_files_only_where_listed_and_never_reads_a_document_there()
    {
        var opening = SemanticSources.Production.Where(file => ShippedCodeDetectors.FileSystemAccesses(file).Any()).Select(file => file.Path).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(FileSystemFiles.Order(StringComparer.Ordinal), opening);
        var listed = SemanticSources.Production.Where(file => FileSystemFiles.Contains(file.Path)).ToList();
        Assert.Empty(listed.SelectMany(ShippedCodeDetectors.DocumentReads));
        var openers = listed.SelectMany(ShippedCodeDetectors.DeclaredTypes).ToHashSet(StringComparer.Ordinal);
        var readers = SemanticSources.Production.Where(file => ShippedCodeDetectors.DocumentReads(file).Any()).ToList();
        Assert.Empty(readers.SelectMany(file => ShippedCodeDetectors.UsesOf(file, openers)));
        // Positive controls: the document and its location are read elsewhere in the shipped code (the sheet, the
        // exports), and the listed files declare the types that the readers must not use.
        Assert.Contains(readers, file => file.Path == "src/Topogenia.Host/Api/Documents/DocumentApi.cs");
        Assert.Contains("Topogenia.Host.Desktop.DesktopSettings", openers);
        Assert.Contains("Topogenia.Host.Desktop.ShellFolderOpener", openers);
    }

    [Theory]
    [InlineData("class A { string M(string path) => System.IO.File.ReadAllText(path); }")]
    [InlineData("using System.IO; class A { object M(string path) => new FileInfo(path).Length; }")]
    [InlineData("using static System.IO.Directory; class A { bool M(string path) => Exists(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.FileStream(path, System.IO.FileMode.Open); }")]
    [InlineData("class A { object M(string path) => new System.IO.StreamReader(path); }")]
    [InlineData("class A { object M(string path) => System.IO.Compression.ZipFile.OpenRead(path); }")]
    [InlineData("using System.IO.Compression; class A { void M(ZipArchive zip, string path) => zip.ExtractToDirectory(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.StreamWriter(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.FileSystemWatcher(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.DriveInfo(path); }")]
    [InlineData("class A { object M(string path) => System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(path); }")]
    [InlineData("class A { object M(string path) => new Microsoft.Extensions.FileProviders.PhysicalFileProvider(path); }")]
    [InlineData("class A { object M(string path) => System.Diagnostics.FileVersionInfo.GetVersionInfo(path); }")]
    [InlineData("class A { string M(Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult reply) => reply.FileName; }")]
    public void Every_way_to_open_a_file_by_its_path_is_detected(string sample) =>
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    /// <summary>
    /// The helpers of paths that look at the disk or at a share without opening anything (the gap of the first detectors:
    /// a path asked whether it is there, a full path asked to the framework, which reads the disk for a tilde).
    /// </summary>
    [Theory]
    [InlineData("class A { bool M(string location) => System.IO.Path.Exists(location); }")]
    [InlineData("using System.IO; class A { bool M(string location) => Path.Exists(location); }")]
    [InlineData("using static System.IO.Path; class A { bool M(string location) => Exists(location); }")]
    [InlineData("using Paths = System.IO.Path; class A { System.Func<string?, bool> M() => Paths.Exists; }")]
    [InlineData("class A { string M(string location) => System.IO.Path.GetFullPath(location); }")]
    [InlineData("class A { string M(string location, string folder) => System.IO.Path.GetFullPath(location, folder); }")]
    [InlineData("using static System.IO.Path; class A { string M(string location) => GetFullPath(location); }")]
    [InlineData("class A { string M(string location, string folder) => System.IO.Path.GetRelativePath(folder, location); }")]
    public void Every_way_to_look_at_a_path_without_opening_it_is_detected(string sample) =>
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    /// <summary>The other ways to open a path given as text: a connection string, a process, XML, a `file` URI, a load from a file.</summary>
    [Theory]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(string location) => new SqliteConnection(\"Data Source=\" + location).Open(); }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M(string text) => new SqliteConnection(text); }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M(string text) => new SqliteConnection { ConnectionString = text }; }")]
    [InlineData("class A { void M(System.Data.Common.DbConnection connection, string text) => connection.ConnectionString = text; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { string M(string path) => new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(SqliteConnectionStringBuilder builder, string path) => builder.DataSource = path; }")]
    [InlineData("class A { void M(System.Data.Common.DbConnectionStringBuilder builder, string path) => builder[\"Data Source\"] = path; }")]
    [InlineData("class A { string M(string path) => $\"Mode=ReadOnly;Data Source={path}\"; }")]
    [InlineData("class A { string M(string path) => \"Server=.;AttachDbFilename=\" + path; }")]
    [InlineData("using Microsoft.EntityFrameworkCore; class A { void M(DbContextOptionsBuilder options, string text) => options.UseSqlite(text); }")]
    [InlineData("class A { void M(string location) => System.Diagnostics.Process.Start(location); }")]
    [InlineData("using System.Diagnostics; class A { void M(Process process) => process.Start(); }")]
    [InlineData("using System.Diagnostics; class A { object M(string location) => new ProcessStartInfo(location) { UseShellExecute = true }; }")]
    [InlineData("class A { object M(string location) => System.Xml.XmlReader.Create(location); }")]
    [InlineData("class A { object M(string location) => System.Xml.Linq.XDocument.Load(location); }")]
    [InlineData("class A { void M(System.Xml.Linq.XElement element, string location) => element.Save(location); }")]
    [InlineData("class A { void M(System.Xml.XmlDocument document, string location) => document.Load(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XPath.XPathDocument(location); }")]
    [InlineData("class A { object M() => new System.Xml.XmlUrlResolver(); }")]
    [InlineData("class A { object M(string share) => new System.Uri(\"file://\" + share); }")]
    [InlineData("class A { bool M(System.Uri uri) => uri.Scheme == System.Uri.UriSchemeFile; }")]
    [InlineData("class A { string M(System.Uri uri) => uri.IsUnc ? uri.LocalPath : \"\"; }")]
    [InlineData("class A { object M(string path) => System.Reflection.Assembly.LoadFrom(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509CertificateLoader.LoadCertificateFromFile(path); }")]
    [InlineData("class A { object M(string path) => Microsoft.AspNetCore.Http.TypedResults.PhysicalFile(path); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { Task M(HttpResponse response, string path) => response.SendFileAsync(path); }")]
    [InlineData("using Microsoft.Extensions.Configuration; class A { void M(IConfigurationBuilder builder, string path) => builder.AddJsonFile(path); }")]
    [InlineData("class A { object M(string folder) => new Microsoft.AspNetCore.Builder.WebApplicationOptions { ContentRootPath = folder }; }")]
    [InlineData("class A { object M(string path) => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { AttachDBFilename = path }; }")]
    [InlineData("class A { object M(string location) => System.Xml.XmlWriter.Create(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XmlTextReader(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XmlTextWriter(location, null); }")]
    [InlineData("class A { void M(System.Xml.Xsl.XslCompiledTransform transform, string location) => transform.Load(location); }")]
    [InlineData("class A { void M(System.Data.DataSet data, string location) => data.ReadXml(location); }")]
    [InlineData("class A { object M(string path) => new System.Security.Cryptography.X509Certificates.X509Certificate(path); }")]
    [InlineData("class A { object M(string path) => new System.Security.Cryptography.X509Certificates.X509Certificate2(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509Certificate.CreateFromCertFile(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509Certificate2.CreateFromPemFile(path); }")]
    [InlineData("class A { object M(string path) => System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path); }")]
    [InlineData("class A { object M(string path) => System.Runtime.InteropServices.NativeLibrary.Load(path); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(string path) => Results.File(path); }")]
    [InlineData("using Microsoft.Extensions.Configuration; class A { void M(IConfigurationBuilder builder, string folder) => builder.SetBasePath(folder); }")]
    [InlineData("using Microsoft.AspNetCore.Hosting; class A { void M(IWebHostBuilder builder, string folder) => builder.UseContentRoot(folder); }")]
    [InlineData("using Microsoft.Extensions.Hosting; class A { void M(IHostBuilder builder, string folder) => builder.UseContentRoot(folder); }")]
    public void Every_way_to_open_a_path_given_as_text_is_detected(string sample) =>
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    [Theory]
    [InlineData("class A { string M(string path) => System.IO.Path.GetFileName(path); }")]
    [InlineData("class A { string M(string folder, string name) => System.IO.Path.Combine(folder, name); }")]
    [InlineData("class A { bool M(string path) => System.IO.Path.IsPathFullyQualified(path); }")]
    [InlineData("class Path { public static bool Exists(string text) => text.Length > 0; } class A { bool M(string text) => Path.Exists(text); }")]
    [InlineData("class A { int M(System.IO.Stream stream) => stream.ReadByte(); }")]
    [InlineData("class A { } // File.ReadAllText(path) in a comment")]
    [InlineData("using Microsoft.Data.Sqlite; class A { bool M(System.Data.Common.DbConnection connection) => connection is SqliteConnection; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { string M(SqliteConnection connection) => new SqliteConnectionStringBuilder(connection.ConnectionString).DataSource; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M() => new SqliteConnection(); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => System.Xml.XmlReader.Create(stream); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => System.Xml.Linq.XDocument.Load(stream); }")]
    [InlineData("class A { object M(string xml) => System.Xml.Linq.XDocument.Parse(xml); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => new System.Xml.XPath.XPathDocument(stream); }")]
    [InlineData("class A { bool M(string text) => System.Uri.TryCreate(text, System.UriKind.Absolute, out var uri) && uri.Scheme == System.Uri.UriSchemeHttps; }")]
    [InlineData("class A { int M(System.Uri uri) => uri.Port; }")]
    [InlineData("class A { int M() => System.Diagnostics.Process.GetCurrentProcess().Id; }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(byte[] content) => TypedResults.File(content, \"text/plain\", \"copy.pmap\"); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(System.IO.Stream content) => Results.File(content, \"application/zip\", \"copy.zip\"); }")]
    [InlineData("class A { string M(string name) => $\"attachment; filename={name}; the data source of the report\"; }")]
    [InlineData("class A { string M(string profile) => \"profile: \" + profile; }")]
    [InlineData("class A { } // new SqliteConnection(\"Data Source=x\") and Process.Start(location) in a comment")]
    public void A_path_computed_or_a_stream_already_open_is_no_file_opened(string sample) =>
        Assert.Empty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    /// <summary>A file that reads the location of a document and calls a helper of a file that opens paths is seen; one that calls another file is not.</summary>
    [Theory]
    [InlineData("static class Probe { public static bool Exists(string path) => System.IO.File.Exists(path); }",
        "class Route { bool M(Topogenia.Domain.Model.Document document) => Probe.Exists(document.Location!); }", true)]
    [InlineData("static class Probe { public static bool Exists(string path) => System.IO.Path.Exists(path); }",
        "class Route { bool M(Topogenia.Domain.Model.Document document) => Probe.Exists(document.Location!); }", true)]
    [InlineData("static class Probe { public static bool Exists(string path) => System.IO.File.Exists(path); }",
        "static class Pages { public static string Of(string text) => text.Trim(); } class Route { string M(Topogenia.Domain.Model.Document document) => Pages.Of(document.Location!); }", false)]
    public void A_reader_of_a_document_that_calls_a_file_that_opens_paths_is_detected(string opener, string reader, bool detected)
    {
        var files = SemanticSources.Samples(("src/Topogenia.Host/Opener.cs", opener), ("src/Topogenia.Host/Reader.cs", reader));
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(files[0]));
        Assert.NotEmpty(ShippedCodeDetectors.DocumentReads(files[1]));
        var openers = ShippedCodeDetectors.DeclaredTypes(files[0]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(detected, ShippedCodeDetectors.UsesOf(files[1], openers).Any());
    }
}
