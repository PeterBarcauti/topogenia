// OperationKinds.cs
// The kinds of the logged operations: the codes of the 1.x `historique.action.*`, written once (spec 4.6, S-4.6-02).

namespace Topogenia.Application.OperationLog;

/// <summary>
/// The `kind` of an entry of the operation log: the same codes as the 1.x `historique.action.*`
/// (`web/src/i18n/fr.json`, `action`), so that the interface names the operation undone or redone (the message
/// "undone: {action}", annex B 2.6). They are data, not identifiers of the code: they stay in French as the 1.x wrote them.
/// </summary>
public static class OperationKinds
{
    public const string InsertStep = "inserer";
    public const string DeleteDetail = "supprimer_detail";
    public const string CreateStep = "creer_etape";
    public const string UpdateStep = "modifier_etape";
    public const string Move = "deplacer";
    public const string Delete = "supprimer";
    public const string CreateFlow = "creer_lien";
    public const string UpdateFlow = "modifier_lien";
    public const string Command = "commande";
    public const string CreateLane = "creer_couloir";
    public const string UpdateLane = "modifier_couloir";
    public const string LaneOrder = "ordre_couloirs";
    public const string Detail = "detailler";
    public const string Raci = "raci";
    public const string Paste = "coller";
    public const string MoveBlock = "deplacer_bloc";
    public const string Group = "regrouper";
    public const string Align = "aligner";
    public const string Dissolve = "remonter";
    public const string Assign = "affecter";
    public const string FinishDissolution = "terminer_remontee";

    /// <summary>New in 2.0, without a 1.x code: a flow reversed (AR-155), written in French as the others.</summary>
    public const string ReverseFlow = "inverser_lien";

    /// <summary>New in 3.0: a proposal of the assistant applied to a map (ADR 079), one operation undone by one undo.</summary>
    public const string Proposal = "proposition_ia";
}
