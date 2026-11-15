<!-- 020-axe-core-outil-de-developpement.md -->
<!-- Q-04 : axe-core (MPL-2.0) utilisé seulement dans les tests, jamais livré -->

# 020 - axe-core, outil de développement

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 11 exige des tests d'accessibilité axe-core sur les écrans principaux (NF08, WCAG 2.2
AA). axe-core est sous licence MPL-2.0, hors de la liste permissive de la spec 0.2 (MIT,
Apache-2.0, BSD, ISC, OFL). Question Q-04 de `docs/questions.md`, défaut appliqué.

## Décision

- axe-core (et son intégration Playwright) est une dépendance de développement
  (`devDependencies`), utilisée seulement par les tests.
- Il n'entre jamais dans un paquet livré (exécutable, ZIP, MSIX, paquet Serveur) ni dans le
  code de l'interface construite.
- Il est inscrit dans `THIRD-PARTY-NOTICES.md` à la rubrique « outils de développement ».

## Conséquences

- La licence MPL-2.0 ne s'applique qu'à l'outillage de test, sans effet sur la licence du
  produit (Q3, en attente).
- Le porteur du projet peut revenir sur ce choix ; le retirer ne supprime que les tests d'accessibilité
  automatiques.

## Vérification

- Le SBOM CycloneDX des paquets livrés (P7-T03) ne contient pas axe-core.
- Porte de contrôle Interface et charte (tests axe-core verts) et Sécurité (relecture des
  dépendances livrées).

## Autres outils de développement sous MPL-2.0

`lightningcss` (MPL-2.0), tiré par Vite 8 pour traiter le CSS à la construction, relève de la
même règle : outil de développement, jamais livré (le CSS produit n'en contient rien). Il est
listé dans `THIRD-PARTY-NOTICES.md`, généré par `build/third-party-notices.mjs`.

## Autres licences des outils de développement

Les outils de développement tirent aussi des paquets sous BlueOak-1.0.0, CC0-1.0, MIT-0 et
CC-BY-4.0 (données `caniuse-lite`). Les trois premières sont des licences permissives au sens
de la spec 0.2 ; CC-BY-4.0 couvre des données de compatibilité des navigateurs utilisées à la
construction. Aucun de ces paquets n'est livré.
