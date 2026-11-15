<!-- 014-aucun-appel-reseau-sortant.md -->
<!-- D14 : aucun appel réseau sortant, ni télémétrie, ni mise à jour, ni CDN -->

# 014 - Aucun appel réseau sortant

Statut : acceptée (date 23/10/2026)

## Contexte

La DSI d'une entreprise exige qu'une application de poste ne contacte rien à
l'extérieur ; Topogenia doit fonctionner hors réseau (NF11).

## Décision

- Aucun appel réseau sortant, quel que soit le mode : pas de télémétrie, pas de vérification
  de mise à jour, pas de CDN.
- Polices (Inter, OFL) et icônes embarquées dans l'application ; la police est aussi intégrée
  aux exports PDF (AR-52).
- Les liens de documents ne sont jamais suivis par le serveur (spec 5.4) ; ils s'ouvrent dans
  le navigateur de l'utilisateur, `rel="noopener noreferrer"`.
- La CSP `default-src 'self'` empêche le navigateur de charger une ressource externe.

## Conséquences

- Toute bibliothèque qui appelle un service tiers par défaut est écartée ou configurée pour ne
  pas le faire.
- Le mode Serveur ne contacte que la base SQL Server et l'annuaire Windows de l'entreprise.

## Vérification

- Test e2e sous CSP stricte (phase 6) : aucune requête hors de l'origine de l'application.
- Porte de contrôle Sécurité (relecture des dépendances et des en-têtes).

## Tests

`SourceRulesTests.Shipped_code_calls_no_external_address` (code C# et interface livrés), `ExtraRulesTests.Configuration_files_name_no_external_address`, et le contrôle de la maquette (`web/scripts/capture-mockup.mjs` bloque toute requête externe). Le contrôle du paquet construit (`wwwroot`) vient en phase 7.
