// SampleProjectEnglish.cs
// The English of every text of the fictional project of the samples: the sample written in English (samples/leave), and the English translations of the French one.

namespace Topogenia.Interchange.Tests.Support;

/// <summary>
/// Each French text of <see cref="SampleProject"/> and its English. One dictionary for both samples: the English one is
/// written with these texts, the French one is translated with them; a text missing here fails the sample at once.
/// </summary>
public static class SampleProjectEnglish
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
    {
        // The project, its maps, its lanes.
        ["Demande de congés"] = "Leave request",
        ["Du besoin du salarié à l'enregistrement en paie."] = "From the employee's need to the payroll record.",
        ["\\\\partage-fictif\\conges"] = "\\\\fictitious-share\\leave",
        ["Équipe RH"] = "HR team",
        ["congé payé = paid leave\nsolde de congés = leave balance"] = "paid leave = congé payé\nleave balance = solde de congés",
        ["Salarié"] = "Employee",
        ["Manager"] = "Manager",
        ["Ressources humaines"] = "Human resources",
        ["Logiciel de paie"] = "Payroll software",
        ["Contrôle de gestion"] = "Financial control",
        ["Prestataire"] = "Contractor",
        // The steps.
        ["Besoin de congés"] = "Leave needed",
        ["Saisir la demande"] = "Enter the request",
        ["Demande conforme ?"] = "Request compliant?",
        ["Valider la demande"] = "Approve the request",
        ["Corriger la demande"] = "Correct the request",
        ["Enregistrer dans la paie"] = "Record in payroll",
        ["Congés posés"] = "Leave booked",
        ["Début de saisie"] = "Start of entry",
        ["Remplir le formulaire"] = "Fill in the form",
        ["En même temps"] = "At the same time",
        ["Joindre le justificatif"] = "Attach the supporting document",
        ["Prévenir l'équipe"] = "Notify the team",
        ["Fin des tâches simultanées"] = "End of parallel tasks",
        ["Pièces à fournir ?"] = "Documents to provide?",
        ["Attendre l'accusé de réception"] = "Wait for the acknowledgement",
        ["Fin des pièces"] = "End of documents",
        ["Demande envoyée"] = "Request sent",
        ["Contrôler les soldes"] = "Check the balances",
        ["Lire le solde"] = "Read the balance",
        ["Comparer à la demande"] = "Compare with the request",
        ["Archiver le papier"] = "Archive the paper",
        ["Joindre un justificatif"] = "Attach a supporting document",
        // The labels of the flows.
        ["oui"] = "yes",
        ["non"] = "no",
        ["justificatif"] = "supporting document",
        ["aucun"] = "none",
        ["archive"] = "archive",
        // The sheets, their operations and their versions.
        ["Gestionnaire RH"] = "HR officer",
        ["Manager direct"] = "Line manager",
        ["Accorder ou refuser la demande"] = "Grant or refuse the request",
        ["Messagerie"] = "Email",
        ["Vérifier le planning de l'équipe avant de répondre.\nRépondre sous 48 h."] = "Check the team schedule before answering.\nAnswer within 48 h.",
        ["Ouvrir la demande"] = "Open the request",
        ["Consulter le planning"] = "Check the schedule",
        ["Accorder ou refuser"] = "Grant or refuse",
        ["Ouvrir"] = "Open",
        ["Décrire la demande"] = "Describe the request",
        ["Préciser le délai de prévenance"] = "Specify the notice period",
        ["Choisir les dates"] = "Choose the dates",
        ["Formulaire"] = "Form",
        ["Archiviste"] = "Archivist",
        // The documents.
        ["Relevé des soldes"] = "Balance statement",
        ["Formulaire de demande"] = "Request form",
        ["https://exemple.invalid/formulaire"] = "https://example.invalid/form",
        ["Planning d'équipe"] = "Team schedule",
        ["\\\\partage-fictif\\planning.xlsx"] = "\\\\fictitious-share\\schedule.xlsx",
        ["Bordereau d'archive"] = "Archive slip",
        // The pain points and their history.
        ["Soldes recalculés à la main"] = "Balances recalculated by hand",
        ["Relances multiples du manager"] = "Repeated reminders to the manager",
        ["Chiffré en atelier"] = "Costed in a workshop",
        ["Aucun outil unique pour les absences"] = "No single tool for absences",
        ["Papier égaré"] = "Lost paper",
    };

    /// <summary>The English of a French text of the sample.</summary>
    public static string Of(string french) =>
        Texts.TryGetValue(french, out var english) ? english : throw new KeyNotFoundException($"The sample has no English for « {french} ».");
}
