<!-- 011-echange-paquet-topogenia.md -->
<!-- D11 : paquet .topogenia à fidélité complète, intégrité par SHA-256, .pmap gardé en format léger -->

# 011 - Échange par paquet `.topogenia`

Statut : acceptée (date 23/10/2026)

## Contexte

Deux besoins distincts : transférer ou sauvegarder un projet complet entre les modes Poste et
Serveur (objectif O2), et garder un brouillon léger, lisible et écrivable par une IA. Le
`.pmap` 1.x ne porte ni fiches, ni documents, ni irritants. La spec écrit « ZIP signé par
empreintes » sans prévoir d'infrastructure de clés.

## Décision

- Paquet `.topogenia` : ZIP contenant `manifest.json` et des entrées JSON versionnées
  (projet, membres, cartes, fiches, documents, irritants, traductions), schéma publié dans
  `schemas/topogenia-package-2.0.schema.json`, sérialisation déterministe (spec 7.2).
- Intégrité par empreinte SHA-256 de chaque entrée dans le manifeste, vérifiée avant toute
  écriture. **Pas d'authenticité** : aucune signature cryptographique du paquet en 2.0
  (AR-35). Ce point tient lieu d'ADR sur la signature.
- Import : aperçu obligatoire dans l'interface, validations refaites par l'API (AR-38), tout
  ou rien, montée de version par convertisseurs, refus d'un paquet plus récent.
- Export « sans membres ni auteurs » coché par défaut (AR-34).
- `.pmap` reste le format léger (versions 1.0 et 1.1, spec 7.5) ; les sous-cartes vides vont
  dans le paquet, pas dans le `.pmap` (AR-09).

## Conséquences

- Une altération accidentelle (troncature, corruption) est détectée ; une modification
  volontaire suivie d'un recalcul des empreintes ne l'est pas. Le paquet se transmet donc par
  des canaux de confiance de l'entreprise ; une signature pourra s'ajouter par une ADR
  ultérieure si une infrastructure de clés est fournie.
- Ni la corbeille, ni le journal des opérations, ni les lots ne sont exportés.

## Vérification

- `tests/Topogenia.Interchange.Tests/` (phase 5) : aller-retour exact, croisé SQLite →
  SQL Server → SQLite, paquets corrompus, tronqués, à empreinte fausse, à chemin dangereux,
  trop gros (spec 7.4).
- Porte de contrôle Sécurité (paquets hostiles).
