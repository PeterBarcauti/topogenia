<!-- 114-collection-de-modeles.md -->
<!-- The template collection .pmaps (3.4, lot 6): an envelope of whole .pmap documents kept as they are, one reader that hands every process to the reader of the .pmap, a read-only preview for the administrator, one project per ticked line by the existing opening route, the schema of the envelope alone -->

# 114 - La collection de modèles (`.pmaps`)

Statut : acceptée le 21/11/2026 (3.4, lot 6). Prolonge les ADR 054 (`.pmap`), 105 (`.pmap` 1.5), 108 (l'aperçu avant
écriture) et 111 (la galerie ouverte dans le navigateur, le code proposé libre). Demande du plan de la 3.4 : la galerie
du site offre, par domaine, tous ses modèles en un seul fichier, qui crée un projet par processus.

## Contexte

- Un `.pmap` décrit un processus ; un domaine de la galerie en compte plusieurs. Les ouvrir un à un demande autant de
  téléchargements et d'ouvertures.
- Le lecteur du `.pmap` refuse toute propriété inconnue à la racine et tout `format` autre que `topogenia`
  (`PmapReader.cs`, `DocumentProperties`). Une enveloppe ne peut donc pas être un `.pmap` élargi, et la 3.3 la refuse
  d'elle-même (`PMAP_FORMAT`, `PMAP_PROPRIETE_INCONNUE`) : c'est le refus attendu d'une version qui ne la connaît pas.
- L'ouverture d'un fichier (`POST /api/projects/import-pmap`, administrateur) crée un projet dans une seule unité de
  travail, avec son événement `project.imported` ; l'insertion (ADR 108) montre un aperçu avant toute écriture.
- Les codes de faute des fichiers (`FileErrorCodes`) sont dans le cœur fermé : aucun ne s'y ajoute.
- L'application n'appelle pas internet (ADR 014) : elle ne peut pas lire la galerie, la personne télécharge.

## Décision

1. **La collection `.pmaps`, une enveloppe d'éléments `.pmap` tels quels.** Un objet JSON UTF-8 :
   `{ "$schema"?, "format": "topogenia-collection", "version": "1.0", "name", "description"?, "processes": [ … ] }`.
   Chaque élément de `processes` **est** un document `.pmap` complet, de version 1.1 à 1.5, comme valeur JSON : ni texte
   échappé, ni format parallèle. `format` vaut `PmapFormat.Name` suivi de `-collection`. L'enveloppe ne porte ni langue
   ni code : chaque `.pmap` porte les siens (une seule source par fait). Une collection par langue (les modèles de la
   galerie existent en un fichier par langue).
2. **Un seul lecteur, qui délègue chaque élément au lecteur du `.pmap`.** `PmapCollectionReader` (échange) nettoie le
   texte comme un `.pmap` (`PmapReader.Clean` : marque d'ordre des octets, bloc de code Markdown), le lit avec les mêmes
   options (virgules finales, commentaires, profondeur 256), contrôle l'enveloppe, puis passe le texte de chaque élément
   à `PmapReader.Read` avec les réglages de l'ouverture : aucune règle du `.pmap` n'est réécrite. Une faute de
   l'enveloppe refuse le fichier entier (`FICHIER_INVALIDE`) ; une faute d'un élément ne refuse que lui. La frontière est
   celle du schéma : ce que décrit le schéma de l'enveloppe (dont « chaque élément est un objet ») est l'enveloppe, le
   reste est l'élément. Codes réemployés : `PMAP_VIDE`, `PMAP_JSON`, `PMAP_PAS_OBJET`, `PMAP_FORMAT`, `PMAP_VERSION`,
   `PMAP_PROPRIETE_INCONNUE`, `PMAP_OBLIGATOIRE` (liste absente ou vide), `PMAP_TYPE_LISTE`, `PMAP_TYPE_OBJET`, les codes
   des textes ; un seul code nouveau, `COLLECTION_TROP_DE_PROCESSUS` (`{count}`, `{max}`), hors du cœur dans
   `CollectionErrors`, son message dans les dix-huit fichiers de messages de l'hôte. Le port `IPmapFormat` gagne
   `ReadCollection`.
3. **Les bornes** : celles du `.pmap` pour l'enveloppe entière (corps de 10 Mo, 8 millions de caractères, JSON de 256
   niveaux) ; **50 processus au plus** (`PmapFormat.ProcessesMax`) ; par élément, celles du `.pmap` (5 000 étapes,
   200 couloirs par carte, 12 niveaux, 30 fautes).
4. **Un aperçu en lecture seule, pour l'administrateur.** `POST /api/projects/import-collection/preview`, corps
   `{ "content" }`, lu après le droit, limité à 10 Mo avant lecture. Rien n'est écrit : ni projet, ni journal des
   opérations, ni audit. Par élément : son nom, sa langue, son code, le code proposé, sa description, ses nombres, ses
   points à vérifier, ses fautes (chemin, code, message dans la langue de la personne), et `content`, le texte de
   l'élément tel que l'hôte l'a lu. L'interface renvoie ce texte tel quel ; elle ne relit jamais l'enveloppe.
5. **La création par la route d'ouverture existante, un projet par ligne cochée.** Pour chaque ligne cochée, dans
   l'ordre, l'interface envoie `POST /api/projects/import-pmap` (`{ content, code, name }`) : tout ou rien par projet,
   une seule écriture et un seul droit à tenir. Une ligne refusée (code pris entre-temps, fautes) est marquée et les
   suivantes continuent ; elle garde son code modifiable et peut être réessayée. Rien n'est envoyé avant « Créer les
   projets », jamais une ligne décochée. L'administrateur devient le seul animateur de chaque projet créé, comme à
   l'ouverture d'un fichier.
