<!-- 110-recherche-dans-le-projet.md -->
<!-- Search in the project (3.4, lot 2): one read query on the host that walks the whole project under the rights of the sheets, a fold rule shared with the interface by a common table of cases, a side panel opened by Ctrl+F, a filter of the project list on the home page -->

# 110 - Recherche dans le projet

Statut : acceptée le 21/11/2026 (3.4, lot 2). S'appuie sur les ADR 083 (langues), 106 (vue BPMN : le modèle de la
lecture sans événement) et 046 (journal des requêtes, limites de taille). Demande du porteur du projet : retrouver un
outil ou un document cité dix niveaux plus bas, sans ouvrir les cartes une à une.

## Contexte

- L'interface ne tient qu'une carte à la fois (`GET /api/maps/{id}`), et chaque carte monte un éditeur neuf. La carte ne
  donne de chaque fiche que son résumé : les outils n'y sont pas.
- La seule piste existante, la copie `GET /api/maps/{id}/pmap?purpose=view`, lit le projet entier pour ne rendre qu'une
  carte. Chercher ainsi dans un projet de cent cartes ferait cent lectures du projet et enverrait au navigateur tout ce
  qui est lisible pour n'en garder que quelques lignes.
- Ce que chaque objet laisse lire : étapes, couloirs, documents et irritants sont vus de tout membre (le plan
  d'actions donne à tout lecteur tous les irritants d'une étape, tous états confondus) ; seuls les **outils** dépendent
  du droit, car ils sont dans la fiche, donc dans le brouillon ou dans une version validée.
- Seuls les projets, cartes, couloirs, étapes, flèches et résumés ont des traductions ; documents, outils et irritants
  n'ont que leur texte, dans la langue de saisie.
- Aucune recherche d'un mot sans accent ni majuscule n'existait : seuls un repli de nom de fichier et un repli d'en-têtes
  de tableau, deux objets différents, non repris.

## Décision

