// FolderPathsTests.cs
// The rules of a path asked to be shown as a folder (ADR 104): its length, the forms refused before anything is looked at, the separators of Windows, its parent, the form given to the shell, the errors of Windows.

using Topogenia.Host.Desktop;

namespace Topogenia.Host.Tests.Desktop;

public sealed class FolderPathsTests
{
    [Theory]
    [InlineData(@"C:\Dossiers\Musee\inventaire.xlsx", @"C:\Dossiers\Musee\inventaire.xlsx", @"C:\Dossiers\Musee")]
    [InlineData(@"C:\Dossiers\Musee\", @"C:\Dossiers\Musee", @"C:\Dossiers")]
    [InlineData(@"C:\Dossiers", @"C:\Dossiers", @"C:\")]
    [InlineData(@"\\serveur\partage\dossier\fichier.xlsx", @"\\serveur\partage\dossier\fichier.xlsx", @"\\serveur\partage\dossier")]
    [InlineData(@"\\serveur\partage\dossier\", @"\\serveur\partage\dossier", @"\\serveur\partage")]
    [InlineData(@"C:\Dossiers\Notes de visite\plan 2026.v2.pdf", @"C:\Dossiers\Notes de visite\plan 2026.v2.pdf", @"C:\Dossiers\Notes de visite")]
    [InlineData(@"\\serveur-01.exemple.org\partage\dossier", @"\\serveur-01.exemple.org\partage\dossier", @"\\serveur-01.exemple.org\partage")]
    [InlineData(@"\\192.168.1.20\partage\fichier.xlsx", @"\\192.168.1.20\partage\fichier.xlsx", @"\\192.168.1.20\partage")]
    [InlineData(@"\\serveur_1\partage\dossier", @"\\serveur_1\partage\dossier", @"\\serveur_1\partage")]
    [InlineData(@"C:\Dossiers\COM10.txt", @"C:\Dossiers\COM10.txt", @"C:\Dossiers")]
    [InlineData(@"C:\Dossiers\console.txt", @"C:\Dossiers\console.txt", @"C:\Dossiers")]
    [InlineData(@"C:\Dossiers\{projet}\plan.pdf", @"C:\Dossiers\{projet}\plan.pdf", @"C:\Dossiers\{projet}")]
    [InlineData("C:\\Dossiers\\\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645.docx", "C:\\Dossiers\\\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645.docx", @"C:\Dossiers")]
    public void A_path_written_as_windows_writes_it_is_kept_whole_with_the_folder_that_holds_it(string path, string item, string parent) =>
        Assert.Equal(new FolderPath(null, item, parent), FolderPaths.Read(path));

    /// <summary>
    /// A short name (a tilde) is kept as it is written, whatever is on the disk: the rules read the text alone, so the
    /// same path gets the same answer on a drive that holds the folder and on a drive that does not exist.
    /// </summary>
    [Theory]
    [InlineData(@"C:\PROGRA~1\Outil\notice.pdf", @"C:\PROGRA~1\Outil")]
    [InlineData(@"Q:\PROGRA~1\Outil\notice.pdf", @"Q:\PROGRA~1\Outil")]
    [InlineData(@"\\serveur\partage\~$inventaire.xlsx", @"\\serveur\partage")]
    public void A_short_name_is_kept_as_written_without_a_look_at_the_disk(string path, string parent) =>
        Assert.Equal(new FolderPath(null, path, parent), FolderPaths.Read(path));

    /// <summary>A drive without a folder and a share without a folder are folders of their own: nothing above them is shown.</summary>
    [Theory]
    [InlineData(@"X:\", @"X:\")]
    [InlineData("x:/", @"x:\")]
    [InlineData(@"\\serveur\partage", @"\\serveur\partage")]
    [InlineData(@"\\serveur\partage\", @"\\serveur\partage")]
    public void A_drive_or_a_share_alone_is_its_own_folder_and_has_no_parent(string path, string item) =>
        Assert.Equal(new FolderPath(null, item, null), FolderPaths.Read(path));

    /// <summary>Mixed or repeated separators name the same folder: they are written as Windows writes them, nothing else is changed.</summary>
    [Theory]
    [InlineData(@"C:/Dossiers\Musee/inventaire.xlsx", @"C:\Dossiers\Musee\inventaire.xlsx", @"C:\Dossiers\Musee")]
    [InlineData(@"C:\\Dossiers\\\Musee//inventaire.xlsx", @"C:\Dossiers\Musee\inventaire.xlsx", @"C:\Dossiers\Musee")]
    [InlineData(@"\\serveur/partage/dossier\fichier.xlsx", @"\\serveur\partage\dossier\fichier.xlsx", @"\\serveur\partage\dossier")]
    [InlineData(@"\\\\serveur\\partage\\dossier", @"\\serveur\partage\dossier", @"\\serveur\partage")]
    public void Mixed_or_repeated_separators_are_written_as_windows_writes_them(string path, string item, string parent) =>
        Assert.Equal(new FolderPath(null, item, parent), FolderPaths.Read(path));

    /// <summary>
    /// The paths Windows would read as another object than the folder written: a device or a pipe, the namespace that
    /// turns the rules of the paths off, a segment that climbs, a name Windows would cut (a final dot or space), a
    /// stream of a file, a control character, a server without a share; a server that is not written as the name of a
    /// machine (the prefix of a provider of Windows, which would reach a pipe behind the name of a share, or a WebDAV
    /// address); the name of a device of Windows, with or without an extension; a folder named as an object of Windows;
    /// a character that hides or reorders what is written. Each is refused on its text, nothing looked at.
    /// </summary>
    [Theory]
    [InlineData(@"\\;LanmanRedirector\serveur\pipe\canal")]
    [InlineData(@"\\;LanmanRedirector\127.0.0.1\IPC$\canal")]
    [InlineData(@"\\;LanmanRedirector\serveur\partage\dossier")]
    [InlineData(@"\\;WebDavRedirector\serveur\partage\dossier")]
    [InlineData(@"\\serveur@SSL\partage\dossier")]
    [InlineData(@"\\serveur@80\DavWWWRoot\dossier")]
    [InlineData(@"\\127.0.0.1@8080\partage\dossier")]
    [InlineData(@"\\serveur nom\partage\dossier")]
    [InlineData(@"C:\Dossiers\NUL")]
    [InlineData(@"C:\Dossiers\nul.txt")]
    [InlineData(@"C:\Dossiers\CON")]
    [InlineData(@"C:\CON\fichier.xlsx")]
    [InlineData(@"C:\Dossiers\PRN.pdf")]
    [InlineData(@"C:\Dossiers\aux.tar.gz")]
    [InlineData(@"C:\Dossiers\COM1")]
    [InlineData(@"C:\Dossiers\com0.txt")]
    [InlineData(@"C:\Dossiers\LPT9.pdf")]
    [InlineData("C:\\Dossiers\\COM\u00B9")]
    [InlineData("C:\\Dossiers\\LPT\u00B3.txt")]
    [InlineData(@"C:\Dossiers\CONIN$")]
    [InlineData(@"C:\Dossiers\conout$")]
    [InlineData(@"\\serveur\partage\NUL.txt")]
    [InlineData(@"\\serveur\NUL\dossier")]
    [InlineData(@"C:\Dossiers\Panneau.{21EC2020-3AEA-1069-A2DD-08002B30309D}")]
    [InlineData(@"C:\Dossiers\Panneau.{21EC2020-3AEA-1069-A2DD-08002B30309D}\fichier.txt")]
    [InlineData("C:\\Dossiers\\\u202Exslx.exe")]
    [InlineData("C:\\Dossiers\\fichier\u200B.xlsx")]
    [InlineData("C:\\Dossiers\\\uFEFFfichier.xlsx")]
    [InlineData("C:\\Dossiers\\fichier\u2028.xlsx")]
    [InlineData("C:\\Dossiers\\fichier\u2029.xlsx")]
    [InlineData("C:\\Dossiers\\\u2066fichier.xlsx")]
    [InlineData("C:\\Dossiers\\fich\u00ADier.xlsx")]
    [InlineData(@"\\?\C:\Dossiers\fichier.xlsx")]
    [InlineData(@"\\?\UNC\serveur\partage\fichier.xlsx")]
    [InlineData(@"\\.\pipe\canal")]
    [InlineData(@"\\./pipe/canal")]
    [InlineData(@"\\.\C:\Dossiers")]
    [InlineData(@"\\serveur\pipe\canal")]
    [InlineData(@"\\127.0.0.1\PIPE\canal")]
    [InlineData(@"\\serveur\IPC$\canal")]
    [InlineData(@"\\serveur\mailslot\boite")]
    [InlineData(@"\\serveur")]
    [InlineData(@"\\serveur\")]
    [InlineData(@"C:\Dossiers\..\Windows\notepad.exe")]
    [InlineData(@"C:\Dossiers\.\fichier.xlsx")]
    [InlineData(@"\\serveur\partage\..\autre\fichier.xlsx")]
    [InlineData(@"C:\Dossiers\rapport.")]
    [InlineData(@"C:\Dossiers\rapport ")]
    [InlineData(@"C:\Dossiers \fichier.xlsx")]
    [InlineData(@"C:\Dossiers.\fichier.xlsx")]
    [InlineData(@"C:\Dossiers\fichier.xlsx:flux")]
    [InlineData(@"C:\Dossiers\fichier.xlsx::$DATA")]
    [InlineData("C:\\Dossiers\\fich\tier.xlsx")]
    [InlineData("C:\\Dossiers\\fichier\0.xlsx")]
    [InlineData(@"C:Dossiers\fichier.xlsx")]
    [InlineData(@"Dossiers\fichier.xlsx")]
    [InlineData(@"\Dossiers\fichier.xlsx")]
    [InlineData("")]
    public void A_path_windows_would_read_as_another_object_is_refused_on_its_text(string path) =>
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read(path).Refusal);

    /// <summary>
    /// What the second security review found still admitted (14/11/2026): a format character beyond the basic plane
    /// (a tag, a joiner of a script), a filler that stands for a letter and draws nothing; a server whose name is not
    /// written in the letters of ASCII (a letter of another script drawn as a Latin one names another server than the
    /// one read); the root of a WebDAV server written without an at sign; the files of a Linux system of the computer,
    /// which a look would start; a name that holds the mark of an object of Windows anywhere, not at its end alone.
    /// </summary>
    [Theory]
    [InlineData("C:\\Dossiers\\rapport\U000E0041.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E0001.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E007F.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U0001D173.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U0001BCA0.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U00013430.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u3164.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u115F.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u1160.pdf")]
    [InlineData("C:\\Dossiers\\rapport\uFFA0.pdf")]
    [InlineData("\\\\serv\u3164eur\\partage\\dossier")]
    [InlineData("\\\\s\u0435rveur\\partage\\dossier")]
    [InlineData("\\\\serveur-comptabilit\u00E9\\partage\\dossier")]
    [InlineData(@"\\serveur\DavWWWRoot\dossier")]
    [InlineData(@"\\127.0.0.1\davwwwroot\dossier")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\notes.txt")]
    [InlineData(@"\\WSL.LOCALHOST\Ubuntu\home")]
    [InlineData(@"\\wsl$\Ubuntu\home\notes.txt")]
    [InlineData(@"C:\Dossiers\Panneau.{21EC2020-3AEA-1069-A2DD-08002B30309D}x\fichier.txt")]
    [InlineData("C:\\Dossiers\\Panneau.{21EC2020-3AEA-1069-A2DD-08002B30309D}\u00A0")]
    [InlineData(@"C:\Dossiers\Panneau.{21EC2020-3AEA-1069-A2DD-08002B30309D}.pdf")]
    public void A_text_that_deceives_the_eye_or_reaches_another_provider_is_refused_on_its_text(string path) =>
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read(path).Refusal);

    /// <summary>Half a character alone (a surrogate without its pair) is no text Windows shows as it is written.</summary>
    [Fact]
    public void Half_a_character_alone_is_refused()
    {
        var high = (char)0xD800;
        var low = (char)0xDC00;
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read($@"C:\Dossiers\a{high}b").Refusal);
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read($@"C:\Dossiers\a{low}b").Refusal);
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read($@"C:\Dossiers\ab{high}").Refusal);
        Assert.Null(FolderPaths.Read($@"C:\Dossiers\a{high}{low}b").Refusal);
    }

    /// <summary>
    /// What the third security review found still admitted (14/11/2026): the characters Unicode says are not to be
    /// drawn and that no category of the framework tells (a selector of a variant, a joiner of graphemes, a vowel never
    /// drawn, a code point kept for them), which make two folders of one name on the screen; and a name that ends with
    /// a space that is not the plain one, which nobody sees there.
    /// </summary>
    [Theory]
    [InlineData("C:\\Dossiers\\rapport\uFE0F.pdf")]
    [InlineData("C:\\Dossiers\\rapport\uFE00.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u034F.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u17B4.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u180B.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u180F.pdf")]
    [InlineData("C:\\Dossiers\\rapport\u2065.pdf")]
    [InlineData("C:\\Dossiers\\rapport\uFFF0.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E0000.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E0080.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E0100.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E01EF.pdf")]
    [InlineData("C:\\Dossiers\\rapport\U000E0FFF.pdf")]
    [InlineData("\\\\serveur\\commun\\Contrats\uFE00\\plan.pdf")]
    [InlineData("C:\\Dossiers\\Contrats\u00A0\\plan.pdf")]
    [InlineData("C:\\Dossiers\\Contrats\u202F")]
    [InlineData("C:\\Dossiers\\Contrats\u3000\\plan.pdf")]
    [InlineData("C:\\Dossiers\\Contrats\u2003")]
    public void A_character_never_drawn_or_a_space_nobody_sees_at_the_end_of_a_name_is_refused(string path) =>
        Assert.Equal(FolderOutcome.Unsupported, FolderPaths.Read(path).Refusal);

    /// <summary>
    /// The whole table of the standard, written here apart from the product: every character Unicode says is not to be
    /// drawn (`Default_Ignorable_Code_Point`, `DerivedCoreProperties.txt`: 17 ranges, 4 174 code points) is refused in
    /// a name, but the two joiners that the letters of several scripts need.
    /// </summary>
    [Fact]
    public void Every_character_unicode_says_not_to_draw_is_refused_but_the_two_joiners()
    {
        (int First, int Last)[] undrawn =
        [
            (0x00AD, 0x00AD), (0x034F, 0x034F), (0x061C, 0x061C), (0x115F, 0x1160), (0x17B4, 0x17B5), (0x180B, 0x180F), (0x200B, 0x200F),
            (0x202A, 0x202E), (0x2060, 0x206F), (0x3164, 0x3164), (0xFE00, 0xFE0F), (0xFEFF, 0xFEFF), (0xFFA0, 0xFFA0), (0xFFF0, 0xFFF8),
            (0x1BCA0, 0x1BCA3), (0x1D173, 0x1D17A), (0xE0000, 0xE0FFF),
        ];
        Assert.Equal(4174, undrawn.Sum(range => range.Last - range.First + 1));
        var admitted = undrawn.SelectMany(range => Enumerable.Range(range.First, range.Last - range.First + 1))
            .Where(value => FolderPaths.Read($@"C:\Dossiers\a{char.ConvertFromUtf32(value)}b.pdf").Refusal != FolderOutcome.Unsupported)
            .Select(value => $"U+{value:X4}");
        Assert.Equal(["U+200C", "U+200D"], admitted);
    }

    /// <summary>
    /// What the rules above must not refuse: a character beyond the basic plane that is drawn, the same names below a
    /// share or below a drive, the drives of a remote desktop session; and, inside a name, a space that is not the
    /// plain one (the no-break space a word processor writes before a punctuation mark, the space of the scripts of
    /// Asia): it is drawn as a space, and only where a name ends does nobody see it.
    /// </summary>
    [Theory]
    [InlineData("C:\\Dossiers\\Budget 2026\u00A0!.pdf")]
    [InlineData("C:\\Dossiers\\Notes\u202Fde visite\\plan.pdf")]
    [InlineData("C:\\Dossiers\\\u4F1A\u8B70\u3000\u8CC7\u6599.pdf")]
    [InlineData("C:\\Dossiers\\plan \U0001F4C4.pdf")]
    [InlineData("C:\\Dossiers\\\U00020000\U00020001.txt")]
    [InlineData(@"\\serveur\partage\DavWWWRoot\dossier")]
    [InlineData(@"C:\Dossiers\wsl.localhost\notes.txt")]
    [InlineData(@"\\tsclient\C\Dossiers\notes.txt")]
    [InlineData(@"\\SRV-FICHIERS01\Commun$\Qualite\plan.pdf")]
    public void A_text_that_is_shown_as_it_is_written_is_read(string path) =>
        Assert.Null(FolderPaths.Read(path).Refusal);

    /// <summary>
    /// The rules of the text against Windows itself, in the tests alone: a path they admit, short names apart, is the
    /// one the framework gives back when asked for the full path, character for character. A name Windows would mend
    /// (a final dot, a device, a segment that climbs) would come back changed.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Dossiers\Musee\inventaire.xlsx")]
    [InlineData(@"C:\Dossiers")]
    [InlineData(@"X:\")]
    [InlineData(@"\\serveur\partage\dossier\fichier.xlsx")]
    [InlineData(@"\\serveur\partage")]
    [InlineData(@"\\serveur-01.exemple.org\partage\dossier")]
    [InlineData(@"\\192.168.1.20\partage\fichier.xlsx")]
    [InlineData(@"C:\Dossiers\Notes de visite\plan 2026.v2.pdf")]
    [InlineData(@"C:\Dossiers\COM10.txt")]
    [InlineData(@"C:\Dossiers\console.txt")]
    [InlineData(@"C:\Dossiers\{projet}\plan.pdf")]
    [InlineData("C:\\Dossiers\\plan \U0001F4C4.pdf")]
    [InlineData(@"\\tsclient\C\Dossiers\notes.txt")]
    public void A_path_the_rules_admit_is_the_full_path_windows_reads(string path)
    {
        var read = FolderPaths.Read(path);
        Assert.Null(read.Refusal);
        Assert.Equal(read.Item, Path.GetFullPath(read.Item));
        Assert.Equal(FolderPaths.AsFolder(read.Item), Path.GetFullPath(FolderPaths.AsFolder(read.Item)));
    }

    /// <summary>A root, as it is given to the shell: a drive or a share alone. Any folder below one is none, nor is a path the rules refuse.</summary>
    [Theory]
    [InlineData(@"C:\", true)]
    [InlineData(@"x:\", true)]
    [InlineData(@"\\serveur\partage\", true)]
    [InlineData(@"\\serveur\partage", true)]
    [InlineData(@"C:\Dossiers\", false)]
    [InlineData(@"C:\Dossiers\Musee\", false)]
    [InlineData(@"\\serveur\partage\dossier\", false)]
    [InlineData(@"\\serveur\pipe\", false)]
    [InlineData(@"\\.\C:\", false)]
    [InlineData(@"C:", false)]
    [InlineData("", false)]
    public void A_drive_or_a_share_alone_is_a_root_and_no_other_folder_is(string folder, bool root) =>
        Assert.Equal(root, FolderPaths.Root(folder));

    [Fact]
    public void A_path_of_259_characters_is_read_and_one_of_260_is_too_long_whatever_it_names()
    {
        var longest = @"C:\Dossiers\" + new string('a', FolderPaths.MaxLength - 12);
        Assert.Equal(259, longest.Length);
        Assert.Null(FolderPaths.Read(longest).Refusal);
        Assert.Equal(FolderOutcome.TooLong, FolderPaths.Read(longest + "a").Refusal);
        Assert.Equal(FolderOutcome.TooLong, FolderPaths.Read(@"\\.\pipe\" + new string('a', 260)).Refusal);
    }

    [Theory]
    [InlineData(@"C:\Dossiers\Musee", @"C:\Dossiers\Musee\")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"\\serveur\partage", @"\\serveur\partage\")]
    public void A_folder_is_given_to_the_shell_with_its_final_separator(string folder, string shown) =>
        Assert.Equal(shown, FolderPaths.AsFolder(folder));

    /// <summary>The errors of Windows (`GetLastError`) a look or the shell may give, read as what they say of the folder.</summary>
    [Theory]
    [InlineData(2, FolderLook.Missing)]
    [InlineData(3, FolderLook.Missing)]
    [InlineData(15, FolderLook.Missing)]
    [InlineData(67, FolderLook.Missing)]
    [InlineData(87, FolderLook.Missing)]
    [InlineData(123, FolderLook.Missing)]
    [InlineData(161, FolderLook.Missing)]
    [InlineData(206, FolderLook.Missing)]
    [InlineData(267, FolderLook.Missing)]
    [InlineData(1155, FolderLook.Missing)]
    [InlineData(5, FolderLook.Denied)]
    [InlineData(65, FolderLook.Denied)]
    [InlineData(86, FolderLook.Denied)]
    [InlineData(1326, FolderLook.Denied)]
    [InlineData(1327, FolderLook.Denied)]
    [InlineData(1328, FolderLook.Denied)]
    [InlineData(1329, FolderLook.Denied)]
    [InlineData(1330, FolderLook.Denied)]
    [InlineData(1331, FolderLook.Denied)]
    [InlineData(21, FolderLook.Unreachable)]
    [InlineData(53, FolderLook.Unreachable)]
    [InlineData(64, FolderLook.Unreachable)]
    [InlineData(121, FolderLook.Unreachable)]
    [InlineData(1231, FolderLook.Unreachable)]
    [InlineData(31, FolderLook.Unreachable)]
    public void An_error_of_windows_says_missing_denied_or_unreachable(int error, FolderLook look) =>
        Assert.Equal(look, FolderPaths.LookOf(error));

    /// <summary>What the shell answers when it refuses a folder, read on the error of Windows as a look is.</summary>
    [Theory]
    [InlineData(2, FolderOutcome.NotFound)]
    [InlineData(3, FolderOutcome.NotFound)]
    [InlineData(1155, FolderOutcome.NotFound)]
    [InlineData(5, FolderOutcome.Denied)]
    [InlineData(1326, FolderOutcome.Denied)]
    [InlineData(31, FolderOutcome.Unreachable)]
    [InlineData(53, FolderOutcome.Unreachable)]
    [InlineData(1223, FolderOutcome.Unreachable)]
    public void An_error_of_the_shell_says_not_found_denied_or_unreachable(int error, FolderOutcome outcome) =>
        Assert.Equal(outcome, FolderPaths.OutcomeOf(error));

    [Fact]
    public void The_failure_of_a_look_is_read_on_its_result_code_and_one_that_is_not_of_windows_is_a_missing_folder()
    {
        Assert.Equal(FolderLook.Missing, FolderPaths.LookOf(new FileNotFoundException()));
        Assert.Equal(FolderLook.Missing, FolderPaths.LookOf(new DirectoryNotFoundException()));
        Assert.Equal(FolderLook.Denied, FolderPaths.LookOf(new UnauthorizedAccessException()));
        Assert.Equal(FolderLook.Unreachable, FolderPaths.LookOf(new IOException("The network path was not found.", unchecked((int)0x80070035))));
        Assert.Equal(FolderLook.Missing, FolderPaths.LookOf(new NotSupportedException()));
        Assert.Equal(FolderLook.Missing, FolderPaths.LookOf(new ArgumentException("path")));
    }
}
