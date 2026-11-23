// CollectionErrors.cs
// The code of the one fault of a template collection that a .pmap does not have, out of the closed core, with a message in each language of the host.

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// The faults of the envelope of a collection reuse the codes of the faults of a `.pmap` (`FileErrorCodes`); the one that a
/// `.pmap` cannot have is here, out of the closed core (ADR 114), like <c>DocumentFolderErrors</c> and <c>AssistantErrors</c>.
/// </summary>
public static class CollectionErrors
{
    /// <summary>The collection holds more processes than <see cref="PmapFormat.ProcessesMax"/> (`count`, `max`).</summary>
    public const string TooManyProcesses = "COLLECTION_TROP_DE_PROCESSUS";
}
