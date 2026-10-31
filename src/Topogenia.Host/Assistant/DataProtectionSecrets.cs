// DataProtectionSecrets.cs
// The protection of the secret of the assistant by the data protection of ASP.NET Core: the keys of the folder of the product, DPAPI of the account in desktop mode (spec 3.0 section 4).

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Topogenia.Application.Ports;

namespace Topogenia.Host.Assistant;

/// <summary>
/// <see cref="ISecretProtector"/> by a protector of its own purpose: the database keeps only the protected text, which the
/// keys of this installation alone can read. A text that can no longer be read (keys lost or of another account) gives
/// null: the dialog then asks for the secret again, never an error of the request.
/// </summary>
public sealed class DataProtectionSecrets(IDataProtectionProvider provider) : ISecretProtector
{
    /// <summary>The purpose of the protector: a secret of the assistant is never read by another protector of the host.</summary>
    public const string Purpose = Application.ProductIdentity.Name + ".Assistant.Secret";

    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    public string Protect(string secret) => protector.Protect(secret);

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
