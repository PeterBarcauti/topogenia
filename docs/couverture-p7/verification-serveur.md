<!-- verification-serveur.md -->
<!-- The publication of the server mode built and run for real on the machine of the build, and the load of 30 clients at once on it (P7-T02, spec 6.2, NF07, ADR 072) -->

# Vérification de la publication du mode Serveur et charge de 30 clients (P7-T02)

Le 29/10/2026, sur le poste de construction (Windows 11, SQL Server local `.`, aucun outil installé pour l'occasion ;
IIS n'est pas installé et ne peut pas l'être, prompt 4.4).

## Publication

`dotnet publish` du profil `src/Topogenia.Host/Properties/PublishProfiles/Server.pubxml` (dans `artifacts/server/`, hors
du dépôt suivi) : 84 fichiers, 22 Mo, dépendants du Hosting Bundle .NET 10 (`win-x64`, `SelfContained=false`) ;
`web.config` du module ASP.NET Core en mode « in-process », complété des réglages du dépôt (`src/Topogenia.Host/web.config` :
requête de 101 Mio au plus pour un paquet de 100 Mio, `removeServerHeader`, `X-Powered-By` retiré). Le ZIP
(`build/package.ps1 -Mode Server`, `node build/package.mjs Server` depuis l'ADR 076) ajoute `appsettings.Production.example.json`, `README.txt`, le guide
`exploitation-serveur.md` (P7-T04) et les licences.

## L'hôte publié en mode Serveur

Lancé tel qu'il est publié (`Topogenia.exe` du dossier de publication), sur Kestrel en HTTPS (`https://localhost:5443`),
avec un certificat de test créé en mémoire et exporté en PFX dans le dossier de travail (aucun magasin de certificats
touché), la configuration par variables d'environnement (`Topogenia__Mode=Server`, chaîne de connexion en
authentification Windows, `Server:AllowedHosts` = `localhost`, l'administrateur de l'essai), `ASPNETCORE_ENVIRONMENT=Production` :

| Essai | Résultat |
|---|---|
| `Topogenia.exe --migrate` sur une base absente (`Topogenia_Charge`) | code 0, « Base de données migrée : 11 mise(s) à jour appliquée(s). » |
| Santé anonyme `GET /api/health` | 200, `{"status":"ok"}` (minimale, spec 5.3) |
| Profil `GET /api/me` en Negotiate | 200 |
| Interface `GET /` sans authentification | 401 |
| Fin de l'essai | hôte arrêté par son numéro, base `Topogenia_Charge` supprimée |

## Charge de 30 clients à la fois (NF07, S-NF-09)

Outil `build/load/ServerLoad.cs` (programme C# d'un fichier, `dotnet run`, jamais livré) : 30 clients HTTP, chacun avec
son projet, s'authentifient en Negotiate (le compte de l'essai : 30 sessions d'un même compte, il n'y a qu'un compte sur
ce poste) et répètent 10 fois les gestes courants : accueil (liste des projets), ouverture de la carte, ajout d'une étape,
relecture de la carte, renommage de l'étape (`If-Match`), lecture puis enregistrement de sa fiche. Un tour de mise en
train non compté, un client seul, puis les 30 ensemble. Résultats complets : `Travail/charge-p7-t02-*.json` et `.txt`.

| Geste | Un client seul : médiane / 95e centile | 30 clients, 1 s entre deux gestes : médiane / 95e / max | 30 clients en rafale (aucune pause) : médiane / 95e / max |
|---|---|---|---|
| Accueil (liste des projets) | 90 / 97 ms | 184 / 542 / 680 ms | 233 / 1 369 / 1 872 ms |
| Ouverture de la carte | 46 / 58 ms | 34 / 50 / 86 ms | 86 / 278 / 499 ms |
| Ajout d'une étape | 58 / 68 ms | 47 / 69 / 121 ms | 177 / 522 / 757 ms |
| Renommage d'une étape | 57 / 61 ms | 44 / 70 / 85 ms | 163 / 470 / 766 ms |
| Lecture de la fiche | 34 / 83 ms | 29 / 48 / 87 ms | 79 / 301 / 490 ms |
| Enregistrement de la fiche | 83 / 114 ms | 62 / 101 / 173 ms | 246 / 619 / 958 ms |

2 334 requêtes par passage, aucun échec. Avec une seconde entre deux gestes (environ 30 requêtes par seconde, bien
au-delà de 30 personnes qui lisent et saisissent), chaque geste reste sous 0,2 s au 95e centile sauf l'accueil (0,54 s) :
aucune dégradation perceptible (seuil retenu : une seconde, ADR 072). En rafale (environ 150 requêtes par seconde), tout
reste sous 1 s au 95e centile sauf l'accueil (1,4 s). La liste des projets de l'accueil est le seul geste qui s'alourdit
avec la charge et avec le nombre de projets visibles (62 dans la base de l'essai) : registre F-42.

## Ce que cet essai ne prouve pas

- IIS : ni le module ASP.NET Core, ni le `web.config`, ni l'authentification Windows d'IIS ne sont essayés ici (IIS non
  installé) : étapes de la recette d'installation (`docs/recette/finale/`, guide `docs/exploitation-serveur.md`).
- Le compte de service membre du seul rôle `topogenia_app` (AR-110) : créer un compte de connexion au serveur SQL sort
  des bases `Topogenia_*` (prompt 4.2) ; les droits du rôle sont prouvés par `Persistence.Tests.Bi.RoleTests` et
  `Startup.StartupErrorTests`, et le démarrage sous ce compte est une étape de la recette d'installation.
- 30 comptes distincts : un seul compte sur ce poste ; l'hôte ne garde par utilisateur que son profil.