1. **Une requête de lecture côté hôte, `POST /api/projects/{id}/search`** (rôle lecteur). Une `ProjectQuery` de
   `Application` (dossier `Search\`), sur le modèle de la vue BPMN et du plan d'actions : le droit, puis le chargement
   sans verrou ni écriture ; ni journal des opérations, ni événement d'audit. Le mot passe **dans le corps d'un `POST`**,
   jamais dans l'adresse : IIS écrit par défaut la chaîne de requête de chaque adresse, et le mot cherché est un
   contenu métier. Le journal du produit n'écrit qu'une ligne sans contenu par recherche (niveau Information, comme tout
   `POST`). Corps `SearchBody(Text, Language, MapId)` : le mot, la langue montrée (absente : la langue d'origine
   seule), la carte ouverte, mise en tête (inconnue ou supprimée : ignorée, jamais une erreur). Refus `VALEUR_INVALIDE`
   pour un texte vide après `Trim` ou de plus de 100 caractères, et pour une langue de plus de 16 caractères ou hors de
   `[a-z-]`, vérifiés après le droit et avant le chargement (`ProjectQuery.CheckRequest`) : une demande refusée ne lit
   rien du projet. Non-membre 404, anonyme 401, projet à la corbeille 404. La requête marche `Subtree(RootMap.Id)` (cartes
   vivantes atteignables), la carte demandée d'abord ; dans une carte, les couloirs puis les étapes en ordre de lecture,
   et pour chaque étape : elle-même, ses documents, ses outils, ses irritants. Un résultat par objet. Réponse en
   `snake_case` : `ProjectSearchView(Total, Maps)`, chaque carte avec son niveau et ses résultats (genre `step`, `lane`,
   `document`, `tool`, `pain_point`, `action`, texte où le mot a été trouvé, sa langue, sa place `Start` et `Length`, et
   depuis la retouche R9b la liste `Marks` de chaque mot marqué, point 3).
   Choisir un nom d'objet reprend `NamedText` du plan d'actions. Sont cherchés : nom et code des étapes, noms des
   couloirs, noms des documents (chez l'étape qui les tient), outils des fiches, code et description des irritants (tous
   états, étapes vivantes seulement), actions des irritants. Ne sont pas cherchés : texte libre, but, déroulé, libellés
   des flèches, noms des cartes, résumés, responsable d'une action (un nom de personne) ; ils sont inscrits dans
   `docs\a-peaufiner.md` (non publié).

2. **La règle des fiches est appelée, jamais réécrite ; le reste est visible de tout membre.** Les outils sont lus par
   `SheetVisibility.VisibleContent(workspace, step.Id, SheetRights.ViewerOf(workspace, step, role, userId))`, sauf pour
   une étape sans contenu (`StepTypes.HasNoContent`, aucun outil), c'est-à-dire ce que la copie d'une carte donne au
   format. L'animateur et l'administrateur trouvent le brouillon de chaque fiche ; le contributeur le brouillon des
   fiches qu'il peut remplir, la dernière version validée ailleurs ; le lecteur la dernière version validée seulement,
   rien d'une fiche jamais validée. Le droit ne change qu'un objet : l'outil. Un essai d'équivalence tient la règle : les
   outils trouvés sont ceux que montre la copie `purpose=view` de la carte, pour chaque rôle, sur les deux bases.

3. **Le repli du mot, écrit deux fois, tenu égal par une table de cas commune.** `SearchText.Fold` et `Find` côté hôte
   (`Application\Search\SearchText.cs`), `fold.ts` côté interface (`web\src\shared\text\`, pour le filtre de l'accueil) ;
   les deux lisent `web\src\test\oracle\search-fold.json` (texte, mot, trouvé ou non, place attendue). Le repli se fait
   caractère par caractère (par `Rune`), chaque caractère replié gardant sa place d'origine : décomposition NFKD ; retrait
   des seules marques U+0300 à U+036F (accents latins, grecs, cyrilliques ; le dakuten japonais U+3099 est gardé : が
   n'est pas か) ; minuscules invariantes ; puis ß→ss, œ→oe, æ→ae, ø→o, ł→l, đ→d, ı→i, ς→σ, ’ et ‘ → ', – et — → -,
   toute espace (y compris insécable, idéographique, tabulation) → espace simple, sans fusion de plusieurs espaces. Le mot
   cherché est replié et coupé de ses espaces de bord ; la comparaison est ordinale sur les textes repliés (« fact »
   trouve « Facture », comme Ctrl+F d'un navigateur). **Précisions que la table fixe** : (a) on rend la première
   occurrence, en unités UTF-16 du texte d'origine (celles de JavaScript) ; (b) le résultat couvre les caractères
   d'origine entiers que le mot touche : « co » trouvé dans « cœur » marque « cœ », et les marques retirées qui suivent
   directement la dernière lettre trouvée sont comprises (« cafe » marque « café » écrit avec une marque combinante) ;
   (c) un résultat ne commence ni ne finit au milieu d'une lettre et de sa marque gardée : « か » ne trouve ni « が » ni
   « か » suivi de U+3099, quelle que soit la forme (précomposée ou décomposée) du texte et du mot ; (d) un mot vide
   après repli ne trouve rien. **Langues** : on cherche dans la langue montrée (`shownLanguage`) puis dans la langue
   d'origine, jamais les autres (des résultats dans des langues que la personne ne lit pas) ; un code n'a pas de langue.
   **Plusieurs mots (retouche R9, 21/11/2026)** : la requête est coupée à ses espaces (`SearchTerms`, au-dessus de
   `SearchWord`, inchangé) ; un texte est trouvé quand il contient **chaque mot**, dans n'importe quel ordre, par la règle
   ci-dessus, et tous dans **le même texte** (un nom, un code, un document, un outil, une description), jamais répartis
   entre le nom et le code d'une étape, entre une étape et son document ou entre deux outils ; un mot répété (replié) ne
   compte qu'une fois ; un mot d'un seul caractère (compté comme une personne le lit) est ignoré s'il reste un mot plus
   long, gardé si tous n'ont qu'un caractère ; chaque mot est marqué à sa première place, les marques qui se recouvrent ou
   se touchent fusionnées (« site sit » marque « Site » une fois). Une requête d'un mot rend exactement ce qu'elle rendait.
   **Chaque mot marqué (retouche R9b, 21/11/2026)** : l'hôte reste la **seule source des marques**. Chaque résultat
   porte la liste `marks` (`[{start, length}]`, schéma `TextMatch`, unités UTF-16), triée, sans recouvrement ni contact,
   une marque au moins. **Ajout pur** au contrat : `start` et `length` restent, égaux à la première marque (une requête
   d'un mot rend la même place qu'avant, et un lecteur du contrat qui ne lit qu'eux garde un sens) ; retirer ces deux
   champs aurait cassé ce qui les lit, pour une économie de deux nombres. L'interface ne refait pas la règle des
   marques : elle dessine celles que l'hôte donne, après un contrôle de forme (une marque hors du texte, vide, ou qui
   commence avant la fin de la précédente n'est pas dessinée ; aucune restante : le texte sans marque) ; l'ancien
   repli de l'interface, qui recherchait le mot tapé quand la place était hors du texte, est retiré (il aurait écrit la
   règle des marques une seconde fois). L'extrait coupe autour de la **première** marque, 36 caractères de chaque côté,
   et montre les suivantes tant qu'elles tiennent **entières** dans la place après la première ; une marque que la
   coupe couperait n'est pas montrée, l'extrait s'arrête avant elle. La **coupe des mots** est, elle, écrite des deux
   côtés : le filtre de l'accueil (point 7) suit la même règle (`foldedTerms`, `holdsTerms` dans `fold.ts`), tenue
   égale par la liste `queries` de la table commune (texte, requête, trouvée ou non, et les marques, vérifiées côté hôte
   seulement puisque l'hôte seul les calcule).

4. **Bornes et coût.** 200 résultats rendus au plus, avec le total (« Les 200 premiers résultats sur 1 234 : préciser la
   recherche. ») ; texte de 1 à 100 caractères ; corps de 4 Ko au plus (`RequestSizeLimitAttribute`, refus avant lecture) ;
   côté interface, 300 ms après la dernière touche, dès deux caractères (un seul sur Entrée, pour un idéogramme), une
   recherche nouvelle annule la précédente (`AbortController`). Coût : un chargement du projet puis une passe en
   mémoire. Deux pièges de coût sans toucher au cœur : les traductions de la langue montrée sont indexées une fois (un
   dictionnaire par objet, `TranslationRules.Of` parcourt toutes les traductions à chaque objet) ; la recherche trie
   d'abord les **candidates** (une fiche dont le brouillon ou la dernière version contient le mot, par des index
   construits une fois, car `SheetQueries.OperationsOf` et `LatestVersion` parcourent tout à chaque fiche), puis n'appelle
   la règle des fiches que pour elles. Le tri ne décode que ce qui peut être montré : le brouillon, puis, s'il ne contient
   pas le mot, la dernière version validée, jamais une version plus ancienne (aucun rôle n'en voit une :
   `SheetVisibility.VisibleContent` rend le brouillon ou `LatestVersion`). Pour chaque candidate, la règle est appelée
   telle quelle sur un **jeu réduit** du projet (le projet, l'étape, son couloir, sa carte, ses fiches, son déroulé et ses
   versions) et non sur le projet entier : 5 000 étapes de dix lignes, mot présent partout, 0,6 s au lieu de 5,1 s.
   Mesure de la retouche R2a (5 000 étapes, chaque fiche un brouillon et dix versions validées ; construction Debug,
   magasin en mémoire des essais, meilleur de cinq passes sur trois séries, la passe en mémoire seule puis la requête
   entière) : mot absent des outils, 240-320 ms → 61-70 ms (requête 280-320 ms → 118-142 ms) ; mot des seules versions
   anciennes, 93-124 ms → 34-40 ms (131-167 ms → 71-84 ms) ; mot du brouillon ou de la dernière version, inchangé
   (50-110 ms). Le tri ne décide jamais de ce qui est montré : un essai de propriété (cent projets d'une graine fixe,
   chaque rôle, jusqu'à cinq versions par fiche dont des anciennes aux outils propres) vérifie qu'il ne change aucun
   résultat, que la règle n'est jamais appelée pour une fiche dont seule une version ancienne contient le mot, et compare
   le jeu réduit à la règle appelée sur le projet entier pour chaque étape. Mode Serveur : lecture sans verrou, pas de
   cache (il faudrait l'invalider à chaque écriture de chaque utilisateur), pas de limiteur de débit : F-151 (à concevoir
   avec l'exploitation) reste ouvert, la recherche y est citée.

5. **Le volet « Rechercher ».** Un volet latéral réductible à droite (`DockPanel` `'search'`), pas une fenêtre posée sur
   le schéma. Son état est un **magasin de la page** (`features\map\search\searchStore.ts`), hors de l'éditeur : chaque
   carte monte un éditeur neuf, le rang du résultat courant doit survivre au changement de carte ; jamais gardé dans
   le navigateur, vidé quand on ouvre un autre projet. Les résultats sont rangés par carte, « Cette carte » en tête,
   chaque mot trouvé marqué (un `<mark>` par marque de l'hôte, R9b) ; un texte trouvé dans la langue d'origine dit sa
   langue. Aller à un résultat passe par l'arrivée de F-64 (`useLeadTo`, `arrival`) : même carte, l'étape (ou les
   étapes du couloir) est choisie et mise au centre ; autre carte, elle s'ouvre, puis choisit et centre à l'arrivée ; un document, un outil, un irritant mènent à
   leur étape ; un objet disparu depuis : la carte s'ouvre, rien n'est choisi. **Dans la vue BPMN**, qui n'a pas de
   `reveal`, un résultat ferme la vue puis mène à l'étape sur la carte. Entrée = suivant, Maj+Entrée = précédent,
   Échap = ferme le volet et rend le focus à la carte ; le résultat ne suit pas les écritures (relu quand le texte change
   et à chaque ouverture). Une fiche ouverte couvre le volet, qui revient quand elle se ferme ; l'assistant ou un calque
   ouvert le ferme. Bouton « Rechercher » en tête de la zone Affichage (la seule zone vue de tous les rôles et dans la
   vue BPMN), commande `find`, ligne dans l'aide-mémoire du lot 1.

6. **Ctrl+F : pris au navigateur ou laissé.** Pris par la couche du canevas (et de la vue BPMN) du gestionnaire du
   clavier, quand le focus est sur le fond, une étape, le ruban ou un volet non modal, même quand une étape seule est
   choisie ; l'action du navigateur est empêchée, le volet s'ouvre, le focus va au champ dont le texte est sélectionné.
   Laissé au navigateur : dans un champ de saisie, **champ de la recherche compris** (un second Ctrl+F ouvre la
   recherche du navigateur : la porte de sortie), sous une boîte de dialogue, sur les autres pages (accueil, membres,
   nouveau projet, aide). Muet pendant une présentation. Pas de `useGlobalShortcut` (Ctrl+S) : il prendrait Ctrl+F
   jusque dans les champs et sous les boîtes. Ni `Permissions-Policy` ni `connect-src 'self'` ne changent ; aucune
   requête sortante.

7. **Le filtre de la liste des projets, dans la page de l'accueil.** Un champ « Filtrer les projets » (code ou nom)
   dans l'en-tête de la colonne, dès qu'il y a des projets : garde un projet dont le code, le nom montré (langue de
   l'interface) ou le nom d'origine contient le mot, par le repli du point 3 ; depuis R9b, chaque mot tapé, dans
   n'importe quel ordre, tous dans **le même texte** (le code, ou un des deux noms), par la coupe des mots du point 3
   (`filterProjects`, `foldedTerms`, `holdsTerms`) ; compteur « 3 sur 12 » ;
   Échap vide le champ. La liste est déjà lue par la page : rien ne part à l'hôte. Le projet mis en avant et la corbeille
   ne changent pas.

## Écarté

- **Chercher dans le navigateur sur la copie `view` de chaque carte** : une lecture du projet par carte, et tout le
  lisible envoyé pour n'en garder que quelques lignes.
- **Une copie de tout le projet en `view`** : un format de lecture de plus, lourde sur un grand projet.
- **Une chaîne de requête (`GET ?q=`)** : le mot, contenu métier, serait écrit par les journaux d'IIS.
- **Les dix-huit langues à la fois** : des résultats dans des langues que la personne ne lit pas.
- **Un index en base** : une migration, interdite pour ce lot, et un index à tenir à chaque écriture.
- **Une bibliothèque de recherche approchée** (Fuse.js, Lunr) : une dépendance livrée de plus ; le repli suffit.
- **Un cache de l'hôte** : à invalider à chaque écriture de chaque utilisateur.
- **Un limiteur de débit** : une décision d'exploitation (F-151), laissée ouverte.
- **Un raccourci global pour Ctrl+F** : il fermerait la porte de sortie vers la recherche du navigateur.

## Conséquences

- Aucun fichier du cœur fermé ni migration : la requête vit dans `Application`, lit le `ProjectWorkspace` chargé par le
  port existant (contenu `Sheets | Documents | Translations`) et n'appelle du cœur que des fonctions publiques
  (`RootMap`, `Subtree`, `LanesOf`, `ReadingOrder`, `DocumentRules.InEntryOrder`, `SheetRights.ViewerOf`,
  `SheetVisibility.VisibleContent`, `StepTypes.HasNoContent`). La recherche marche les sous-cartes par `Subtree`, la
  seule descente admise.
- Une route de plus, ouverte au lecteur : une ligne dans la matrice des droits et dans la table des routes, le contrat
  OpenAPI et `schema.ts` réécrits (R9b : `marks` et le schéma `TextMatch` ajoutés, `CONTRACT_WRITE=1` puis `npm run api`).
- Une règle de repli à tenir à deux endroits ; toute évolution passe d'abord par la table commune.
- Limite connue : le repli NFKD sépare les syllabes hangul en jamos ; chercher une seule consonne coréenne peut trouver
  une syllabe. Non traité dans ce lot.
- Les documents, outils et irritants n'ont qu'une langue : on les cherche tels que saisis.
- Un projet de cinq mille étapes (le plafond d'un `.pmap`) coûte un chargement et une passe, bornée à 200 résultats.

## Vérification

- Hôte : `ProjectSearchQueryTests` et `ProjectSearchNarrowingTests` (application) : tous les genres de résultat sur
  tous les niveaux, ordre (carte demandée d'abord), accents et places d'origine, langues, droits de chaque rôle,
  étape sans contenu, objets supprimés, 5 000 étapes et 200 résultats, un seul chargement sans écriture ni journal, refus
  du texte et de la langue sans aucun chargement, tri des candidates sans effet sur le résultat, version ancienne jamais
  candidate (5 000 étapes de dix versions comprises). `ProjectSearchRoutesTests` (deux bases) :
  droits par rôle, équivalence avec la copie `view`, aucun événement d'audit, 404 et 401, mot absent du journal,
  corps trop grand refusé avant lecture, liste `marks` de deux mots (R9b). `SearchFoldTests` : chaque cas et chaque
  requête de plusieurs mots de la table commune, marques comprises. `RightsMatrixTests`,
  `RouteTableTests`, `ApiContractTests` à jour.
- Interface : `fold.test.ts` (même table, requêtes comprises), `projectFilter.test.ts` (plusieurs mots, R9b) et
  `HomeScreen.test.tsx`, `searchResults.test.ts` (marques contrôlées, extrait qui garde la première marque et les
  suivantes entières), `SearchPanel.test.tsx` (deux mots marqués, envoi différé, annulation, rangement, clavier,
  changement de carte, vue BPMN, fiche qui couvre le volet), `keys.test.ts` (Ctrl+F pris et laissé), essai du ruban,
  `shortcutSheet.test.ts`.
- Parcours : `web\e2e\search-34.spec.ts`, en mode Poste et en mode Serveur (un cas de deux mots, recherche et accueil,
  écrit en R9b).
- Témoins de mutation vus rouges puis remis : le spectateur forcé à voir le brouillon ; le rôle forcé à animateur ;
  `HasNoContent` retiré ; `Subtree` remplacé par toutes les cartes ; le tri des candidates qui écarte la dernière version
  validée ; le tri qui prend la plus ancienne version au lieu de la dernière (R2a) ; l'équivalence avec la copie `view`
  vue rouge avec le spectateur forcé, à travers l'hôte.
