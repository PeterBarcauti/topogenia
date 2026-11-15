<!-- 104-ouvrir-le-dossier-d-un-document.md -->
<!-- « Open the folder » of a document (3.2): in desktop mode only, on a click of the person, the host asks Windows to show the folder that holds a document, never the document itself; the one named exception to S-5.4-13, its route, its refusals, its port and its single file that reaches the disk and the shell, the risk that remains; this decision is the server, the screen is written apart -->

# 104 - Ouvrir le dossier d'un document

Statut : acceptée le 14/11/2026 (3.2, lot J10), sur demande du porteur du projet : un bouton « Ouvrir le dossier » à
côté du chemin d'un document. Cette décision pose le côté serveur (la route, ses refus, ce que le programme demande à
Windows) ; l'écran (le bouton, « Copier le chemin », la mise en page) est écrit à part, sur le contrat ci-dessous.

**Révisée le 14/11/2026** après une relecture de sécurité indépendante, avant tout essai : le nom du serveur d'un chemin
réseau est tenu à la forme d'un nom de machine ; les règles du chemin ne lisent plus que le texte (plus aucun appel du
cadre qui lise le disque) ; le shell est appelé sur le fil du regard, borné comme lui, par la fonction de Windows
elle-même et avec la classe des dossiers imposée ; les règles d'architecture fixent la liste des fonctions natives et
la surface de l'exception. Les points 4 à 9 disent l'état corrigé ; ce qui a été écarté est aux alternatives.

**Révisée une seconde fois le 14/11/2026** après la seconde relecture indépendante, qui n'a démontré ni exécution, ni
ouverture d'un fichier, ni course : la classe des dossiers n'est plus imposée que pour une racine (imposée à tout
dossier, elle aurait confié au verbe des dossiers un fichier mis à la place du dossier) ; le texte est lu caractère
par caractère et non par demi-caractères ; le nom du serveur est tenu aux lettres d'ASCII ; le regard et le shell
sont deux classes ; les règles d'architecture admettent par liste au lieu de refuser par liste.

**Relue une troisième fois le 14/11/2026**, sans exécution, ouverture d'un fichier ni plantage démontrés. Dans le
produit : la table entière des caractères qu'Unicode dit de ne pas dessiner, et un nom terminé par une espace, sont
refusés (point 4). Dans les règles d'architecture : elles lisent tout fichier que la construction compile, où qu'il
soit rangé ; un type de fichiers que le texte ne nomme pas est lu ; le tour compare les routes que l'hôte dit avoir
atteintes. Cinq phrases récrites. La correction des règles n'a pas été relue à son tour, et ces règles sont devenues
nombreuses et raides : les alléger est inscrit (F-202).

## Contexte

