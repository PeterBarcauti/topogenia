<!-- 089-corrections-de-l-audit-externe.md -->
<!-- The corrections after the external audit of 01/11/2026: a stable port and a cookie per port in desktop mode, a folder of keys in server mode, and the checks moved where they were missing -->

# 089 - Les corrections de l'audit externe

Statut : acceptée (date 01/11/2026). Prolonge les ADR 044 (hôte du mode Poste), 075 (connexions d'un autre compte),
085 (flux de valeur) et 087 (empreinte du cœur). Demande du porteur du projet du 01/11/2026 : lire le rapport d'un
auditeur externe et apporter les corrections. Chaque constat a été vérifié dans les sources ; le verdict de chacun,
corrigé, gardé ou écarté avec sa raison, est dans `Travail\audit-plan.md` (hors du dépôt).

## Contexte

Quatre constats touchent des choix écrits, qu'une décision change :

- en mode Poste, le port libre choisi à chaque démarrage (ADR 044) change l'origine de la page : le navigateur oublie à
  chaque lancement ce qu'il garde pour une origine, les préférences d'affichage et la largeur des volets ;
- le cookie de session `desktop_session` n'est pas lié au port (RFC 6265, 8.5) : deux exécutions de la machine à la
  fois, la version portable et celle du Store pendant une reprise par export et import (F-70), se remplacent leur
  cookie, et l'onglet de l'autre exécution est refusé 401 ;
- en mode Serveur, aucune décision ne dit où vivent les clés de la protection des données qui protègent la clé de
  l'assistant dans la base : un pool d'IIS sans profil les garde en mémoire, la clé devient illisible à chaque recyclage ;
- la vérification d'un paquet du cœur (ADR 087) authentifie la marque, pas les octets.

## Décision

1. **Port stable du mode Poste.** Sans port configuré, l'hôte écoute sur un port calculé à partir du compte (le SID de
   son jeton) et du fichier de données : une empreinte SHA-256 des deux, ramenée dans 41 000 à 44 999, sous les ports
   que Windows donne aux connexions (49 152 et plus). Le même à chaque démarrage, un autre pour un autre fichier ou un
   autre compte d'une machine partagée. Quand ce port est pris par un autre programme ou refusé par Windows (une plage
   réservée à Hyper-V), le port libre choisi par le système, comme avant : l'exécution marche, seule l'origine est
   nouvelle. La sonde est un écouteur exclusif de la boucle locale, arrêté aussitôt : elle ne se connecte à rien (D14).
   Un port configuré (`Desktop:Port`) reste tel quel, et un port configuré pris arrête le démarrage, comme avant.
   `DesktopPort`.
2. **Un cookie par port.** Le cookie de session se nomme `desktop_session_<port>` : chaque exécution lit le sien,
   le lancement d'une seconde ne déconnecte plus la première. Toujours `HttpOnly`, `SameSite=Strict`, sans date, posé
   par l'échange du jeton de lancement ; la garde des connexions d'un autre compte (ADR 075) ne change pas.
3. **Clés du mode Serveur.** `Server:KeysFolder`, par défaut `keys` à côté du dossier du journal ; les clés y sont
   protégées pour la machine par DPAPI (`ProtectKeysWithDpapi(protectToLocalMachine: true)`) ; le compte de
   l'application doit pouvoir y écrire, comme dans le dossier du journal. `ServerServices.cs` rejoint la liste des
   fichiers qui ouvrent un dossier par son chemin (`SourceRulesTests.FileSystemFiles`).
4. **Les contrôles là où ils manquaient.**
   - La lecture d'un `.pmap` 1.2 applique les règles du domaine aux entrées du flux de valeur (bornes, et la place :
     un effectif sur un couloir de personnes, un volume sur un départ, une part sur une sortie de décision ou de
     branchement inclusif ; réglages du travail du projet) : une proposition de l'IA qui les enfreint est renvoyée au
     modèle avec ses fautes au lieu d'un refus à l'aperçu. Mêmes chemins que la construction du projet.
   - L'import d'un paquet `.topogenia` refuse un enregistrement supprimé dont l'effectif ou le volume enfreint la
     place que la base contrôle sur toutes les lignes (`PAQUET_INVALIDE` au lieu de `CONFLIT_INTEGRITE`).
   - Les lecteurs des fichiers Office joints rendent « illisible » un fichier hors de ce qu'ils attendent
     (identifiants Visio en double, indice de style négatif, date au-delà du calendrier), jamais une erreur du serveur.
   - L'échange de l'assistant attend la fin de son exécution avant que la requête se termine, une annulation comprise ;
     une session de la base disposée refuse d'ouvrir une connexion.
5. **Vérification d'un paquet du cœur.** `licence.mjs verify` compare aussi les octets : un paquet par son empreinte
   inscrite au registre, un assemblage par celle de chaque entrée de l'archive qui a sa marque. Il nomme la
   construction, ou dit que l'assemblage n'est aucune des constructions archivées (modifié ou reconstruit ailleurs).
   Les deux constructions du 31/10/2026 ont le même assemblage et diffèrent par la licence de leur paquet.

## Conséquences

- Les préférences d'affichage et la largeur des volets restent d'un lancement à l'autre en mode Poste (ZIP et Store).
- Un navigateur garde un cookie de session par port utilisé : des cookies de session, effacés à sa fermeture.
- En mode Serveur, un dossier de plus à créer et à protéger ; la clé de l'assistant survit aux recyclages et aux
  redémarrages ; un dossier de clés perdu rend la clé illisible (`key_unreadable`), à saisir de nouveau.
- Le domaine change (contrôle de l'import) : le paquet du cœur est à reconstruire pour la prochaine version publiée.
- Gardés et inscrits dans `docs/a-peaufiner.md` : la fenêtre de la console (F-69, à juger à la réponse de la
  certification), le proxy local d'un antivirus sur la boucle locale (F-73), l'aperçu d'une carte détaillée nouvelle
  (F-74), la colonne 10 000 (F-75), la relecture après écriture (F-76), le coût estimé sur Bedrock (F-77).
