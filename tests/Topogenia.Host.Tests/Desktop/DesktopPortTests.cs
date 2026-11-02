// DesktopPortTests.cs
// The port of the desktop mode: the one configured, else the same at each start for an account and its data file, else a free one (spec 5.2, ADR 089, audit 3.2).

using System.Net;
using System.Net.Sockets;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopPortTests
{
    private const string Account = "S-1-5-21-1000";

    [Fact]
    public void The_port_of_an_account_and_a_data_file_is_the_same_at_each_start_in_its_range_and_another_for_another_file_or_account()
    {
        var port = DesktopPort.StableFor(@"C:\Users\camille\AppData\Local\Topogenia\data\topogenia.db", Account);
        Assert.InRange(port, DesktopPort.First, DesktopPort.First + DesktopPort.Range - 1);
        Assert.Equal(port, DesktopPort.StableFor(@"c:\users\CAMILLE\appdata\local\topogenia\DATA\topogenia.db", Account));
        Assert.NotEqual(port, DesktopPort.StableFor(@"C:\Users\camille\AppData\Local\Packages\PeterBarcauti.Topogenia_x\LocalState\Topogenia\data\topogenia.db", Account));
        Assert.NotEqual(port, DesktopPort.StableFor(@"C:\Users\camille\AppData\Local\Topogenia\data\topogenia.db", "S-1-5-21-1001"));
    }

    [Fact]
    public void A_port_configured_is_kept_and_a_stable_port_taken_or_refused_gives_a_free_port_chosen_at_the_start()
    {
        const string file = @"C:\Data\topogenia.db";
        var stable = DesktopPort.StableFor(file, Account);
        Assert.Equal(5123, DesktopPort.Of(new DesktopOptions { Port = 5123 }, file, Account, _ => false));
        Assert.Equal(stable, DesktopPort.Of(new DesktopOptions(), file, Account, port => port == stable));
        Assert.Equal(0, DesktopPort.Of(new DesktopOptions(), file, Account, _ => false));
    }

    [Fact]
    public void A_stable_port_where_another_program_listens_is_seen_taken()
    {
        const string file = @"C:\Data\pris.db";
        using var taken = new TcpListener(IPAddress.Loopback, DesktopPort.StableFor(file, Account));
        try
        {
            taken.Start();
        }
        catch (SocketException)
        {
            // Another program of the machine listens there already: taken all the same.
        }
        Assert.Equal(0, DesktopPort.Of(new DesktopOptions(), file, Account));
    }
}
