<!-- 009-contrat-openapi-types-generes.md -->
<!-- D09 : contrat d'API OpenAPI généré par l'hôte, types et client TypeScript générés -->

# 009 - Contrat OpenAPI et types générés

Statut : acceptée (date 23/10/2026)

## Contexte

En 1.x, les types TypeScript sont recopiés à la main depuis les DTO C# ; les deux côtés
dérivent sans que rien ne le signale.

## Décision

- Le contrat d'API est décrit en OpenAPI, généré par l'hôte (`Topogenia.Host`).
- Les types et le client TypeScript (`web/src/api/`) sont générés depuis ce contrat (par
  exemple `openapi-typescript` plus un petit client maison) ; plus aucun type d'API écrit à
  la main.
- Le client porte les erreurs typées `{code, message, details}`, l'intercepteur `If-Match`
  (ADR 006) et l'en-tête anti-CSRF `X-Requested-With` (AR-13).

## Conséquences

- Une modification d'un DTO casse la compilation TypeScript au lieu d'un parcours en
  production.
- Le fichier OpenAPI et le code généré sont exclus des règles de taille (code généré).
- Le générateur choisi suit la règle de dépendance de la spec 0.2.

## Vérification

- Un contrôle de construction régénère le client et échoue si le résultat diffère du fichier
  versionné (phase 6, P6-T01).
- `tsc` en mode `strict` sans erreur ; porte de contrôle Architecture (aucun type d'API
  écrit à la main dans `web/src/`).
