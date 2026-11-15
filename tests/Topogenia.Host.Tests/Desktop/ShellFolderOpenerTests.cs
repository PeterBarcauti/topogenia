// ShellFolderOpenerTests.cs
// The look of Windows at a path (ADR 104), on folders and files made by the test: a folder, a file never opened, nothing, and the form given to the shell, which names no file; where the verb is read, by the folder; the thread of the shell without the shell, its question asked once and no exception left there; the shell itself is never called.

using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

/// <summary>
/// The real look, on a temporary folder removed at the end. <see cref="ShellFolderOpener"/> is never built here nor
/// anywhere in the tests, so that <see cref="ShellFolderOpener.Show"/> cannot be called: it would open an Explorer on
/// the machine that runs them. The look is another class, which cannot ask the shell.
/// </summary>
public sealed class ShellFolderOpenerTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-folders-");
    private readonly WindowsFolderDisk windows = new();

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(root.FullName, name)).FullName;

    private static string FileIn(string folder, string name)
    {
        var file = Path.Combine(folder, name);
        File.WriteAllText(file, "inventaire");
        return file;
    }

    /// <summary>The verb that shows a folder in the Explorer, and the class of the folders, where it is read for a root.</summary>
    [Fact]
    public void The_shell_is_given_the_verb_that_shows_a_folder_and_the_class_of_the_folders()
    {
        Assert.Equal("explore", ShellFolderOpener.Verb);
        Assert.Equal("Folder", ShellFolderOpener.Class);
    }

    /// <summary>
    /// Where the verb is read (ADR 104, point 6). For a root, a drive or a share alone, in the class of the folders and
    /// no other: a disc may declare a verb of its own for its root (`autorun.inf`), which would start a program, and a
    /// root is a folder whatever is put on the disk. For any other folder no class is named: the shell reads the verb
    /// in the classes of what the path names, where no file has it, so that a file put where the folder was after the
    /// look is refused; under the class of the folders it would be handed to the Explorer as a folder.
    /// </summary>
    [Theory]
    [InlineData(@"C:\", "Folder")]
    [InlineData(@"E:\", "Folder")]
    [InlineData(@"\\serveur\partage\", "Folder")]
    [InlineData(@"C:\Dossiers\", null)]
    [InlineData(@"E:\Dossiers\Musee\", null)]
    [InlineData(@"\\serveur\partage\dossier\", null)]
    [InlineData(@"\\serveur\partage\rapport.exe\", null)]
    public void The_verb_is_read_in_the_class_of_the_folders_for_a_root_alone(string folder, string? named) =>
        Assert.Equal(named, ShellFolderOpener.ClassOf(folder));

    /// <summary>
    /// The thread of the shell, without the shell: the question is asked once, on another thread, in the apartment the
    /// shell wants, and its error comes back as it is. A question that fails there is not asked again and says an error
    /// read as a folder that does not answer: an exception left on a thread of its own would end the program.
    /// </summary>
    [Fact]
    public void A_question_that_fails_on_the_thread_of_the_shell_says_unreachable_and_does_not_end_the_program()
    {
        var caller = Environment.CurrentManagedThreadId;
        var asked = (Thread: caller, Apartment: ApartmentState.Unknown, Background: false);
        var times = 0;
        var answered = ShellFolderOpener.OnShellThread(() =>
        {
            times++;
            asked = (Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState(), Thread.CurrentThread.IsBackground);
            return 1326;
        });

        Assert.Equal(1326, answered);
        Assert.Equal(1, times);
        Assert.NotEqual(caller, asked.Thread);
        Assert.Equal(ApartmentState.STA, asked.Apartment);
        Assert.True(asked.Background);

        var failed = ShellFolderOpener.OnShellThread(() =>
        {
            times++;
            throw new InvalidOperationException("shell");
        });
        Assert.Equal(2, times);
        Assert.NotEqual(0, failed);
        Assert.Equal(FolderOutcome.Unreachable, FolderPaths.OutcomeOf(failed));
    }

    /// <summary>Nothing leaves the thread of the shell, whatever it is: the failure the rest of the code lets through ends the program there too.</summary>
    [Fact]
    public void No_failure_of_any_kind_leaves_the_thread_of_the_shell()
    {
        var failed = ShellFolderOpener.OnShellThread(() => throw new OutOfMemoryException("shell"));
        Assert.NotEqual(0, failed);
        Assert.Equal(FolderOutcome.Unreachable, FolderPaths.OutcomeOf(failed));
    }

    /// <summary>
    /// A short name (a tilde) is looked at as any path: the framework first asks Windows for its long name, then for the
    /// attributes. Played on the short name of the folder of the programs, where the drive of the system keeps short
    /// names; a drive that keeps none has no such folder, and the test then proves nothing.
    /// </summary>
    [Fact]
    public void A_path_written_with_a_short_name_is_looked_at_and_a_name_under_it_that_names_nothing_is_missing()
    {
        var programs = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "PROGRA~1");
        if (!Directory.Exists(programs))
            return;
        Assert.Equal(FolderLook.Folder, windows.Look(programs));
        Assert.Equal(FolderLook.Missing, windows.Look(Path.Combine(programs, $"absent-{Guid.NewGuid():N}")));
        Assert.Equal(FolderLook.Missing, windows.Look(Path.Combine(programs, $"absent-{Guid.NewGuid():N}", "fichier.txt")));
    }

    [Fact]
    public void A_folder_is_a_folder_a_file_is_not_one_and_a_name_alone_is_missing()
    {
        var folder = Folder("Musee");
        var file = FileIn(folder, "inventaire.xlsx");

        Assert.Equal(FolderLook.Folder, windows.Look(folder));
        Assert.Equal(FolderLook.Folder, windows.Look(Path.GetPathRoot(folder)!));
        Assert.Equal(FolderLook.NotFolder, windows.Look(file));
        Assert.Equal(FolderLook.Missing, windows.Look(Path.Combine(folder, "absent.xlsx")));
        Assert.Equal(FolderLook.Missing, windows.Look(Path.Combine(folder, "Absent", "fichier.xlsx")));
    }

    /// <summary>The test holds the file open and shared with no one: any opening of it, to read it or to run it, would fail.</summary>
    [Fact]
    public void A_file_is_looked_at_without_being_opened_nor_read()
    {
        var file = FileIn(Folder("Musee"), "inventaire.xlsx");
        var written = File.GetLastWriteTimeUtc(file);
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => File.ReadAllBytes(file));
            Assert.Equal(FolderLook.NotFolder, windows.Look(file));
        }
        Assert.Equal(written, File.GetLastWriteTimeUtc(file));
        Assert.Equal("inventaire", File.ReadAllText(file));
    }

    /// <summary>
    /// The form given to the shell (the final separator) names a folder or nothing: Windows opens no file under it, so a
    /// file put where the folder was between the look and the shell is not what the shell is given. The proof is on the
    /// file functions of Windows; the look itself, which reads attributes, does not tell the two forms apart, and the
    /// shell is never called by a test.
    /// </summary>
    [Fact]
    public void Under_the_form_given_to_the_shell_windows_opens_the_folder_and_never_a_file_put_in_its_place()
    {
        var folder = Folder("Musee");
        var given = FolderPaths.AsFolder(folder);
        Assert.Equal(FolderLook.Folder, windows.Look(given));
        Assert.Empty(Directory.GetFiles(given));

        Directory.Delete(folder);
        File.WriteAllText(folder, "echo");
        Assert.Equal(FolderLook.NotFolder, windows.Look(folder));

        Assert.False(File.Exists(given));
        Assert.ThrowsAny<IOException>(() => File.ReadAllText(given));
        Assert.ThrowsAny<IOException>(() => File.OpenHandle(given).Dispose());
        Assert.ThrowsAny<IOException>(() => Directory.GetFiles(given));
        Assert.Equal("echo", File.ReadAllText(folder));
    }

    /// <summary>The whole opener on the real disk, its shell replaced: the folder of a file, of a folder, and nothing for a name without a folder.</summary>
    [Fact]
    public async Task The_opener_on_a_real_disk_gives_the_shell_the_folder_just_looked_at()
    {
        var folder = Folder("Association");
        var file = FileIn(folder, "statuts.pdf");
        var shell = new FolderRecorder();
        var opener = new CheckedFolderOpener(windows, shell, new FolderOpeningOptions(TimeSpan.FromMinutes(5)));
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(file, token));
        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(folder, token));
        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(Path.Combine(folder, "absent.pdf"), token));
        Assert.Equal(FolderOutcome.NotFound, await opener.ShowAsync(Path.Combine(folder, "Absent", "fichier.pdf"), token));

        Assert.Equal([folder + @"\", folder + @"\", folder + @"\"], shell.Shown);
        Assert.All(shell.Shown, shown => Assert.True(Directory.Exists(shown)));
    }

    public void Dispose() => root.Delete(recursive: true);
}