6. **Le code proposé est libre, en comptant les codes déjà proposés.** Le code du fichier, sinon celui que propose son
   nom (ADR 111) ; puis le premier code libre de la règle de la corbeille (`ProjectCodeRules.TrashCode`, appelée telle
   quelle), en comptant aussi les codes proposés plus haut dans la liste : ACHAT pris donne ACHATA ; deux éléments ACHAT
   donnent ACHAT puis ACHATA. Chaque code reste modifiable.
7. **Le schéma publié décrit l'enveloppe seule** : `schemas/topogenia-collection-1.0.schema.json`, dans le sous-ensemble
   de JSON Schema que lit `SchemaValidator` (ADR 051). `processes.items` est un objet, sa description renvoie au schéma
   `.pmap` de la version que chaque élément déclare ; les essais valident l'enveloppe par son schéma, puis chaque
   élément par celui de sa version.
8. **L'application ne télécharge rien ; le site sert les fichiers et leur empreinte.** Les pages de la galerie offrent
   chaque `.pmap`, la collection de chaque domaine et de chaque langue, leur empreinte SHA-256 et le texte de chaque
   modèle à copier ; « Ouvrir un fichier… » accepte le fichier choisi, déposé ou collé.

## Écarté

- **Un `.pmap` élargi** (une liste dans le `.pmap`, ou un autre `format` sous l'extension `.pmap`) : refusé par la 3.3 de
  façon obscure, et deux contenus sous un même nom.
- **Une archive** (ZIP de `.pmap`) : illisible au Bloc-notes, et refusée par le service informatique le plus strict.
- **Les éléments en texte échappé** : illisibles, et la porte d'un second format.
- **Une route de création groupée** : une seconde écriture et un second droit à tenir, à côté de l'ouverture.
- **La relecture de l'enveloppe dans l'interface** : deux lecteurs d'un même format.
- **Le schéma `.pmap` recopié dans `$defs`** du schéma de la collection : deux sources de la même forme.
- **Un code de faute dans le cœur fermé** : interdit ; `CollectionErrors` suffit.
- **Une galerie lue par l'application** : un appel sortant (ADR 014).
- **L'extension `.json`** : le fichier perd son type et s'ouvre dans la visionneuse du navigateur.

## Conséquences

- Une route nouvelle, en lecture seule (`import-collection/preview`, administrateur) ; aucune route d'écriture
  nouvelle, aucune migration, aucun fichier du cœur fermé, aucune dépendance, aucun appel sortant.
- Une collection s'ouvre dès la 3.4 ; chacun de ses éléments, copié seul, s'ouvre dans la version qui lit sa version de
  `.pmap` (dès la 3.3 pour un `.pmap` 1.5).
- Un processus dont la langue n'est pas configurée est refusé seul (`PMAP_LANGUE`) : la page du domaine offre la
  collection de l'autre langue.
- Format : `docs/format-collection.md` ; exemple : `samples/exemple.pmaps`.

## Vérification

Essais écrits avec le code de chaque partie :

- Échange : `PmapCollectionReaderTests` (1, 2 et 50 processus dans l'ordre ; 51 refusés avec leur nombre et le maximum ;
  liste vide ou absente ; autre format ou version, un `.pmap` n'est pas une collection ; propriété inconnue de
  l'enveloppe à sa place ; un élément invalide parmi des valides ; règles du `.pmap` par élément ; nettoyage, commentaires
  et virgules finales ; JSON de plus de 256 niveaux ; le texte de chaque élément s'ouvre seul en un même document) et
  `CollectionDocumentationTests` (le schéma se charge, l'exemple le suit, chaque élément suit le schéma de sa version).
- Application et hôte : `PreviewCollectionQueryTests` (code proposé, code pris ou à la corbeille, deux codes égaux, code
  tiré du nom, rien d'écrit) ; `CollectionPreviewRoutesTests`, deux bases (aperçu complet, fautes traduites, enveloppe
  fautive, 413 avant lecture, corps jamais lu sans le droit, ouverture d'une ligne tout ou rien, code pris après l'aperçu,
  ni audit ni opération) ; lignes de `RightsMatrixTests` (administrateur 200, animateur, contributeur et lecteur 403,
  anonyme 401).
- Interface et parcours : `collection.test.tsx`, `web\e2e\collection-34.spec.ts` en Poste et en Serveur.
- Témoins de mutation : le contrôle de `ProcessesMax`, la délégation au lecteur du `.pmap`, `[RequiresAdmin]` de
  l'aperçu, une écriture par l'aperçu, la garde « rien avant Créer » et le filtre des lignes cochées.

## Révision (23/11/2026, pré-audit de la copie 68) : clés doublées, emplacements qui sortent du poste

Une clé écrite deux fois dans un même objet refuse le fichier (`PMAP_JSON`, avec la ligne et la colonne de la seconde),
pour un `.pmap` comme pour l'enveloppe ou un processus d'une collection : le lecteur gardait la dernière, la relecture
du texte voit la première. L'aperçu de chaque processus, l'aperçu d'une insertion et la réponse de l'ouverture d'un
fichier portent `remote_locations`, le nombre d'adresses distinctes de documents qui mènent à une autre machine
(adresse web, chemin réseau), que l'interface dit avant qu'un lien soit suivi ; un fichier sans point à vérifier mais
avec de telles adresses montre aussi « Projet créé ». Aucun des 32 modèles publiés n'en porte, ni aucune clé doublée.
