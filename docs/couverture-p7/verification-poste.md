<!-- verification-poste.md -->
<!-- The portable ZIP of the desktop mode built, unzipped and started for real on the machine of the build (P7-T01, spec 6.1, ADR 071) -->

# Vérification du paquet du mode Poste (P7-T01)

Le 29/10/2026, sur le poste de construction (Windows 11, aucun outil installé pour l'occasion).

## Construction

`build/package.ps1 -Mode Desktop` (depuis `topogenia/` ; `node build/package.mjs Desktop` depuis l'ADR 076) : `dotnet publish` du profil
`src/Topogenia.Host/Properties/PublishProfiles/Desktop.pubxml`, puis le ZIP et son empreinte dans `artifacts/` (hors du
dépôt suivi, `.gitignore`) :

| Fichier | Taille | Rôle |
|---|---|---|
| `Topogenia.exe` | 55,2 Mo | exécutable unique autonome (`win-x64`, runtime inclus, compressé) |
| `e_sqlite3.dll`, `Microsoft.Data.SqlClient.SNI.dll` | 2,0 Mo, 0,6 Mo | bibliothèques natives à côté de l'exécutable, jamais extraites dans `%TEMP%` |
| `wwwroot/`, `Topogenia.staticwebassets.endpoints.json` | | l'interface construite et sa table |
| `appsettings.json` | 119 o | niveaux du journal |
| `README.txt` | | lancer, quitter, données, vérifier la copie, licences (français, anglais) |
| `licenses/` | | `LICENSE.txt` (Apache-2.0), `NOTICE.txt`, `CORE-LICENSE.md`, `THIRD-PARTY-NOTICES.md`, `Inter-OFL-1.1.md`, `Lucide-ISC.txt` |

ZIP : `Topogenia-2.0.0-portable.zip`, 51 Mo, empreinte dans `Topogenia-2.0.0-portable.zip.sha256` (elle change à chaque
construction ; `Get-FileHash -Algorithm SHA256` la redonne).

## Essais de l'exécutable décompressé

Toujours lancé avec ses réglages, jamais par défaut : `Topogenia__Desktop__Folder` vers un dossier de travail,
`Topogenia__Desktop__OpenBrowser=false`, `Topogenia__Desktop__Port=0`, dossier courant `%TEMP%` (l'hôte prend le dossier
de l'exécutable pour son interface).

| Essai | Résultat |
|---|---|
| Premier lancement (copie fraîchement décompressée ; fichier de données créé, migré, sauvegardé) | « Prêt » en 4,2 s (cible de la spec 6.1 : moins de 5 s) ; une copie toute neuve examinée par l'antivirus a donné 6,5 s une fois |
| Lancement suivant (fichier existant) | « Prêt » en 2,2 s |
| Second lancement pendant que le premier tourne | code de sortie 0 en 1,4 s, « L'application est déjà lancée : elle s'ouvre dans le navigateur. » ; le premier écrit « Un second lancement : le navigateur s'ouvre sur l'application. » (jeton neuf) |
| Requêtes sans session (`/`, `/favicon.svg`, `/api/me`, `/launch` avec un jeton faux) | 401 partout (spec 5.2) |
| Écritures | seulement dans le dossier donné : `data/topogenia.db` (et son journal WAL), `backups/`, `keys/`, `logs/` ; aucun `%TEMP%\.net` (aucune extraction) ; aucune clé de registre (pas d'installeur) |
| Arrêt | processus de l'essai arrêté par son numéro |

Le parcours de l'interface dans un vrai navigateur depuis cet exécutable demande le jeton que seul le navigateur reçoit :
il est l'objet de la recette du porteur du projet (`docs/recette/finale/`) ; l'interface et l'hôte sont les mêmes que ceux des 90
parcours (`HostApplication`, `wwwroot` construit), joués dans les deux modes et dans Edge.
