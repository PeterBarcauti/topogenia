<!-- 055-allers-retours-et-ligne-de-commande-d-echange.md -->
<!-- Allers-retours exact et croisé du paquet par l'API sur les deux bases, paquets hostiles, commandes --export et --import -->

# 055 - Allers-retours du paquet et ligne de commande d'échange

Statut : acceptée (date 25/10/2026)

## Contexte

La spec 7.4 exige l'aller-retour exact `export(import(export(P))) == export(P)` sur un projet de test qui utilise toutes
les fonctions, le croisé SQLite -> SQL Server -> SQLite, et le refus sans écriture des paquets corrompus, tronqués, à
empreinte fausse, à chemin dangereux ou trop gros ; la spec 11 veut ces tests sur les deux bases. La 6.3 ajoute
`Topogenia.exe --export <projet> <fichier.topogenia>` et `--import <fichier.topogenia>`. P5-T01 et P5-T02 ont prouvé
l'aller-retour en mémoire (`PackageImportTests`) et par l'API sur un petit projet ; restaient le projet de toutes les
fonctions à travers les deux moteurs, le croisé, les paquets hostiles par l'API et la ligne de commande.

## Décision

### 1. Le projet de toutes les fonctions et ses allers-retours

- Le projet est celui de l'exemple livré, `samples/conges.topogenia` (`SampleProject` des tests de l'échange) : couloirs
  des trois types, décision à deux sorties, macro-étape et son détail, sous-carte vide, remontée ouverte et son reliquat
  (document, irritant, fiche), fiches brouillon, validée deux fois et à préciser, documents d'entrée et de sortie
  (adresse web et chemin), usages avec %C&A et irritants et leur historique, RACI des quatre lettres dont un rôle masqué,
  membres des trois rôles, ancien membre auteur, traductions manuelles et automatiques de tous les objets, classification,
  groupe de partage. Il lui manquait les losanges ET et OU inclusif, l'attente et l'automatisation en cours : P5-T07 les
  ajoute (l'exemple est réécrit par `SamplePackageTests`) et un test vérifie que l'exemple tient chaque valeur des listes
  fermées que porte le paquet.
- **Exact, par l'API, sur chaque base** : l'exemple importé par l'administrateur donne un projet P ; `export(P)` remplace
  P (le remplacement garde le code de P ; un import en nouveau projet exigerait un autre code, qui réécrit chaque code
  lisible) ; l'export du projet qui prend sa place est identique à `export(P)`, hors manifeste (spec 7.4), sans les
  personnes puis avec elles. Le premier import ajoute l'administrateur aux membres (importateur animateur, spec 7.3) :
  l'égalité se lit à partir de `export(P)`, comme la spec l'écrit.
- **Croisé** : `export(P)` de SQLite (avec les personnes) importé dans SQL Server en nouveau projet sous le même code,
  puis l'export de SQL Server remplaçant P dans SQLite : les trois exports sont identiques, hors manifeste.

### 2. Les paquets hostiles par l'API

Sur les deux bases, chacun refusé `PAQUET_INVALIDE` (ou 413) sans rien écrire (ni projet, ni utilisateur, ni audit) :
archive abîmée, tronquée, empreinte fausse, entrée au chemin dangereux (`..`, racine, lecteur), entrée inconnue, bombe de
décompression (une entrée qui se décompresse au-delà de sa taille annoncée), manifeste trop gros, JSON invalide, référence
qui ne désigne rien, structure fausse (deux étapes dans une case, attente sur une sortie), version plus récente
(`PAQUET_TROP_RECENT`) ; l'aperçu les refuse comme l'import. Le corps de plus de 100 Mo (413 avant lecture) est déjà
éprouvé par `ImportRoutesTests`.

Ajoutés à la porte P5 (round 1, ADR 052 section 3 bis) : une clé écrite deux fois dans un objet (`json`), un nombre hors
de la plage décimale (`schema`, au lieu d'une erreur 500), un rôle RACI porté deux fois (`reference`), et sept valeurs que
le schéma admet mais que les règles de la saisie refusent (`values`) : nom de 100 émojis (200 unités UTF-16), `<script>`
ou caractère NUL dans le contenu d'une version validée, adresse `javascript:`, irritant cité par deux usages, traduction
d'un autre champ que celui de son objet, commentaire d'historique trop long, login entouré d'espaces. Soit 23 paquets.

### 3. La ligne de commande

- `--export <code> <fichier>` et `--import <fichier>`, dans `Startup/HostCommands` comme `--migrate` et `--check`
  (ADR 050) : l'application est construite sans serveur, codes de sortie 0, 1 et 2, messages à la console dans la langue
  de l'utilisateur.
- **Qui agit** : le compte qui lance la commande, connecté comme à une première requête (`UserRunner.SignInAsync`,
  utilisateur créé s'il est nouveau) ; ses droits sont ceux de l'application, la ligne de commande n'en contourne aucun,
  et chaque export et import est tracé dans l'audit comme par l'API. En mode Poste, le compte du processus est
  administrateur de son fichier (spec 5.2) et peut tout faire. En mode Serveur, `--import` crée un projet et demande
  donc d'être administrateur (`ACCES_REFUSE` sinon) ; `--export` demande d'être animateur du projet, comme la route
  (AR-113) : un administrateur l'est de chaque projet, un animateur non administrateur exporte le sien, et un compte qui
  n'en est pas membre reçoit `INTROUVABLE`, comme par l'API (un projet qu'on ne voit pas n'existe pas pour lui).
- **Options, jamais un défaut silencieux** (spec 5.4) : `--export` exige `--with-people` ou `--without-people` ;
  `--import` exige `--with-members` ou `--without-members`, et prend `--code <CODE>` quand le code du paquet est pris.
  Une option absente, inconnue ou en double est une erreur d'usage (code 2) qui donne la forme de la commande. Le
  remplacement d'un projet n'est pas offert par la ligne de commande : il passe par l'aperçu de l'interface (AR-38).
- `<code>` est le code d'un projet vivant : un code mal formé est `PROJET_CODE_INVALIDE` (`ProjectCode.Parse`), un code
  qu'aucun projet visible ne porte `INTROUVABLE`. Le fichier exporté est écrit en entier ou pas du tout (fichier
  temporaire à côté, de nom unique, supprimé si l'écriture échoue, puis renommage) ; un fichier existant n'est jamais
  écrasé (`EXPORT_FICHIER_EXISTANT`). Le fichier à importer doit exister (`IMPORT_FICHIER_INTROUVABLE`).
- **Base** : en mode Poste, un fichier de données absent est refusé (`COMMANDE_FICHIER_ABSENT` : la commande ne crée
  pas de fichier, le premier lancement le fait), le verrou d'instance et le fichier tenu ouvert sont vérifiés comme pour
  `--check` (`COMMANDE_FICHIER_OUVERT`, le message de `--check` adapté à une commande), et un fichier en retard de schéma
  est refusé (`COMMANDE_FICHIER_A_MIGRER` : le démarrage le migre après sa sauvegarde) ; en mode Serveur, le contrôle du
  démarrage (`ServerDatabase`) passe d'abord.

## Défauts trouvés par ces allers-retours, corrigés

L'aller-retour exact passait en mémoire (P5-T02) ; à travers les vraies bases, l'import de l'exemple a révélé deux défauts :

1. **Ordre d'écriture** (`WritePlan`) : un lot de suppression créé part dans le premier envoi, avant tout le reste. Pour
   un projet créé dans le même jeu de changements (la remontée ouverte d'un paquet revient dans un lot neuf, ADR 052), le
   lot partait avant son projet et avant les utilisateurs qu'il nomme : `FK_deletion_batch_project`. Les projets et les
   utilisateurs créés partent désormais dans le premier envoi quand un lot est créé avec eux (EF Core ordonne les
   instructions d'un envoi par leurs clés étrangères, pas d'un envoi à l'autre). Test : `Persistence.Tests.Writing.
   NewProjectWritingTests` sur les deux bases.
2. **Cohérence des documents** : l'exemple portait un temps d'attente sur une sortie, ce que la base interdit (l'attente
   d'une sortie est portée par chacun de ses usages, `CK_document_wait_hours`) ; l'import le recopiait et la base le
   refusait en `CONFLIT_INTEGRITE` (409) au lieu de `PAQUET_INVALIDE` avant toute écriture. Les règles du projet importé
   (`ImportedProjects.Violations`) refusent désormais une attente sur une sortie (`output-wait`) et un lien sans son type
   (`document-link`), comme `DocumentEdits` les écrit ; l'exemple est corrigé (l'attente passe sur l'entrée extérieure).

## Options écartées

- Construire par l'API, dans le test, un projet de toutes les fonctions : des centaines d'appels qui ne prouveraient rien
  de plus que l'import de l'exemple, dont le contenu est déjà contrôlé ; l'exemple est le projet fictif livré (spec 3.1).
- Une ligne de commande qui agit hors des droits (lecture directe de la base) : elle échapperait à l'audit et aux
  règles d'import.
- Un défaut pour les personnes à la ligne de commande : la spec 5.4 interdit le défaut silencieux.

## Vérification

- `Topogenia.Host.Tests.Exchange.RoundTripTests` : exact avec et sans les personnes sur chaque base, croisé SQLite ->
  SQL Server -> SQLite.
- `Topogenia.Host.Tests.Exchange.HostilePackageTests` : vingt-trois paquets hostiles sur les deux bases (46 cas), refusés
  à l'aperçu et à l'import, aucun projet ni événement d'import écrit.
- `Topogenia.Host.Tests.Startup.ExchangeCommandsTests` : forme des commandes (opérandes et options hors de la
  configuration, neuf formes fautives en erreur d'usage), import puis export en mode Poste (export identique à l'exemple,
  code pris, `--code`, fichier existant jamais écrasé, projet inconnu), application lancée, compte non administrateur
  refusé en mode Serveur puis export identique à l'exemple une fois administrateur.
- `Topogenia.Interchange.Tests.Packages.SampleCoverageTests` (l'exemple tient chaque valeur des listes fermées de la
  structure et des règles), `Topogenia.Persistence.Tests.Writing.NewProjectWritingTests`,
  `Topogenia.Domain.Tests.Projects.ImportedProjectsTests` (cohérence des documents).
- Mutations tuées : lot écrit seul en premier, attente sur une sortie admise, réponse à la question des personnes donnée
  deux fois acceptée.
