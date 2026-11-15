<!-- 050-ligne-de-commande.md -->
<!-- Ligne de commande de l'executable : --migrate du mode Serveur, --check des deux modes, codes de sortie -->

# 050 - Ligne de commande : `--migrate` et `--check`

Statut : acceptée (date 24/10/2026)

## Contexte

La spec 6.3 donne `--migrate` (serveur) et `--check` (base et configuration, code de sortie non nul en cas de problème) ;
`--export` et `--import` viennent en phase 5, `--migrate-v1` est retirée (AR-94). Restaient à trancher : la place des
commandes dans l'hôte, `--migrate` en mode Poste, ce que `--check` exige de la configuration et de la base, le cas d'un
fichier Poste ouvert par l'application lancée (deux processus sur un fichier : limite de l'ADR 029), les codes de sortie.

## Décision

- **Place** : `Startup/HostCommands`. L'application est construite comme pour une exécution (même configuration, mêmes
  services), le serveur n'est jamais démarré ; les autres arguments vont à la configuration. Messages à la console dans
  la langue de l'utilisateur (`Startup/messages.*.json`), journal par codes et numéros du moteur (ADR 036).
- **Codes de sortie** : 0 réussite, 1 problème, 2 usage (plus d'une commande).
- **`--migrate`** : mode Serveur seulement ; il applique les migrations sous le compte qui lance la commande (spec 5.3,
  compte distinct du pool), crée la base absente (ADR 036) et dit combien de mises à jour il a appliquées, ou que la
  base était à jour. Il exige SQL Server et la chaîne de connexion, pas la liste des noms du serveur (elle sert à servir,
  pas à migrer). En mode Poste, il est refusé avec l'explication : le fichier est migré au démarrage, après sa
  sauvegarde (AR-14).
- **Délai des migrations** (ADR 041, « réglable par l'hôte ») : réglage `Database:MigrationTimeoutMinutes` du mode Serveur,
  10 par défaut, de 1 à 1440 ; hors de ces bornes, refus `DELAI_MIGRATIONS_INVALIDE` (démarrage, `--check` et `--migrate`).
  Le mode Poste garde le délai par défaut.
- **`--check`, mode Serveur** : les réglages du démarrage (`ServerSettings`), puis le contrôle du démarrage
  (`ServerDatabase`) : une base en retard est un problème (`BASE_EN_RETARD`, qui renvoie à `--migrate`).
- **`--check`, mode Poste** : l'emplacement du fichier (`DesktopDataFile`), puis, si le fichier, son journal ou sa mémoire
  partagée est tenu ouvert par un autre processus (l'application lancée), un refus sans rien ouvrir
  (`VERIFICATION_FICHIER_OUVERT`, `DataFileUse.HeldOpen`, extrait de la restauration) ; sinon le contrôle du schéma, le
  moteur fermé ensuite (journal réécrit). Un fichier absent ou en retard est utilisable : le démarrage le crée ou le
  migre après sauvegarde ; rien n'est créé par la vérification. Les dossiers lisibles par d'autres comptes sont signalés
  comme au démarrage (AR-98).

## Conséquences

- Le guide d'exploitation (P7-T04) donne `--migrate` application arrêtée, sous un compte membre de `db_owner`, et
  `--check` comme contrôle après installation ou mise à jour.
- La phase 5 ajoute `--export` et `--import` dans `HostCommands`.

## Vérification

`tests/Topogenia.Host.Tests/Startup/HostCommandsTests` (commandes et arguments, usage, `--migrate` refusé en mode Poste,
fichier à créer non créé, fichier tenu par l'application lancée puis libéré, dossier synchronisé, base SQL Server absente
créée et migrée puis à jour puis vérifiée, base en retard, chaîne absente, noms du serveur exigés par `--check` seul) ;
`Server/ServerStartupTests` (bornes du délai des migrations, délai passé au moteur).

## Révision (25/10/2026) : porte P4, round 1

- **Verrou d'instance** (`Desktop/DesktopInstance`) : un sémaphore nommé par fichier de données (`Local\`, empreinte du
  chemin), pris par l'application au démarrage et rendu à l'arrêt ; un second démarrage sur le même fichier est refusé
  `INSTANCE_DEJA_LANCEE` (P7-T01 le remplacera par l'ouverture du navigateur sur l'instance lancée). `--check` prend le
  même verrou avant d'ouvrir le fichier : une application lancée mais inactive (connexions relâchées) est vue, ce que le
  test du fichier ouvert ne garantissait pas.
- **Aucune trace** : toute panne d'une commande donne `ERREUR_INTERNE`, le code 1 et une ligne de journal par son type ;
  la fermeture du moteur après un refus (fichier endommagé, en lecture seule) est protégée ; un fichier en lecture seule
  n'arrête plus le test du fichier ouvert (`DataFileUse`).
- **Contenu inchangé** : la vérification ne change pas les données ; la fermeture réécrit le journal dans le fichier,
  comme un arrêt propre.
- **`--migrate`** : un refus passager (BASE_OCCUPEE, `model` tenu par une autre création) est rejoué après 2, 4 puis 8 s.
- `--check` journalise les dossiers lisibles par d'autres comptes, comme le démarrage (AR-98).
