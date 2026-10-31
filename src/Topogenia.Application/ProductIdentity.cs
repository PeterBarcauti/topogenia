// ProductIdentity.cs
// The product name: the only place in the code where it is written (D13), outside the closed core so that the public repository builds with its package (ADR 048, ADR 074).

namespace Topogenia.Application;

/// <summary>Product identity. The build reads <see cref="Name"/> to name the executable; folders and the package format derive from it.</summary>
public static class ProductIdentity
{
    public const string Name = "Topogenia";
}
