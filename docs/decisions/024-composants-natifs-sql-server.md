<!-- 024-composants-natifs-sql-server.md -->
<!-- Exception à la règle des licences permissives pour le composant natif livré avec le pilote SQL Server -->

# 024 - Composant natif du pilote SQL Server

Statut : acceptée (date 23/10/2026)

## Contexte

D02 impose le fournisseur EF Core SQL Server, donc le pilote `Microsoft.Data.SqlClient`. Sous
Windows, il livre `Microsoft.Data.SqlClient.SNI.runtime` (couche réseau native), sous licence
logicielle Microsoft (fichier `LICENSE.txt`) et non sous une licence permissive (spec 0.2).
Avec la version 6 du pilote, la chaîne d'authentification Azure (`Azure.Identity`, MSAL et
`Microsoft.Identity.Client.NativeInterop`) était aussi livrée, dont la licence prévoit une
collecte de données d'usage (contraire à D14).

## Décision

- Le pilote est figé en version 7.1.1 (`Directory.Packages.props`), qui ne tire plus la chaîne
  Azure : `Azure.Identity`, MSAL et `NativeInterop` ne sont plus livrés.
- `SNI.runtime` est accepté comme exception : c'est un composant de distribution de Microsoft,
  indispensable au mode Serveur imposé par la spec, sans collecte de données.

## Conséquences

- EF Core 10.0.13 est utilisé avec le pilote 7.1.1 (version plus récente que celle qu'il
  demande) : la compatibilité est prouvée par les tests de persistance sur SQL Server (phase 3).
- `THIRD-PARTY-NOTICES.md` signale l'exception.
- Le pilote tire aussi `Microsoft.SqlServer.Server` 1.0.0 (MIT, 2021, publication ancienne) : paquet de
  types d'interopérabilité figé par Microsoft, sans code exécuté par l'application ; accepté avec le pilote.

## Vérification

`node build/third-party-notices.mjs` (liste « To review ») ; tests `tests/Topogenia.Persistence.Tests`
sur SQL Server.
