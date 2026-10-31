// DataProtectionSecretsTests.cs
// The protection of the secret of the assistant by the data protection of ASP.NET Core: the protected text never holds the secret, the keys of this installation alone read it, anything else reads as nothing (spec 3.0 section 4, ADR 077).

using Microsoft.AspNetCore.DataProtection;
using Topogenia.Host.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class DataProtectionSecretsTests
{
    private const string Secret = "sk-ant-test-protected-0123";

    [Fact]
    public void A_secret_protected_is_read_back_by_the_same_keys_and_never_shows_in_its_protected_text()
    {
        var secrets = new DataProtectionSecrets(new EphemeralDataProtectionProvider());
        var protectedSecret = secrets.Protect(Secret);
        Assert.DoesNotContain(Secret, protectedSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("0123", protectedSecret, StringComparison.Ordinal);
        Assert.Equal(Secret, secrets.Unprotect(protectedSecret));
    }

    [Fact]
    public void A_text_of_other_keys_of_another_purpose_or_damaged_reads_as_nothing_never_as_an_error()
    {
        var provider = new EphemeralDataProtectionProvider();
        var secrets = new DataProtectionSecrets(provider);
        var protectedSecret = secrets.Protect(Secret);
        // The keys of another installation, or of another account (keys lost: the dialog asks for the secret again).
        Assert.Null(new DataProtectionSecrets(new EphemeralDataProtectionProvider()).Unprotect(protectedSecret));
        // A text protected by another protector of the host is never read as a secret of the assistant.
        Assert.Null(secrets.Unprotect(provider.CreateProtector("Another.Purpose").Protect(Secret)));
        Assert.Null(secrets.Unprotect(protectedSecret[..^4] + "AAAA"));
    }
}
