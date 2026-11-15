// FolderOpenerTests.cs
// The opener of « Open the folder » (ADR 104), its disk and its shell replaced: which folder is shown, in which form, what is never looked at nor shown, the bounded look and the bounded shell, one click at a time.

using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class FolderOpenerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A bound no look of these tests reaches, however loaded the machine.</summary>
    private static readonly FolderOpeningOptions Patient = new(TimeSpan.FromMinutes(5));

    /// <summary>A bound that a look held by its test always exceeds.</summary>
    private static readonly FolderOpeningOptions Hasty = new(TimeSpan.FromMilliseconds(30));

    /// <summary>A disk of a test: what each path is, anything else missing; the paths looked at, in their order.</summary>
    private sealed class FakeDisk(params (string Path, FolderLook Look)[] known) : IFolderDisk
    {
        public List<string> Looked { get; } = [];

        public FolderLook Look(string path)
        {
            lock (Looked)
                Looked.Add(path);
            return known.Where(entry => entry.Path == path).Select(entry => (FolderLook?)entry.Look).FirstOrDefault() ?? FolderLook.Missing;
        }
    }

    private static FolderRecorder Shell() => new();

    [Fact]
    public void A_look_is_given_three_seconds_by_default() => Assert.Equal(TimeSpan.FromSeconds(3), FolderOpeningOptions.Default.LookTimeout);

    private static async Task<FolderOutcome> ShowAsync(IFolderDisk disk, IFolderShell shell, string path, FolderOpeningOptions? options = null) =>
        await new CheckedFolderOpener(disk, shell, options ?? Patient).ShowAsync(path, Token);

    [Fact]
    public async Task A_path_that_is_a_folder_is_shown_itself_with_its_final_separator_after_one_look()
    {
        var disk = new FakeDisk((@"C:\Dossiers\Musee", FolderLook.Folder));
        var shell = Shell();

        Assert.Equal(FolderOutcome.Shown, await ShowAsync(disk, shell, @"C:\Dossiers\Musee"));

        Assert.Equal([@"C:\Dossiers\Musee"], disk.Looked);
        Assert.Equal([@"C:\Dossiers\Musee\"], shell.Shown);
    }

    [Fact]
    public async Task A_path_that_is_a_file_shows_the_folder_that_holds_it_and_the_file_is_never_given_to_the_shell()
    {
        var disk = new FakeDisk((@"\\serveur\partage\dossier\fichier.exe", FolderLook.NotFolder), (@"\\serveur\partage\dossier", FolderLook.Folder));
        var shell = Shell();

        Assert.Equal(FolderOutcome.Shown, await ShowAsync(disk, shell, @"\\serveur\partage\dossier\fichier.exe"));

        Assert.Equal([@"\\serveur\partage\dossier\fichier.exe", @"\\serveur\partage\dossier"], disk.Looked);
        Assert.Equal([@"\\serveur\partage\dossier\"], shell.Shown);
    }

    [Fact]
    public async Task A_path_that_names_nothing_shows_its_parent_when_it_is_a_folder_and_never_a_folder_further_up()
    {
        var disk = new FakeDisk((@"C:\Dossiers", FolderLook.Folder), (@"C:\", FolderLook.Folder));
        var shell = Shell();

        Assert.Equal(FolderOutcome.Shown, await ShowAsync(disk, shell, @"C:\Dossiers\ancien.xlsx"));
        Assert.Equal(FolderOutcome.NotFound, await ShowAsync(disk, shell, @"C:\Dossiers\Archives\ancien.xlsx"));
        Assert.Equal(FolderOutcome.NotFound, await ShowAsync(disk, shell, @"Z:\"));

        Assert.Equal([@"C:\Dossiers\ancien.xlsx", @"C:\Dossiers", @"C:\Dossiers\Archives\ancien.xlsx", @"C:\Dossiers\Archives", @"Z:\"], disk.Looked);
        Assert.Equal([@"C:\Dossiers\"], shell.Shown);
    }

    /// <summary>A parent that is a file (a file put where the folder was) is no folder to show.</summary>
    [Theory]
    [InlineData(FolderLook.Denied, FolderLook.Folder, FolderOutcome.Denied, 1)]
    [InlineData(FolderLook.Unreachable, FolderLook.Folder, FolderOutcome.Unreachable, 1)]
    [InlineData(FolderLook.NotFolder, FolderLook.Denied, FolderOutcome.Denied, 2)]
    [InlineData(FolderLook.Missing, FolderLook.Unreachable, FolderOutcome.Unreachable, 2)]
    [InlineData(FolderLook.Missing, FolderLook.NotFolder, FolderOutcome.NotFound, 2)]
    [InlineData(FolderLook.Missing, FolderLook.Missing, FolderOutcome.NotFound, 2)]
    public async Task A_look_refused_or_unanswered_is_said_as_it_is_and_nothing_is_shown(FolderLook item, FolderLook parent, FolderOutcome outcome, int looks)
    {
        var disk = new FakeDisk((@"\\serveur\partage\dossier\fichier.xlsx", item), (@"\\serveur\partage\dossier", parent));
        var shell = Shell();

        Assert.Equal(outcome, await ShowAsync(disk, shell, @"\\serveur\partage\dossier\fichier.xlsx"));

        Assert.Equal(looks, disk.Looked.Count);
        Assert.Empty(shell.Shown);
    }

    [Theory]
    [InlineData(@"\\?\C:\Dossiers\fichier.xlsx")]
    [InlineData(@"\\.\pipe\canal")]
    [InlineData(@"\\serveur\pipe\canal")]
    [InlineData(@"C:\Dossiers\..\Windows\notepad.exe")]
    [InlineData(@"C:\Dossiers\rapport.")]
    public async Task A_path_refused_on_its_text_is_neither_looked_at_nor_shown(string path)
    {
        var disk = new FakeDisk((path, FolderLook.Folder));
        var shell = Shell();

        Assert.Equal(FolderOutcome.Unsupported, await ShowAsync(disk, shell, path));

        Assert.Empty(disk.Looked);
        Assert.Empty(shell.Shown);
    }

    [Fact]
    public async Task A_path_of_260_characters_is_too_long_before_any_look_and_a_folder_of_259_is_too_long_to_show()
    {
        var folder = @"C:\Dossiers\" + new string('a', FolderPaths.MaxLength - 12);
        var disk = new FakeDisk((folder, FolderLook.Folder), (folder + "a", FolderLook.Folder));
        var shell = Shell();

        Assert.Equal(FolderOutcome.TooLong, await ShowAsync(disk, shell, folder + "a"));
        Assert.Empty(disk.Looked);
        // The folder itself would be given to the shell in 260 characters, its final separator counted.
        Assert.Equal(FolderOutcome.TooLong, await ShowAsync(disk, shell, folder));
        Assert.Empty(shell.Shown);
        // A file of 259 characters shows its folder, which is shorter.
        var file = @"C:\Dossiers\" + new string('a', FolderPaths.MaxLength - 20) + @"\fichier";
        Assert.Equal(259, file.Length);
        Assert.Equal(FolderOutcome.Shown, await ShowAsync(new FakeDisk((Path.GetDirectoryName(file)!, FolderLook.Folder)), shell, file));
        Assert.Equal([Path.GetDirectoryName(file) + @"\"], shell.Shown);
    }

    [Fact]
    public async Task What_the_shell_answers_is_the_outcome()
    {
        var disk = new FakeDisk((@"C:\Dossiers", FolderLook.Folder));
        var shell = new FolderRecorder { Reply = FolderOutcome.NotFound };

        Assert.Equal(FolderOutcome.NotFound, await ShowAsync(disk, shell, @"C:\Dossiers"));

        Assert.Equal([@"C:\Dossiers\"], shell.Shown);
    }

    [Fact]
    public async Task A_look_that_exceeds_its_bound_says_unreachable_and_its_late_answer_shows_nothing()
    {
        using var disk = new HeldLook();
        var shell = Shell();
        var opener = new CheckedFolderOpener(disk, shell, Hasty);

        Assert.Equal(FolderOutcome.Unreachable, await opener.ShowAsync(@"\\serveur\partage\dossier", Token));
        // The look still waits for the server: a second click is refused at once, without a second look.
        Assert.Equal(FolderOutcome.Busy, await opener.ShowAsync(@"C:\Dossiers", Token));
        Assert.Equal(1, disk.Looks);

        disk.Release();
        await FreeAsync(opener);

        Assert.Empty(shell.Shown);
    }

    [Fact]
    public async Task A_second_click_while_a_look_waits_is_refused_at_once_and_the_first_is_shown_when_its_answer_comes()
    {
        using var disk = new HeldLook();
        var shell = Shell();
        var opener = new CheckedFolderOpener(disk, shell, Patient);
        var first = opener.ShowAsync(@"\\serveur\partage\dossier", Token);
        await disk.WaitingAsync();

        Assert.Equal(FolderOutcome.Busy, await opener.ShowAsync(@"\\serveur\partage\autre", Token));
        Assert.Equal(1, disk.Looks);
        Assert.Empty(shell.Shown);

        disk.Release();
        Assert.Equal(FolderOutcome.Shown, await first);
        await FreeAsync(opener);
        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(@"\\serveur\partage\autre", Token));
        Assert.Equal([@"\\serveur\partage\dossier\", @"\\serveur\partage\autre\"], shell.Shown);
    }

    [Fact]
    public async Task A_request_given_up_while_its_look_waits_shows_nothing()
    {
        using var disk = new HeldLook();
        var shell = Shell();
        var opener = new CheckedFolderOpener(disk, shell, Patient);
        using var givenUp = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var asked = opener.ShowAsync(@"\\serveur\partage\dossier", givenUp.Token);
        await disk.WaitingAsync();

        await givenUp.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asked);
        disk.Release();
        await FreeAsync(opener);
        Assert.Empty(shell.Shown);
    }

    /// <summary>
    /// The shell is asked on the thread of the look and the turn is kept until it answers: a shell that Windows keeps
    /// waiting (a server that answered the look, then went silent) holds neither the request nor a thread a click.
    /// </summary>
    [Fact]
    public async Task A_shell_that_does_not_answer_keeps_the_turn_a_second_click_is_refused_and_the_first_says_unreachable()
    {
        using var shell = new HeldShell();
        var opener = new CheckedFolderOpener(new FixedLook(FolderLook.Folder), shell, Hasty);
        var first = opener.ShowAsync(@"\\serveur\partage\dossier", Token);
        await shell.WaitingAsync();

        Assert.Equal(FolderOutcome.Busy, await opener.ShowAsync(@"C:\Dossiers", Token));
        Assert.Equal(FolderOutcome.Unreachable, await first);
        Assert.Equal(1, shell.Asked);
        Assert.True(opener.Looking);

        shell.Release();
        await FreeAsync(opener);
        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(@"C:\Dossiers", Token));
        Assert.Equal(2, shell.Asked);
    }

    /// <summary>
    /// A request given up while the shell is asked (the person leaves the page): the caller is told at once, the shell
    /// was asked once and is not asked again, and the turn stays taken until Windows answers.
    /// </summary>
    [Fact]
    public async Task A_request_given_up_while_the_shell_waits_keeps_the_turn_and_asks_the_shell_once()
    {
        using var shell = new HeldShell();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var opener = new CheckedFolderOpener(new FixedLook(FolderLook.Folder), shell, Patient);
        var first = opener.ShowAsync(@"C:\Dossiers\Musee", request.Token);
        await shell.WaitingAsync();

        await request.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.True(opener.Looking);
        Assert.Equal(FolderOutcome.Busy, await opener.ShowAsync(@"C:\Dossiers", Token));
        Assert.Equal(1, shell.Asked);

        shell.Release();
        await FreeAsync(opener);
        Assert.Equal(1, shell.Asked);
        Assert.Equal(FolderOutcome.Shown, await opener.ShowAsync(@"C:\Dossiers", Token));
    }

    /// <summary>A look or a shell that fails otherwise than by its answer is a folder that does not answer; the opener takes the next click.</summary>
    [Fact]
    public async Task A_look_or_a_shell_that_fails_says_unreachable_shows_nothing_and_gives_the_turn_back()
    {
        var shell = Shell();
        var opener = new CheckedFolderOpener(new FailingLook(), shell, Patient);

        Assert.Equal(FolderOutcome.Unreachable, await opener.ShowAsync(@"C:\Dossiers", Token));
        await FreeAsync(opener);
        Assert.Equal(FolderOutcome.Unreachable, await opener.ShowAsync(@"C:\Dossiers\Musee", Token));
        Assert.Empty(shell.Shown);

        var failing = new CheckedFolderOpener(new FixedLook(FolderLook.Folder), new FailingShell(), Patient);
        Assert.Equal(FolderOutcome.Unreachable, await failing.ShowAsync(@"C:\Dossiers", Token));
        await FreeAsync(failing);
        Assert.False(failing.Looking);
    }

    /// <summary>Waits until the look let go by the test has ended: the opener takes a look again.</summary>
    private static async Task FreeAsync(CheckedFolderOpener opener)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromMinutes(1));
        while (opener.Looking)
            await Task.Delay(1, bound.Token);
    }
}
