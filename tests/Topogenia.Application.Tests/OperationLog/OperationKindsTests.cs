// OperationKindsTests.cs
// The kinds of the operation log are the codes of the 1.x `historique.action.*`, and the new ones of 2.0, each once (spec 4.6, S-4.6-02, AR-155).

using Topogenia.Application.OperationLog;

namespace Topogenia.Application.Tests.OperationLog;

public sealed class OperationKindsTests
{
    /// <summary>The keys of `historique.action` in the 1.x `web/src/i18n/fr.json`, in their order.</summary>
    private static readonly string[] Legacy =
    [
        "inserer", "supprimer_detail", "creer_etape", "modifier_etape", "deplacer", "supprimer", "creer_lien", "modifier_lien",
        "commande", "creer_couloir", "modifier_couloir", "ordre_couloirs", "detailler", "raci", "coller", "deplacer_bloc",
        "regrouper", "aligner", "remonter", "affecter", "terminer_remontee",
    ];

    /// <summary>
    /// The operations the 1.x did not have: the reversal of a flow (2.0, AR-155) and a proposal of the assistant applied to
    /// a map (3.0, ADR 079).
    /// </summary>
    private static readonly string[] New = ["inverser_lien", "proposition_ia"];

    /// <summary>The constants of the kinds, read by the test: the application keeps no list of them.</summary>
    private static string[] Constants() =>
        typeof(OperationKinds).GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToArray();

    [Fact]
    public void The_kinds_are_the_codes_of_the_1x_history() =>
        Assert.Equal(Legacy.Concat(New).Order(StringComparer.Ordinal), Constants().Order(StringComparer.Ordinal));

    [Fact]
    public void Every_constant_is_listed_once()
    {
        var constants = Constants();
        Assert.Equal(Legacy.Length + New.Length, constants.Length);
        Assert.Equal(constants.Length, constants.Distinct(StringComparer.Ordinal).Count());
    }
}
