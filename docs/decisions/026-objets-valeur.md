<!-- 026-objets-valeur.md -->
<!-- Les objets valeur de la spec 3.3 portent la règle d'une valeur saisie ; les enregistrements gardent des types simples -->

# 026 - Objets valeur du domaine

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 3.3 demande des objets valeur : `ProjectCode`, `ReadableCode`, `Cell`, `Color`,
`LocalizedText`, `Frequency`, `Duration`, `DocumentLocation`. À la fin de la phase 2, leurs
règles étaient éparpillées dans des fonctions statiques (`ReadableCodes`,
`LaneColors.Normalise`, `DocumentRules.Address`, `UserRules.IsValidLogin`, bornes de durée
dans `SheetContentReader`), parfois écrites deux fois. Les enregistrements du modèle
(`Project`, `Lane`, `Document`, `AppUser`, `StepSheet`) sont lus et écrits par EF Core
(D02) et par le paquet `.topogenia` (D11) : des types propres dans leurs propriétés
imposeraient des convertisseurs dans les deux fournisseurs et dans les formats d'échange.

## Décision

Chaque valeur saisie a un objet valeur dans `src/Topogenia.Domain/Values/`, un
`readonly record struct` dont le constructeur est privé : on ne l'obtient que par `Parse`
(refus par une `DomainException` et son code) ou, quand un appelant en a besoin, `TryParse`
(vrai ou faux, sans refus), qui portent la règle. Une valeur obtenue est donc toujours valide.

| Objet valeur | Règle | Refus de `Parse` |
|---|---|---|
| `ProjectCode` | 3 à 6 majuscules A à Z, après trim (annexe A 2.1) | `PROJET_CODE_INVALIDE` |
| `ReadableCode` | `PRJ-E001`, `PRJ-C01`, `PRJ-I001`, `PRJ-S01`, forme exacte ; `TryParseStep` lit un code tapé dans la barre de commande (annexe A 2.6) | `VALEUR_INVALIDE` |
| `LaneColor` | `#RRGGBB`, mis en majuscules (AR-11) | `VALEUR_INVALIDE` |
| `LocalizedText` | texte saisi (règles de `TextRules`) et sa langue d'origine, par `Parse` seulement ; `TakesTranslationIn` est la seule implémentation de « une traduction dans la langue d'origine est ignorée », appelée par la saisie manuelle, l'import et l'export des traductions (les traductions elles-mêmes sont des enregistrements `Translation`) | codes de `TextRules` |
| `Duration` | minutes de 0 à 100 000, arrondies à 2 décimales (annexe A 2.10) | `VALEUR_INVALIDE` |
| `DocumentLocation` | URL http(s) sans espace ou chemin Windows (UNC, lecteur), 1 000 caractères (annexe A 2.11) | `ADRESSE_INVALIDE` |
| `Login` | `DOMAINE\compte`, 128 caractères, sans `@` ; forme normalisée en majuscules (annexe A 2.1, spec 4.1) | `MEMBRE_LOGIN_INVALIDE` |

`Cell` (case) et les listes fermées (`Frequency`...) existaient déjà dans `Values`. Les
enregistrements gardent des types simples (texte, nombre) : une opération passe la valeur
saisie par l'objet valeur puis écrit `Value` (ou `Minutes`, `Address`...) dans
l'enregistrement. Les anciennes fonctions disparaissent (`ReadableCodes`,
`LaneColors.Normalise`, `UserRules.IsValidLogin`, `UserRules.Normalise`,
`UserRules.ShortName`) ou délèguent (`DocumentRules.Address`, `ProjectCodeRules.Check`).

## Conséquences

- Une règle de valeur a une seule implémentation (spec 3.3, « fin des duplications ») ; le
  domaine, l'Application (phase 4) et les formats (phase 5) l'appellent de la même façon.
- La persistance et les formats n'ont aucun convertisseur à écrire : le schéma garde les
  colonnes de la spec 4.1.
- Un enregistrement peut, en théorie, recevoir une valeur non validée si une opération
  oublie l'objet valeur : les tests de chaque opération vérifient leurs refus, et les index
  et `CHECK` de la base restent les garde-fous de dernier recours (spec 4.2).
- `LaneColors` garde les huit teintes (AR-45) et `Nearest`, qui convertit les six couleurs
  de la 1.x à la migration (phase 5).

## Vérification

`tests/Topogenia.Domain.Tests/Values/ValueObjectTests.cs` (une théorie par règle, valeurs
acceptées et refusées, conversion des six couleurs 1.x) ; les tests des opérations
(`ProjectCodeRulesTests`, `UserRulesTests`, `DocumentRulesTests`, `CommandLineParserTests`,
`SheetContentReaderTests`) passent par les objets valeur.
