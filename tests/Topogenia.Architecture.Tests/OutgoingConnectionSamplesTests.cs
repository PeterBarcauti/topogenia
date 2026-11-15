// OutgoingConnectionSamplesTests.cs
// Further samples of the detector of outgoing connections (D14): the named pipes that reach another machine.

namespace Topogenia.Architecture.Tests;

public sealed class OutgoingConnectionSamplesTests
{
    private static bool OpensConnection(string sample) =>
        ShippedCodeDetectors.OutgoingConnections(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")).Any();

    [Theory]
    [InlineData("using System.IO.Pipes; class A { object M() => new NamedPipeClientStream(\"remote-server\", \"pipe\", PipeDirection.InOut); }")]
    [InlineData("class A { void M(System.IO.Pipes.NamedPipeClientStream pipe) => pipe.Connect(1000); }")]
    public void A_named_pipe_client_is_an_outgoing_connection(string sample) => Assert.True(OpensConnection(sample));

    [Theory]
    [InlineData("using System.IO.Pipes; class A { object M() => new AnonymousPipeServerStream(PipeDirection.Out); }")]
    [InlineData("class A { object M() => new System.IO.MemoryStream(); }")]
    public void An_anonymous_pipe_and_a_stream_in_memory_are_no_outgoing_connection(string sample) => Assert.False(OpensConnection(sample));
}
