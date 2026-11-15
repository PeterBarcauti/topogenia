// AssistantConnectors.cs
// What each connector of the assistant asks and accepts (ADR 098): whether it calls a model, takes a key, a resource of Azure or a port of this machine, reads images and PDF files; no address here (the table of the providers lives with the connectors).

using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>The facts of the connectors, the same for the rules of the settings, the exchanges and the files joined.</summary>
public static class AssistantConnectors
{
    /// <summary>The connectors that call a model: all but none and the copy and paste.</summary>
    public static bool CallsModel(AssistantConnector connector) => connector is not (AssistantConnector.None or AssistantConnector.CopyPaste);

    /// <summary>
    /// The connector as the settings show it. The copy and paste, a choice of its own from 3.0 to 3.1, does what none does:
    /// nothing leaves, the prompt is copied into an AI and its reply pasted back. One choice is offered since 3.2 (lot J10,
    /// asked by the project owner): a setting kept as copy and paste is still read and still accepted, and shown as none.
    /// </summary>
    public static AssistantConnector Shown(AssistantConnector connector) => connector == AssistantConnector.CopyPaste ? AssistantConnector.None : connector;

    /// <summary>The codes of the connectors a person chooses among, in their order: every one but the copy and paste (<see cref="Shown"/>).</summary>
    public static IReadOnlyList<string> Offered() =>
        [.. Enum.GetValues<AssistantConnector>().Where(connector => connector != AssistantConnector.CopyPaste).Select(Codes.Of)];

    /// <summary>The connectors that take a key of an API: Bedrock reads an AWS profile, a model on this machine takes none.</summary>
    public static bool TakesKey(AssistantConnector connector) =>
        CallsModel(connector) && connector is not (AssistantConnector.Bedrock or AssistantConnector.Ollama or AssistantConnector.LmStudio);

    /// <summary>The connectors of the Chat Completions API of OpenAI, served by one connector read in the table of the providers.</summary>
    public static bool SpeaksChatCompletions(AssistantConnector connector) =>
        CallsModel(connector) && connector is not (AssistantConnector.Anthropic or AssistantConnector.Bedrock);

    /// <summary>A model on this machine (or on the server of the application in server mode): nothing leaves it.</summary>
    public static bool OnThisMachine(AssistantConnector connector) => connector is AssistantConnector.Ollama or AssistantConnector.LmStudio;

    /// <summary>The port of a model on this machine when the settings give none: the one each program listens on by default.</summary>
    public static int? DefaultPort(AssistantConnector connector) => connector switch
    {
        AssistantConnector.Ollama => 11434,
        AssistantConnector.LmStudio => 1234,
        _ => null,
    };

    /// <summary>The connectors whose models read an image joined: Claude, and the providers that document it for their current models.</summary>
    public static bool ReadsImages(AssistantConnector connector) => connector is AssistantConnector.Anthropic or AssistantConnector.Bedrock
        or AssistantConnector.OpenAi or AssistantConnector.AzureOpenAi or AssistantConnector.Gemini or AssistantConnector.Grok or AssistantConnector.Mistral;

    /// <summary>The connectors that read a PDF file joined as it is: Claude and OpenAI; the others get Word, Excel and Visio as their text only.</summary>
    public static bool ReadsPdf(AssistantConnector connector) => connector is AssistantConnector.Anthropic or AssistantConnector.Bedrock or AssistantConnector.OpenAi;
}
