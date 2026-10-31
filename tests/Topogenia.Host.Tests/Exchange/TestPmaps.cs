// TestPmaps.cs
// .pmap files for the tests of the routes: the smallest valid map, and a map with a sheet block.

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
}
