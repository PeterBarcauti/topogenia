<!-- 012-anglais-dans-le-code.md -->
<!-- D12 : code, commentaires et API en anglais ; interface en français par défaut -->

# 012 - Anglais dans le code

Statut : acceptée (date 23/10/2026)

## Contexte

Le code 1.x est en français. La 2.0 vise une publication GitHub (privée) et une équipe
internationale (une développeuse React dans une autre équipe) ; le modèle de données est déjà en anglais.
Confirmé par le porteur du projet le 22/10/2026 (spec Q1).

## Décision

- Identifiants du code, commentaires, noms d'API, routes de l'interface (AR-30) en anglais.
- Vocabulaire métier fixé par le glossaire de la spec 3.3 (`ProcessMap`, `Lane`, `Step`,
  `Flow`, `StepSheet`, `PainPoint`, `Dissolution`...).
- Interface en français par défaut, anglais disponible ; textes dans les fichiers i18n ; la
  liste des langues vient de la configuration (AR-29), le repli va à la langue par défaut
  (AR-51).
- Les codes d'erreur stables gardent leur forme 1.x (par exemple `OPERATION_DEPASSEE`) : ce
  sont des données du contrat, traduites par les messages.
- La documentation du dossier `docs/` reste en français.

## Conséquences

- Les tests 1.x sont portés par intention et renommés en anglais (spec 11).
- La table de correspondance des routes 1.x → 2.0 est tenue dans `docs/couverture.md`.

## Vérification

- Porte de contrôle Architecture (nommage, glossaire 3.3).
- Test de l'hôte vérifiant que chaque code d'erreur a un message dans chaque langue (spec
  3.6, phase 4).
