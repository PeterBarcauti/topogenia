// ProductIdentity.cs
// The product name and the two addresses of its site and of its page of the Store: the only place in the code where they are written (D13, ADR 111), outside the closed core so that the public repository builds with its package (ADR 048, ADR 074).

namespace Topogenia.Application;

/// <summary>Product identity. The build reads <see cref="Name"/> to name the executable; folders and the package format derive from it.</summary>
public static class ProductIdentity
{
    public const string Name = "Topogenia";

    /// <summary>
    /// The site of the product (ADR 111 point 2): the base of the pages that the browser opens on a click (the versions,
    /// the gallery of templates), never called by the application. The only file of the shipped code, with the table of
    /// the providers of the assistant, where an address is written; its host is listed in `docs/securite.md`.
    /// </summary>
    public const string Site = "https://topogenia.com";

    /// <summary>The page of the product in the Microsoft Store, by the identifier of the product alone (ADR 088, ADR 111 point 4).</summary>
    public const string StorePage = "https://apps.microsoft.com/detail/9NZ56VV4N6QR";
}