Dans la fiche d'une étape, chaque document peut porter un emplacement : une adresse web, ou un chemin Windows ou
réseau (`X:\...`, `\\serveur\partage\...`, `DocumentLocation`). Une adresse web est un lien. Un chemin n'était
qu'affiché : **le navigateur interdit** à une page servie en `http` d'ouvrir un chemin du poste ou du réseau (un
lien `file://` y est bloqué, l'Explorateur ne se lance pas depuis une page) ; la page ne peut que copier le texte.

L'exigence S-5.4-13 (F-06) dit : « le serveur ne suit jamais l'emplacement saisi pour un document », un chemin UNC
étant le partage d'une autre machine. Elle protège un serveur partagé, dont le compte de service irait frapper à la
porte d'une machine choisie par un utilisateur. En mode Poste, le programme tourne sur le poste de la personne, sous
son compte, pour elle seule (ADR 044, 075) : il peut demander à Windows, à sa place, ce qu'elle ferait elle-même en
collant le chemin dans l'Explorateur.

## Décision

1. **L'exception à S-5.4-13, mot pour mot** : « en mode Poste, sur un clic de la personne, le programme demande à
   Windows de montrer le dossier qui contient le document ; il n'ouvre jamais un document ». Tout le reste de la règle
   tient : aucune autre route ne regarde ni n'ouvre un emplacement, et le mode Serveur ne le fait jamais.
2. **Une route** : `POST /api/steps/{id}/documents/{documentId}/folder`, sans corps, réponse **204** sans contenu
   quand le dossier a été montré. `id` est l'étape qui a déclaré le document, comme pour `PUT` et `DELETE` du même
   chemin. **Droit** : qui peut lire la fiche (lecteur du projet). La requête ne porte que des identifiants : **le
   chemin vient de la base**, jamais de la requête. Rien n'est écrit : ni version, ni `If-Match`, ni événement
   d'audit, ni entrée dans l'historique. C'est tout de même un `POST` (il a un effet sur le poste) : il passe par les
   gardes de toute écriture (`RequestGuard` : `Host`, en-tête anti-CSRF, `Origin`, `Sec-Fetch-Site`).
3. **Dans cet ordre** :
   1. le mode Poste, sinon `INTROUVABLE` (comme le projet d'exemple) ; le mode est décidé **avant** toute lecture du
      document, et le mode Serveur n'enregistre ni l'ouvreur, ni le regard sur le disque, ni le shell : sa route ne
      sait que refuser ;
   2. le droit de lecture et le document vivant de cette étape vivante (`GetStepDocumentQuery`), sinon `INTROUVABLE` :
      un document d'une autre étape ou d'un autre projet ne se distingue pas d'un document absent ;
   3. l'emplacement gardé doit être de type `path`, sinon `DOCUMENT_SANS_CHEMIN` (pas d'emplacement, ou une adresse
      web, qui n'est jamais suivie) ;
   4. les règles du chemin, **sur son texte, avant tout accès au disque** (point 4) ;
   5. le regard : le dossier à montrer est le chemin lui-même s'il désigne un dossier, sinon son dossier parent,
      jamais un dossier plus haut (point 5) ;
   6. le dossier qui vient d'être regardé est donné au shell de Windows (point 6).
4. **Les règles du chemin** (`FolderPaths`). Un principe : *le dossier montré est, caractère pour caractère, le
   chemin saisi ou son parent* ; ce que Windows lirait comme un autre objet est refusé, jamais réparé.

   | Chemin | Décision |
   |---|---|
   | plus de 259 caractères (ou un dossier qui, écrit avec son séparateur final, les dépasse) | refusé, `CHEMIN_TROP_LONG` |
   | barres obliques mélangées, séparateurs répétés (`C:/Dossiers\\a`) | **normalisés** : écrits comme Windows les écrit, rien d'autre ne change |
   | un lecteur sans dossier (`X:\`), un partage sans dossier (`\\serveur\partage`) | montré lui-même ; il n'a pas de parent |
   | `\\?\...`, `\\.\...` (périphériques, tubes, espace de noms sans règles) | refusé, `CHEMIN_NON_PRIS_EN_CHARGE` |
   | `\\serveur\pipe\...`, `\\serveur\mailslot\...`, `\\serveur\IPC$\...` (un regard y serait une connexion) | refusé |
   | `\\serveur\DavWWWRoot\...` (la racine d'un serveur WebDAV : une adresse du web écrite comme un partage) | refusé |
   | un serveur sans partage (`\\serveur`), un chemin sans lecteur | refusé |
   | un serveur qui n'est pas écrit comme un nom de machine (autre chose que les lettres et les chiffres d'ASCII, `.`, `-`, `_`) : `\\;LanmanRedirector\serveur\...`, `\\serveur@SSL\...`, `\\serveur@80\...`, un nom qui porte une lettre d'une autre écriture | refusé : un point-virgule nomme un fournisseur de Windows devant le serveur, et un tube se cache alors derrière le nom d'un partage ; une arobase fait du chemin une adresse WebDAV sur un port au choix ; une lettre d'une autre écriture, dessinée comme une lettre latine, nomme un autre serveur que celui qui se lit |
   | `\\wsl.localhost\...` (les fichiers d'un système Linux du poste, qu'un regard démarrerait) | refusé |
   | un segment `.` ou `..` (un chemin relatif masqué) | refusé |
   | un nom terminé par un point ou une espace (Windows le couperait), ou par une autre espace que l'ordinaire (Windows la garde, et personne ne la voit à cet endroit) | refusé |
   | un deux-points hors du lecteur (un flux d'un fichier) | refusé |
   | le nom d'un périphérique de Windows, avec ou sans extension, dans n'importe quel dossier (`CON`, `PRN`, `AUX`, `NUL`, `COM0` à `COM9`, `LPT0` à `LPT9`, `CONIN$`, `CONOUT$`) | refusé |
   | un nom qui porte la marque d'un objet de Windows (`nom.{identifiant}`, où que soit la marque `.{` dans le nom), que l'Explorateur montrerait à la place du contenu du dossier | refusé |
   | un caractère de contrôle, un séparateur de ligne ou de paragraphe, un caractère de mise en forme d'Unicode de n'importe quel plan (celui qui écrit un nom à l'envers, une espace sans largeur, une marque d'ordre des octets, une étiquette), un caractère qu'Unicode dit de ne pas dessiner (les caractères de remplissage, les sélecteurs de variante, les marques et les voyelles jamais dessinées, les points de code réservés à ces usages : la table `Default_Ignorable_Code_Point` entière, 4 174 points de code), un demi-caractère seul ; les deux liants que des écritures emploient dans leurs mots sont gardés | refusé : le chemin affiché à côté du bouton ne serait pas celui qui est écrit |
   | à l'intérieur d'un nom de dossier ou de fichier, une espace autre que l'ordinaire (l'espace insécable qu'un traitement de texte écrit devant une ponctuation, l'espace des écritures d'Asie) ou une lettre d'une autre écriture | gardées : elles se dessinent. Seul le nom du serveur est tenu aux lettres d'ASCII ; deux noms d'un même partage peuvent donc se ressembler à l'œil |
   | un nom court (`PROGRA~1`) | gardé tel qu'il est écrit, sans regard sur le disque |

   Ces règles ne lisent que le texte : aucune fonction du cadre qui pourrait lire le disque n'y est appelée. La
   première écriture demandait à `Path.GetFullPath` de rendre le chemin inchangé ; cette fonction lit le disque, et le
   réseau, dès qu'un chemin porte un tilde, hors du délai et du tour unique : elle est retirée, et les noms de
   périphérique sont refusés par leur liste, pareil sous tout Windows. Dans les essais seulement, chaque chemin admis
   est comparé à ce que cette fonction en rend : il revient inchangé. Le texte est lu caractère par caractère : lu par
   moitiés, un caractère hors du plan de base ne dit rien de lui.
5. **Le regard** (`IFolderDisk`) demande à Windows les **attributs** du chemin : un fichier n'est jamais ouvert, lu
   ni exécuté. Pour un chemin qui porte un tilde, le cadre .NET demande d'abord à Windows son nom long ; quand Windows
   refuse les attributs ou que le fichier est pris, il demande ce seul nom au dossier qui le contient : des requêtes
   au même serveur, sur le fil du clic et dans son délai, rien de plus. Le regard est **borné à
   3 secondes** : un serveur de fichiers injoignable ne fige pas la fiche (`DOSSIER_INJOIGNABLE`), et une réponse
   arrivée après le délai **ne montre rien** (aucun Explorateur ne s'ouvre longtemps après le refus). **Un seul clic
   à la fois**, sur un fil à lui qui regarde puis demande au shell : un clic pendant qu'un autre attend Windows est
   refusé tout de suite (`DOSSIER_OUVERTURE_EN_COURS`) au lieu d'empiler des fils, et le tour n'est rendu qu'une fois
   le shell revenu. Une panne du regard ou du shell qui n'est pas une réponse de Windows est dite
   `DOSSIER_INJOIGNABLE`, jamais une erreur interne ; une panne de mémoire seule est laissée à l'hôte, comme partout
   dans le programme : le travail du clic est une tâche, elle remonte à la requête et le programme ne s'arrête pas.
6. **Le lancement** (`IFolderShell`) demande au shell de Windows de montrer le dossier, par sa fonction
   `ShellExecuteExW`, déclarée dans le seul fichier `ShellFolderOpener.cs` : **aucune ligne de commande**, ni `cmd`,
   ni PowerShell, ni `explorer.exe`, aucun programme nommé, aucun paramètre, aucun dossier de travail, aucune
   fenêtre d'erreur. Trois gardes pour que seul le programme qui montre les dossiers soit lancé :
   - le dossier est donné **terminé par son séparateur**, forme sous laquelle les fonctions de fichiers de Windows
     n'ouvrent aucun fichier (prouvé sur ces fonctions ; l'analyse que le shell fait du chemin n'est pas jouée) ;
   - le verbe est `explore`, celui qui montre un dossier dans l'Explorateur, et **qu'aucune classe de fichier ne
     déclare** : dans le registre d'un Windows 11, seule la classe `Folder` le porte ;
   - **où le verbe est cherché dépend du dossier** (`ShellFolderOpener.ClassOf`). Pour une **racine**, un lecteur ou
     un partage seul, dans la seule classe `Folder`, imposée à l'appel : un disque optique ou une image montée peut
     déclarer pour sa racine un verbe à lui (`autorun.inf`), qui lancerait un programme, et une racine est un dossier
     quoi qu'on mette sur le disque. Pour **tout autre dossier**, aucune classe n'est nommée : le shell cherche le
     verbe dans les classes de ce que le chemin désigne, où aucun fichier ordinaire ne l'a ; un document ou un
     programme mis à la place du dossier entre le regard et le lancement, ou servi par un serveur qui a répondu
     « dossier » au regard, est donc refusé. La classe imposée à tout dossier (écriture d'entre les deux relectures)
     l'aurait confié au verbe des dossiers. **Reste un fichier que Windows montre lui-même comme un dossier** (une
     archive `.zip` ou `.cab`, une bibliothèque, une recherche enregistrée) : la classe des dossiers est aussi la
     sienne, le verbe peut donc y être trouvé, et l'Explorateur le montrerait alors comme un dossier, comme il le
     fait quand une personne y navigue. Il y faut un serveur qui réponde « dossier » au regard puis serve ce
     fichier ; ce cas n'est pas joué, une sonde hors du dépôt le regarde à l'essai à la main (voir Conséquences).

   Le programme lancé est celui que Windows associe aux dossiers : l'Explorateur, ou le gestionnaire de fichiers que
   le compte a mis à sa place. C'est ce que dit la documentation de Windows ; l'appel réel n'est joué par aucun essai.

   Le shell reçoit le même délai que le regard, une fois le regard répondu : s'il ne répond pas à temps, le clic est
   dit `DOSSIER_INJOIGNABLE` ; Windows ayant déjà pris la demande, l'Explorateur peut encore s'ouvrir quand il
   répond. Un échec du shell est rendu comme un refus, lu sur l'erreur de Windows comme celui du regard. La fonction
   est appelée sur un fil à elle, dans l'appartement que le shell demande ; toute exception sur ce fil, quelle
   qu'elle soit, y est attrapée et dite `DOSSIER_INJOIGNABLE` : laissée là, elle arrêterait le programme. Une faute
   du code natif du shell lui-même, ou d'une extension que le poste y a installée, n'est pas une exception : rien ne
   peut l'attraper, et elle arrêterait le programme (non joué). L'appel réel du shell n'est joué par aucun essai
   (voir Conséquences).
7. **Les refus**, chacun avec sa phrase dans les 18 langues de l'hôte, qui dit quoi faire (le chemin reste à copier) :

   | Code | Statut | Quand |
   |---|---|---|
   | `INTROUVABLE` | 404 | mode Serveur ; non-membre ; document absent, d'une autre étape, d'une étape supprimée, d'un projet à la corbeille |
   | `DOCUMENT_SANS_CHEMIN` | 400 | pas d'emplacement, ou une adresse web |
   | `CHEMIN_TROP_LONG` | 400 | plus de 259 caractères (`max` dans les détails) |
   | `CHEMIN_NON_PRIS_EN_CHARGE` | 400 | une forme refusée du point 4 ; rien n'a été regardé |
   | `DOSSIER_INTROUVABLE` | 404 | ni le chemin ni son parent n'est un dossier |
   | `DOSSIER_ACCES_REFUSE` | 403 | Windows refuse l'accès au compte |
   | `DOSSIER_INJOIGNABLE` | 409 | le serveur ou le lecteur ne répond pas, au regard ou au shell, délai dépassé compris ; une panne du regard ou du shell |
   | `DOSSIER_OUVERTURE_EN_COURS` | 409 | un autre clic attend encore la réponse de Windows, à son regard ou à son shell |

   Les codes sont déclarés par l'hôte (`DocumentFolderErrors`), le cœur fermé n'en gagne aucun.
8. **Un port, un seul fichier qui touche le disque**. Le port (`IFolderOpener`, avec `IFolderDisk`, `IFolderShell`,
   les règles `FolderPaths` et l'ouvreur `CheckedFolderOpener`) est déclaré dans `Desktop/FolderOpening.cs`, sans
   aucun type de `System.IO`. Un seul fichier de production regarde le disque et appelle le shell pour cette
   fonction : `Desktop/ShellFolderOpener.cs`, ajouté à la liste `FileSystemFiles` ; il reçoit un chemin et ne connaît
   aucun type de document. Il déclare **deux classes qui ne partagent rien** : `WindowsFolderDisk` porte le regard et
   lui seul, `ShellFolderOpener` porte le shell et lui seul ; qui tient le regard ne peut pas atteindre le shell, et
   aucun essai ne construit la classe du shell. Un seul fichier donne l'emplacement d'un document au port : la route,
   `Api/Documents/DocumentFolderApi.cs`. **Les règles d'architecture disent cette vérité** : la liste exacte des
   fichiers qui emploient les types du port est fixée (le port, son fichier listé, l'enregistrement du mode Poste, la
   route), la route y est la seule à lire un document ; les seuls membres par lesquels le fichier listé touche le
   disque et le shell sont fixés, avec ce qu'il donne à la fonction du shell, et ce fichier est en plus tenu tel
   qu'il est écrit, mot pour mot, commentaires à part : toute retouche de son code, même un nom, fait tomber la
   règle et se fait exprès, la règle avec ; la liste exacte des fonctions natives du code livré est fixée, fichier
   par fichier ; la surface du port et de la route l'est aussi (les interfaces portées, les membres publics qui
   prennent un texte, qui nomme la route) ; un second usage casse un essai. Ces règles **admettent par liste** au
   lieu de refuser par liste : les espaces de noms du cadre que le code livré emploie, ce qu'un fichier qui lit un
   document a le droit d'employer, les fichiers où un appel se fait par un nom écrit en texte sont des listes
   exactes ; aucune ligne du code n'est gardée ou écartée par un symbole de construction, que les règles ne
   liraient pas. Ce qu'elles ne tiennent pas est inscrit (F-197).
9. **Aucun essai ni parcours n'ouvre un vrai Explorateur** : le shell est remplacé par un double qui enregistre ce
   qu'on lui demande dans tout hôte qu'un essai construit, quel que soit son mode (l'hôte en mémoire, l'exécution
   réelle, les commandes, l'hôte des parcours, qui écrit les dossiers demandés dans `shown-folders.txt` de son
   dossier), et une règle le vérifie sur la liste exacte des fichiers d'essai qui construisent un hôte. Aucun essai
   ni parcours ne construit la classe du shell, sous aucune écriture que la syntaxe laisse lire : ses essais ne
   lisent que ses deux constantes et ses deux fonctions qui n'atteignent rien, et sans objet de cette classe la
   demande au shell ne peut pas être faite.

## Conséquences

- La personne ouvre le dossier d'un document en un clic, en mode Poste. En mode Serveur le bouton n'a pas de route :
  il reste « Copier le chemin ».
- **Le risque qui reste, dit franchement** : le clic fait contacter par Windows le serveur nommé dans le chemin (le
  regard suffit, avant même l'Explorateur) ; le programme n'ouvre lui-même aucune connexion (D14), c'est Windows qui
  ouvre une session vers ce serveur de fichiers. Dans un projet importé (paquet, `.pmap`), ce serveur a été choisi par
  quelqu'un d'autre ; Windows peut alors lui présenter l'identité de la session, comme pour tout lien réseau cliqué.
  Ce qui le borne : c'est un geste de la personne, sur un chemin affiché en clair à côté du bouton ; rien n'est
  regardé sans ce clic (aucune lecture de fiche, de carte ni d'import ne touche un emplacement) ; le mode Poste
  seulement ; un clic à la fois ; ni tube, ni périphérique, ni adresse WebDAV écrite dans le chemin (une arobase dans
  le serveur, le partage `DavWWWRoot`), ni fournisseur nommé devant le serveur ; un serveur écrit dans les seules
  lettres d'ASCII, pour que le nom lu soit le nom contacté. Ce qui ne le borne pas : il n'y a ni liste de serveurs
  admis, ni avertissement propre aux projets importés ; et quand le partage de fichiers ne répond pas, Windows peut de
  lui-même essayer le même serveur par WebDAV, comme pour tout chemin réseau ouvert dans l'Explorateur.
- **Ce que l'Explorateur montre est ce que le serveur donne.** Un serveur choisi par un tiers peut répondre « dossier »
  au regard puis donner autre chose au shell. Un fichier ordinaire n'a pas le verbe demandé : il est refusé, d'après
  ce que Windows documente et ce que le registre montre. Mais l'Explorateur montre ce qu'il reçoit comme dossier (une
  archive, une bibliothèque ou une recherche enregistrée s'y affichent comme un dossier, y compris, sans doute, quand
  c'est ce fichier lui-même que le serveur sert à la place du dossier ; et un dossier peut se déclarer objet de
  Windows par son `desktop.ini`). C'est le risque de tout dossier ouvert sur ce serveur dans l'Explorateur ; à
  regarder à l'essai à la main.
- **Le verbe vient du registre du poste.** Un gestionnaire de fichiers que le compte a installé à la place de
  l'Explorateur reçoit le dossier, par la ligne de commande que le shell bâtit lui-même ; un type de fichier auquel un
  logiciel du poste aurait donné un verbe `explore` serait suivi pour un fichier mis à la place d'un dossier. Aucun
  Windows n'en déclare ; c'est la configuration du poste, hors de la main d'un projet importé.
- Le fil d'un clic reste occupé tant que Windows attend un serveur muet, au regard comme au shell (un seul à la
  fois) ; pendant ce temps les autres clics sont refusés. Un serveur qui ne répondrait jamais garderait le tour
  jusqu'à ce que Windows renonce.
- L'appel réel du shell n'est joué par aucun essai (il ouvrirait un Explorateur sur le poste de construction) : le
  dossier choisi et la forme donnée au shell sont prouvés ; la disposition de la structure donnée à
  `ShellExecuteExW` (112 octets, quinze champs) et le refus propre de l'appel sur un chemin et un verbe absents ont
  été vérifiés par une sonde hors du dépôt, qui n'ouvre rien ; le verbe a été lu dans le registre du poste de
  construction. Ce que le shell fait de cet appel sur un vrai dossier (l'Explorateur ouvert sur le dossier, la racine
  d'un lecteur, un partage, son refus d'un fichier mis à la place du dossier) reste à essayer à la main sur un poste.
  Trois cas restent ouverts après la troisième relecture, inscrits (F-201) : un fichier que Windows montre comme un
  dossier (une archive), que la sonde de l'essai regarde ; un volume monté dans un dossier, qui n'est pas une racine
  par son texte et dont le disque pourrait déclarer le verbe ; un partage qui ne serait pas un dossier (une file
  d'impression), auquel la classe des dossiers est imposée comme à toute racine.
- L'ADR 014 disait : « les liens de documents ne sont jamais suivis par le serveur ». Elle tient en mode Serveur et
  pour les adresses web ; en mode Poste, elle reçoit la seule exception du point 1.
- Le contrat de l'API gagne une route et sept codes ; aucune table, aucune colonne, aucune migration.
- S-5.4-21 (aucun script à l'exécution) tient : le code livré ne lance qu'un processus, le navigateur ; le dossier
  est montré par la fonction du shell, sans programme nommé ni paramètre.

## Alternatives écartées

- **Ouvrir le fichier** : le programme exécuterait, sous le compte de la personne, ce qu'un projet importé désigne.
- **Une liste blanche d'extensions** : elle ne dit rien du contenu, vieillit, et laisse passer les documents à macros.
- **`explorer /select`** (montrer le fichier sélectionné) : un programme nommé et une ligne de commande bâtie avec un
  chemin saisi par quelqu'un, dont les guillemets, les virgules et les espaces deviendraient de la syntaxe ; le shell
  appelé avec un seul texte n'a pas de syntaxe à détourner.
- **Un protocole `file://`** dans la page : bloqué par les navigateurs depuis une page `http`, et il ouvrirait le
  fichier.
- **La même route en mode Serveur** : le serveur suivrait un chemin saisi par un utilisateur, ce que S-5.4-13 interdit.
- **Normaliser comme Windows** (`..`, points et espaces de fin) : le dossier montré ne serait plus celui qui est écrit.
- **Lancer par `Process.Start` avec le verbe `explore`** (première écriture, retirée après la relecture de sécurité) :
  le shell cherche alors le verbe dans les classes de ce que le chemin désigne. À la racine d'un disque optique ou
  d'une image montée, un `autorun.inf` peut déclarer ce verbe et faire lancer un programme ; la classe `Folder`
  imposée ferme ce chemin, et `Process.Start` ne sait pas l'imposer.
- **Imposer la classe `Folder` à tout dossier** (seconde écriture, retirée après la seconde relecture) : le verbe des
  dossiers est alors appliqué à ce que le chemin désigne, quel qu'il soit. Un fichier mis à la place du dossier, que
  la recherche du verbe refusait jusque-là faute de verbe, serait confié à l'Explorateur comme un dossier, qui est
  connu pour ouvrir un fichier qu'on lui donne à parcourir. La classe n'est donc imposée que pour une racine, qui ne
  peut pas être un fichier.
- **Montrer le dossier avec le document sélectionné** (`SHOpenFolderAndSelectItems`) : trois fonctions natives et
  des identifiants du shell à libérer, pour un geste différent de celui décidé (le dossier lui-même quand le chemin
  en est un). C'est la fonction qui ne sait que naviguer ; à reprendre si l'essai à la main montrait que le verbe
  `explore` fait autre chose que ce qui est écrit ici.
- **Demander le chemin complet au cadre** (`Path.GetFullPath`) comme dernier garde : il lit le disque et le réseau
  pour tout chemin qui porte un tilde, avant le délai et hors du tour unique.

## Vérification

`FolderPathsTests` (les règles du chemin, ce qu'elles gardent, ce qu'est une racine, chaque chemin admis comparé à
ce que Windows en lit, les erreurs de Windows), `FolderOpenerTests` (le dossier choisi, sa forme, ce qui n'est ni
regardé ni montré, le délai, le second clic, la réponse tardive, un shell qui ne répond pas, une requête abandonnée
pendant que le shell attend, une panne du regard ou du shell), `ShellFolderOpenerTests` (le regard réel sur un
dossier temporaire : un fichier n'est ni ouvert ni lu, la forme donnée au shell n'ouvre aucun fichier, un nom court ;
la classe où le verbe est cherché, pour une racine et pour tout autre dossier ; le fil du shell, sans le shell :
la question posée une seule fois, même quand elle tombe en panne, et aucune exception n'en sort),
`DocumentCommandsTests` (la lecture du document de l'étape),
`DocumentFolderRoutesTests` et `DocumentFolderLookTests` (la route par l'hôte : droits, refus, rien d'écrit, mode
Serveur), `DocumentLocationNeverOpenedTests` (aucune route n'ouvre un emplacement qui nomme un tube, sous trois
écritures et sur les deux bases ; en mode Poste, avec un dossier que le port admet, la route du dossier est la seule
après laquelle le regard et le shell ont reçu quelque chose ; pour le tube local et pour le dossier, le tour joue en
plus chaque route de la table des droits, la santé et le projet d'exemple, et ce qui a été joué est comparé à la
table des routes de l'hôte : 107 routes atteintes sur 108, `POST /api/desktop/quit` seule nommée hors du tour, parce
qu'elle arrêterait l'hôte ; c'est la route que le routage de l'hôte d'essai a choisie qui est comptée, non le nom
donné par l'essai, et une requête qui atteint une autre route que celle qu'elle nomme fait tomber le tour ; une
route ajoutée fait tomber l'essai tant qu'elle n'est ni jouée ni nommée),
`ErrorMessagesTests`, `RouteTableTests`, `RightsMatrixTests`, `IfMatchRequirementTests`, `ApiContractTests`.

Les règles d'architecture, qui lisent les sources du code livré et des essais. Elles admettent par liste : ce qui
n'est pas dans la liste fait tomber la règle, quel qu'il soit.

- `SourceRulesTests.Shipped_code_opens_files_only_where_listed_and_never_reads_a_document_there` : la liste des 27
  fichiers qui ouvrent ou regardent un chemin ; dans `System.IO` et les espaces de noms voisins, tout type est une
  ouverture, sauf 13 types admis qui n'ouvrent rien (un flux déjà ouvert, un lecteur de texte, les aides de chemin
  qui ne lisent que le texte) ;
- `SourceRulesTests.Only_the_route_of_the_folder_hands_the_location_of_a_document_to_its_port` : quatre fichiers
  emploient le port, la route seule lit un document, le fichier listé ne touche le disque et le shell que par les
  attributs d'un chemin et par une fonction native ;
- `SourceRulesTests.The_port_of_the_folder_reads_the_text_of_a_path_and_uses_no_type_of_the_file_system` (point 4) ;
- `SourceRulesTests.The_listed_file_of_the_folder_gives_the_shell_a_verb_a_class_and_the_folder_and_calls_nothing_else`
  (point 6) : la structure champ par champ, un seul appel, la liste de tout ce que le fichier appelle, et **le
  fichier comparé à son texte exact**, déclaration par déclaration, commentaires à part (37 entrées). Les listes
  disent ce que chaque nom désigne, non que rien ne s'intercale : sans cette comparaison, la classe des dossiers
  rendue après coup à tout dossier, le dossier modifié avant d'être donné ou la question posée deux fois passaient.
  Toute retouche de `ShellFolderOpener.cs` demande donc de mettre cette règle à jour ;
- la surface (point 8) : `SourceRulesTests.The_port_of_the_folder_is_carried_by_its_opener_and_by_its_listed_file_and_by_no_other_type`,
  `SourceRulesTests.The_route_of_the_folder_offers_one_constructor_and_one_method_that_takes_identifiers_alone`,
  `SourceRulesTests.The_handler_of_the_folder_is_named_by_its_file_and_by_its_one_route_alone`,
  `SourceRulesTests.The_desktop_mode_registers_the_port_of_the_folder_by_four_lines_and_does_nothing_else_with_it`,
  `SourceRulesTests.The_members_of_the_admitted_files_that_take_a_text_are_those_of_the_port_and_of_its_listed_file`,
  avec leurs exemples dans `SurfaceSamplesTests` ;
- `NativeCodeRulesTests.The_native_functions_of_the_shipped_code_are_those_listed_file_by_file`,
  `NativeCodeRulesTests.The_shipped_code_reaches_native_code_and_com_by_its_listed_functions_alone` et
  `FrameworkRulesTests.The_shipped_code_uses_the_listed_types_of_native_code_and_the_listed_members_of_its_memory` :
  la liste exacte des fonctions natives (42 dans 6 fichiers), aucun autre chemin nommé vers le code natif ni vers
  COM (un détour par une bibliothèque admise n'est vu que si un détecteur le nomme, F-197 ; le pilote de SQLite
  est tenu à trois fonctions listées, l'option donnée à `sqlite3_db_config` comprise, et le chargement d'une
  extension de SQLite est nommé), les 13 types du code natif employés et les 6 usages de sa mémoire, fichier par
  fichier ;
- `FrameworkRulesTests.The_shipped_code_uses_the_listed_namespaces_of_the_framework_and_of_its_libraries_and_no_other` :
  tout le code livré hors du cœur n'emploie que 102 espaces de noms listés ;
- `FrameworkRulesTests.A_file_that_reads_a_document_uses_what_is_admitted_for_it_and_nothing_else` : les 20 fichiers
  qui lisent un document ou son adresse sont tenus à leurs espaces de noms, à des types exacts et à trois membres de
  la description d'un type ; aucun appel par un nom, rien de natif, aucun chemin ouvert ;
- `FrameworkRulesTests.The_shipped_code_calls_by_a_name_written_in_a_text_only_where_listed` : l'appel par un nom
  écrit en texte, que les symboles ne suivent pas, n'existe que dans 7 fichiers listés avec leur raison, chaque
  occurrence comptée ; aucun ne lit un document ;
- `PublicationRulesTests.The_shipped_code_starts_no_process_but_the_browser_and_the_zip_carries_no_script` et
  `FrameworkRulesTests.A_process_is_named_by_the_file_that_opens_the_browser_and_by_no_other` ;
- `FrameworkRulesTests.A_build_compiles_the_text_that_the_rules_read_and_no_other` : aucune ligne C# de `src` ni de
  `tests` n'est gardée ou écartée par un symbole de construction (`#if`, `#define`) ; sous `src` et `tests`, les
  règles n'écartent que `bin` et `obj` à la racine d'un projet, tout fichier C# est lu par elles, aucun fichier
  d'un autre langage ni page Razor n'y est admis, et aucun fichier compilable ne se trouve dans un dossier que
  `.gitignore` écarte du dépôt ; les projets du code livré, leurs profils de publication et leurs fichiers `.user` ne portent aucun
  des dix réglages qui changent ce que la construction compile. Une ligne sous `#if RELEASE`, ou un fichier rangé
  sous un dossier `obj` plus bas que la racine d'un projet, était compilé à la publication sans qu'aucune règle le
  lise ;
- `FrameworkRulesTests.The_files_that_a_tool_wrote_are_migrations_and_use_the_model_of_the_base_and_nothing_else` :
  les fichiers écrits par un outil, compilés et livrés mais lus par aucune autre règle, sont les 72 fichiers des
  migrations et rien d'autre, tenus à leur propre liste ;
- `ExtraRulesTests.No_test_nor_journey_asks_the_shell_of_windows_to_show_a_folder` : chacun des 17 endroits des 9
  fichiers d'essai qui construisent un hôte lui donne un shell qui enregistre, et aucun essai ne nomme la classe du
  shell autrement que pour lire ses quatre membres statiques.

Les exemples de ces règles sont dans `FrameworkSamplesTests` (dont les 25 fonctions du cadre que la seconde
relecture avait fait passer hors de toute liste, chacune vue par sa règle), `SurfaceSamplesTests` et
`ExtraRulesTests`.

Côté écran : `location.test.tsx` (le bouton de copie dit ce qu'il copie, et le refus du presse-papiers ; le dossier
demandé par les seuls identifiants du document, le refus de l'hôte dit tel qu'il l'a écrit, un second clic qui ne
demande rien pendant l'attente ; pas de bouton du dossier en mode Serveur, ni pour un lien, ni pour un document sans
emplacement), `DocumentFolder.test.tsx` (le mode Poste porté du profil jusqu'à la fiche) et `styles/sheet.test.ts`
(la ligne d'un document garde son nom lisible en entier, ses commandes à l'écart).
