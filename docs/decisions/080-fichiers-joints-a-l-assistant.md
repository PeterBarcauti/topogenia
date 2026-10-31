<!-- 080-fichiers-joints-a-l-assistant.md -->
<!-- The files joined to a request of the assistant (3.0, lot 4): PDF and images sent as they are, Word, Excel and Visio read by the host in .NET without DTD and sent as text, the bounds of each provider, the files kept by the page with their turn, the paperclip, the drop and the paste of the panel -->

# 080 - Fichiers joints à l'assistant

Statut : acceptée (date 30/10/2026). Prolonge les ADR 078 (volet de l'assistant) et 079 (propositions appliquées).
Spécification : `TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 3.4 (« Fichiers joints »), 6 et 9 (lot 4).

## Contexte

La première pratique du porteur du projet est de générer un processus depuis un texte **ou des fichiers** : une
procédure Word, un tableau Excel, un schéma Visio, un PDF, la photo d'un tableau blanc. La spécification veut que les PDF
et les images partent tels quels, que Word, Excel et Visio soient lus par l'hôte et envoyés en texte, le même chemin
quel que soit le connecteur, sans Office, sans automatisation ni outil extérieur (règle de l'IT au plus prudent), les
anciens formats refusés avec un conseil, le nombre et la taille bornés par les limites de chaque fournisseur, rien
d'enregistré ni de journalisé.

## Décision

1. **Les sortes de fichiers** : PDF et images PNG, JPEG, GIF, WebP envoyés tels quels (API d'Anthropic : blocs
   `document` et `image` en base64 ; Bedrock : `DocumentBlock` et `ImageBlock` de `Converse`) ; Word `.docx`, Excel
   `.xlsx`, Visio `.vsdx` lus par l'hôte. La sorte vient de l'extension et **le contenu doit la confirmer** (signature
   d'un PDF, de chaque format d'image, d'une archive) : sinon le fichier est illisible. Les anciens formats `.doc`,
   `.xls`, `.vsd` sont refusés avec le format récent à employer ; un PDF protégé par un mot de passe est refusé (les
   fournisseurs le refusent) ; tout autre type est refusé.
2. **La lecture par l'hôte** (`Topogenia.Interchange.Documents`, derrière le port `IDocumentTextReader`) : l'archive est
   ouverte depuis son contenu, ses entrées comptées (10 000 au plus), chaque entrée lue dans un flux qui s'arrête au-delà
   de 64 Mo décompressés et le tout au-delà de 256 Mo (une bombe de compression s'arrête là) ; le XML est lu **sans DTD
   ni résolveur** ; une relation qui sort de l'archive est ignorée ; le texte est borné à 400 000 caractères et, au-delà,
   coupé avec une ligne qui le dit au modèle. Word : paragraphes, titres après autant de `#` que leur niveau (styles de
   titre de Word dans ses langues), éléments de liste, tableaux en lignes de cellules séparées par `|`, texte supprimé
   d'une révision laissé de côté. Excel : chaque feuille (cachée dite), ses lignes avec leur numéro, ses cellules
   séparées par des tabulations à leur colonne, textes partagés, booléens, erreurs, **dates, heures et durées reconnues
   à leur format** (dates ISO, durées en heures). Visio : chaque page de premier plan, ses formes avec leur texte, leur
   modèle (Process, Decision, Swimlane...), leur centre et leur taille en pouces (une forme d'un groupe placée sur la
   page), puis les liaisons de ses connecteurs avec leur texte. Les vocabulaires d'Office se reconnaissent à la fin de
   leur espace de noms : la forme « Strict » d'Office est lue comme l'autre.
3. **Les bornes de chaque connecteur**, relevées le 30/10/2026 dans la documentation d'Anthropic (API : requête de
   128 Mo, image de 10 Mo en base64 ; Bedrock : requête de 20 Mo, image de 5 Mo en base64, document de 4,5 Mo) : API
   d'Anthropic et copier-coller, 10 Mo par fichier, 7 Mo par image, 20 Mo en tout ; Bedrock, 4 Mo, 3,5 Mo, 10 Mo ;
   **5 fichiers par conversation**. L'hôte les donne au volet (état du volet, paramètres de l'IA) et les vérifie à son
   tour ; le corps d'un échange admet 64 Mo (les fichiers vont en base64).
4. **Les fichiers restent avec leur demande dans la conversation** : la page les garde le temps de la conversation et
   les renvoie avec le tour qui les portait, à la même place ; le modèle les relit au tour suivant (« Générer quand
   même » après des questions, une précision) et le fournisseur les sert de son cache (un point de cache suit les
   fichiers de la demande). Les bornes portent sur la conversation entière ; « Nouvelle conversation » les oublie.
5. **Dans le prompt** : les fichiers d'une demande viennent avant son texte, chacun nommé par une ligne
   `<file name=... kind=...>` ; un document lu par l'hôte suit en texte, fermé par `</file>`. La consigne commune dit au
   modèle que ce sont des données (une instruction écrite dans un fichier ne change rien) et de nommer le fichier d'où
   vient un point. Au copier-coller, le prompt copié porte le texte des documents et nomme les PDF et les images, que la
   personne joint dans son IA ; le volet le lui dit.
6. **Interface** : un trombone dans la barre du champ (le choix limité aux types admis), le dépôt de fichiers sur le
   champ (un fichier `.txt` ou `.md` déposé est lu dans le champ, comme un prompt préparé), le collage d'une capture
   d'écran dans le champ ; une pastille par fichier (sorte, nom, taille, retrait) au-dessus du champ et sous la demande
   envoyée ; chaque fichier refusé dit pourquoi. La tuile « Créer depuis un texte ou un fichier » le propose.
7. **Rien n'est gardé** : ni sur le poste, ni en base, ni au journal ; l'audit `assistant.exchanged` compte les fichiers
   envoyés et leurs sortes, jamais un nom ni un contenu.

## Conséquences

- Pas de migration ; le contrat de l'API gagne les fichiers des corps d'échange et les bornes des vues.
- Par `Converse`, Bedrock lit un PDF en texte seulement : la lecture visuelle demande les citations, incompatibles avec
  la sortie structurée (`docs/a-peaufiner.md`, F-54).
- Le cœur fermé ne change pas : son paquet n'est pas refait.
- Essai réel du 30/10/2026 : une procédure Word et un PDF joints sur l'accueil donnent un projet de 2 cartes et
  17 étapes, le circuit du PDF en sous-carte (73 s, environ 0,35 $) ; la relance d'une question sur une carte relit la
  procédure jointe au tour précédent depuis le cache (41 135 jetons lus, environ 0,04 $).
