<!-- CONTRIBUTING.md -->
<!-- Rules for contributing: language, layout, tests, dependencies, commits -->

# Contribuer

- **Langues** : code, identifiants, commentaires et messages de commit en anglais ;
  documentation et interface française en français, interface traduisible (fichiers i18n).
- **Architecture** : Domaine pur, Application, adaptateurs (Persistance, Échange, Hôte). Les
  règles de dépendance, de taille (600 lignes par fichier, 80 par méthode) et de forme sont
  vérifiées par `tests/Topogenia.Architecture.Tests`.
- **En-tête** : chaque fichier commence par deux lignes de commentaire, son nom puis sa
  description (sans auteur ni date).
- **Tests** : tout comportement nouveau ou modifié arrive avec son test ; la suite complète
  passe sur SQLite et SQL Server, avec 0 avertissement. Un test rouge se corrige dans le code,
  jamais en affaiblissant le test.
- **Dépendances** : licence permissive (MIT, Apache-2.0, BSD, ISC, OFL pour les polices),
  publication dans les 12 derniers mois, version figée, inscrite dans
  `THIRD-PARTY-NOTICES.md`.
- **Cœur fermé** : le domaine (`src/Topogenia.Domain`) et ses tests sont sous licence propriétaire (ADR 048) :
  aucune contribution externe n'y est reçue ; le reste du dépôt est sous Apache-2.0. Le dépôt public ne porte pas leurs
  sources : il compile avec le paquet `Topogenia.Core`, remis avec la licence écrite du titulaire et posé dans
  `artifacts/core-feed` (ADR 074) ; les règles d'architecture qui lisent les sources du cœur y sont sautées, avec leur
  raison ; deux d'entre elles portent aussi sur l'application, la persistance et l'hôte (qui seuls appellent
  `RecordKey.Of` et `Stamp` où il le faut) : ces couches n'y sont vérifiées que dans la copie de travail.
- **Données** : exemples fictifs uniquement ; aucune donnée réelle d'entreprise ou de personne.
- **Forme** : UTF-8, pas d'emoji, fins de ligne CRLF pour les `.bat` et `.cmd`.
- **Décisions** : un choix d'architecture s'écrit en ADR courte dans `docs/decisions/`.
- **Télémétrie des outils** : le SDK .NET et la plateforme de tests envoient des données d'usage
  par défaut. Avant de construire ou de tester, définir `DOTNET_CLI_TELEMETRY_OPTOUT=1` et
  `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` (l'intégration continue le fait déjà). L'application elle-même
  n'envoie rien (D14).
