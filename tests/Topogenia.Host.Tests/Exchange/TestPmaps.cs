// TestPmaps.cs
// .pmap files for the tests of the routes: the smallest valid map, a map with a sheet block, a map with comments, and a collection of them (ADR 114).

namespace Topogenia.Host.Tests.Exchange;

/// <summary>Fictional `.pmap` 1.1 files, as a person or an AI would write them.</summary>
public static class TestPmaps
{
    /// <summary>Two lanes, three steps, two flows, no column (automatic placement).</summary>
    public const string Minimal = """
        {
          "format": "topogenia",
          "version": "1.1",
          "project": { "name": "Demande d'achat" },
          "map": {
            "lanes": [{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "Acheteur", "type": "department" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Saisir la demande" },
              { "id": "e2", "lane": "c2", "label": "Passer la commande" },
              { "id": "e3", "lane": "c2", "label": "Commande envoyée", "type": "end" }
            ],
            "flows": [{ "source": "e1", "target": "e2" }, { "source": "e2", "target": "e3" }]
          }
        }
        """;

    /// <summary>A map whose first step carries a sheet block, and whose second step uses its output, with an uncertainty.</summary>
    public const string WithSheet = """
        {
          "format": "topogenia",
          "version": "1.1",
          "project": { "code": "ACHA", "name": "Achats", "translations": { "en": "Purchasing" } },
          "map": {
            "lanes": [{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "Acheteur" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Saisir la demande", "column": 0, "translations": { "en": "Enter the request" },
                "sheet": { "purpose": "Exprimer le besoin", "frequency": "weekly", "duration_minutes": 10,
                           "procedure": [{ "text": "Remplir le formulaire", "nature": "re_enter" }],
                           "outputs": [{ "id": "d1", "name": "Demande d'achat" }] } },
              { "id": "e2", "lane": "c2", "label": "Passer la commande", "column": 1,
                "sheet": { "uses": [{ "document": "d1", "wait_hours": 8, "pain_point": "La demande arrive incomplète." }] } }
            ],
            "flows": [{ "source": "e1", "target": "e2", "label": "valide", "translations": { "en": "valid" } }]
          },
          "uncertainties": ["Le délai de 8 heures est à confirmer."]
        }
        """;

    /// <summary>A template collection `.pmaps` 1.0 (ADR 114) around the `.pmap` files given, each kept as it is.</summary>
    public static string Collection(params string[] processes) => $$"""
        {
          "format": "topogenia-collection",
          "version": "1.0",
          "name": "Achats",
          "description": "Les processus d'achat.",
          "processes": [{{string.Join(", ", processes)}}]
        }
        """;

    /// <summary><see cref="Minimal"/> under a code of its own.</summary>
    public static string MinimalCoded(string code) =>
        Minimal.Replace("\"project\": { \"name\"", $"\"project\": {{ \"code\": \"{code}\", \"name\"", StringComparison.Ordinal);

    /// <summary>
    /// A file 1.5: a comment of presentation on the first step (with its English line) and on the flow, a glossary, and
    /// two uses of outputs of the first step, one with a pain point still reported, one with a pain point dropped.
    /// </summary>
    public const string WithComments = """
        {
          "format": "topogenia",
          "version": "1.5",
          "project": { "code": "COMM", "name": "Commentaires", "glossary": "demande = request" },
          "map": {
            "lanes": [{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "Acheteur" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Saisir la demande", "column": 0,
                "comment": { "text": "Tout part d'ici.", "translations": { "en": "It all starts here." } },
                "sheet": { "outputs": [{ "id": "d1", "name": "Demande d'achat" }, { "id": "d2", "name": "Devis" }] } },
              { "id": "e2", "lane": "c2", "label": "Passer la commande", "column": 1,
                "sheet": { "uses": [
                  { "document": "d1", "pain_point": "La demande arrive incomplète.", "pain_point_status": "reported" },
                  { "document": "d2", "pain_point": "Le devis est illisible.", "pain_point_status": "dropped" } ] } }
            ],
            "flows": [{ "source": "e1", "target": "e2", "comment": { "text": "La demande change de mains." } }]
          }
        }
        """;
}
