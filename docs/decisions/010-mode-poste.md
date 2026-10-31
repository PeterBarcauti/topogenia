<!-- 010-mode-poste.md -->
<!-- D10 : mode Poste en exécutable autonome sur 127.0.0.1, protégé par un jeton de lancement -->

# 010 - Mode Poste

Statut : acceptée (date 23/10/2026)

## Contexte

L'installation 1.x sur poste exige un SQL Server local. La 2.0 doit s'installer sans droits
d'administrateur, sans script, sans service et sans réseau sortant (objectif O4), et ne rien
exposer sur le réseau.

## Décision

- Un exécutable autonome (`PublishSingleFile`, runtime .NET inclus, `win-x64`) avec un fichier
  SQLite sous `%LOCALAPPDATA%\Topogenia\` (spec 6.1).
- Écoute sur `127.0.0.1` seulement, jamais `0.0.0.0` ni `::` (spec 5.2).
- Jeton de lancement aléatoire de 256 bits, passé une fois au navigateur par l'adresse
  d'ouverture, échangé contre un cookie `HttpOnly`, `SameSite=Strict` ; toute requête sans ce
  cookie reçoit 401, santé comprise (AR-46). Contrôle de `Host`, `Origin` et `Sec-Fetch-Site`.
- Ouverture du navigateur par défaut (Edge ou Chrome). Une coquille de bureau WebView2 PEUT
  venir plus tard, par une ADR à part.
- Fichier sur partage réseau ou dossier synchronisé refusé (AR-33) ; sauvegarde avant toute
  migration, migration non lancée si la sauvegarde échoue (AR-14).

## Conséquences

- Aucune installation de base ni de service, aucune règle de pare-feu.
- Un autre utilisateur du même poste ne peut pas se connecter à l'instance.
- Un second lancement rouvre le navigateur sur l'instance existante (verrou nommé par
  utilisateur, P7-T01).

## Vérification

- Tests de sécurité du mode Poste dans `tests/Topogenia.Host.Tests/` (P4-T05) : refus sans
  cookie, `Host` invalide, écoute locale.
- Parcours e2e « jeton de lancement » et « installation Poste » (phases 6 et 7).
- Porte de contrôle Sécurité.
