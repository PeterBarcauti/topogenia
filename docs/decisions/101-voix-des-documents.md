<!-- 101-voix-des-documents.md -->
<!-- What a presentation says of a document (3.2): its type, from the medium entered or the extension of its name, then what it holds in a few words read from its name, the numbers of its title with them (audit 3.2); never the name as a file is named; the map gives the medium of each document marker; a pure function of the interface builds the sentence -->

# 101 - La voix des documents dans la présentation

Statut : acceptée le 11/11/2026 (3.2), sur demande du porteur du projet : « on caractérise un fichier en disant son type
et ce qu'il contient, très sommairement comme un titre, mais on n'articule pas son nom ». **Révisée le 12/11/2026**,
avant toute livraison, après une relecture critique : une extension est reconnue où qu'elle soit dans le nom, et
seulement si elle est connue (points 2 et 3) ; une adresse ou un chemin ne donne que son dernier segment, les codes
sont retirés, un titre court est dit (point 3) ; les limites connues sont écrites (« Conséquences »).
**Révision du 12/11/2026 (audit 3.2, UX-MAJ-06)** : les nombres d'un titre sont dits (point 3, « ce qui est dit ») ;
retirés, ils faisaient perdre ce que le titre disait (« Certificat ISO 9001.pdf » était dit « Certificat ISO »).

## Contexte

Le mode présentateur (ADR 097) disait les documents d'une étape par leur nom : « Reçoit : Suivi_CP_2024-03_v2.xlsx. »
La voix articulait alors un nom de fichier, tirets bas, date et extension compris, et le sous-titre l'écrivait tel quel.
Un nom de fichier n'est pas fait pour être dit.

## Décision

1. **Une phrase par sens, comme avant** : ce que l'étape reçoit, ce qu'elle produit. Chaque document y est dit par son
   **type**, puis par **ce qu'il contient** : « Reçoit un tableur Excel : Suivi CP. », « Produit un document papier :
   Bon de commande. », « Reçoit un PDF. ». Le sous-titre écrit la phrase que la voix dit.
2. **Le type** vient du support saisi sur le document (ADR 095) : papier, courriel, logiciel, quel que soit le nom. Un
   tableur est dit « un tableur Excel » ou « un fichier CSV » quand une extension de son nom le dit. Un fichier, un
   autre support ou aucun : la dernière extension du nom qui dit un type, **où qu'elle soit dans le nom** (`.xlsx`,
   `.xls`, `.xlsm`, `.csv`, `.docx`, `.doc`, `.pdf`, `.vsdx`, `.vsd`, `.pptx`, `.ppt`, `.msg`, `.eml`, images :
   22 extensions). `Suivi.xlsx (onglet Congés)` est un tableur Excel, `Fiche de poste.doc.pdf` un PDF. À défaut,
   « un fichier » quand le support le dit.
3. **Le contenu** vient du nom, en trois temps (les règles une à une sont dans la spécification, § 3.21.10) :
   - **ce qui est lu** : une adresse web (un schéma et `://`, ou `www.`) ou un chemin de fichier saisi comme nom ne
     donne que son dernier segment, sans ce qui suit un `?` ou un `#`, décodé ; le serveur et les dossiers ne sont
     jamais dits (`/partage/association/Statuts.pdf` : « un PDF : Statuts »). Tout autre nom est lu entier : une
     barre oblique entre deux mots d'un titre n'est pas un chemin ;
   - **ce qui est retiré** : les marques d'un courriel répondu ou transféré (`RE:`, `TR:`, `FW:`, `FWD:`) et les
     adresses de courriel ; toute **extension connue**, où qu'elle soit (une liste fermée de 103 extensions : un
     dernier mot après un point qui n'y est pas est un mot, `Rapport.final` : « Rapport final ») ; les signes qui
     tiennent lieu d'espaces (tirets bas, tirets, points, parenthèses, guillemets, ponctuation d'Asie orientale) ;
     les **codes**, c'est-à-dire un sigle en capitales, de quatre au plus, collé à un nombre : par un trait d'union
     (`FOR-RH-007-Demande-de-conges.docx` : « Demande de conges »), par un tiret bas quand le nombre a trois chiffres
     ou plus (`IMG_1234.JPG` : « une image »), ou sans séparateur avec deux chiffres au moins (`PR012`) ; un sigle
     devant une date n'est pas un code (`Suivi_CP_2024-03_v2.xlsx` : « Suivi CP ») ; les mots sans lettre (dates,
     signes seuls, nombres qui ne sont pas ceux du titre, ci-dessous), `&` gardé ; les marques de version (`v2`,
     `rév. 4`, `version 3`), « vers » suivi d'un nombre restant un mot ; les restes d'une heure et « n° » ; une suite
     de trois chiffres ou plus collée à un mot (`scan0001` : « scan ») ;
   - **ce qui est dit** : ce qui reste, comme écrit, huit mots au plus : un titre, pas une phrase. Il faut un mot
     lisible, une lettre avec une autre lettre ou un chiffre, même séparés (`R&D`, `8D`, `5S`) ; sinon le type seul.
     **Les nombres du titre** (révision du 12/11/2026, UX-MAJ-06) : un nombre de 1 à 4 chiffres, sans zéro de tête,
     seul à côté d'un mot à lettres, avant ou après lui, est dit, avec son signe s'il en a un (`%`, `‰`, une
     monnaie, collé ou séparé) : « Certificat ISO 9001 », « Annexe 2 », « Bilan 2025 », « 3 devis comparatifs »,
     « Transfert vers 2 sites », « Budget 100 % ». Restent retirés : une suite de nombres (une date, une heure :
     `12.03.2024`, `2024-03`), un nombre après un mot de version (`version 3`, `rév. 4`), un nombre de cinq chiffres
     et plus ou à zéro de tête (une référence), un nombre à côté d'aucun mot à lettres, les codes qui mêlent lettres
     et chiffres, les chemins et les adresses (leur dernier segment seul est lu).
4. **Un document dont rien ne dit le type** (ni support, ni extension connue) est dit par son titre seul, comme avant :
   « Reçoit Bon de commande. » ; « un document » quand son nom ne dit rien non plus. Dire « un document : » devant
   chaque titre alourdirait la phrase la plus courante sans rien apprendre.
5. **Trois documents au plus par phrase**, puis « et N autres », par la liste de la langue montrée ; deux documents dits
   de la même façon sont dits une fois.
6. **Les types ont leur article dans les 18 langues**, à la forme du complément de la phrase (accusatif en tchèque,
   polonais, russe, ukrainien, grec). Quatre langues gardent une phrase en étiquette (« Entrées : … » en hongrois, turc,
   japonais, chinois) et mettent le contenu entre parenthèses.
7. **Architecture** : la vue de la carte donne le support de chaque document des marqueurs d'une étape
   (`DocumentMarkerView`, dans l'application : le marqueur du cœur et le support lu sur le document ; le cœur ne change
   pas) ; le contrat de l'API est régénéré. La phrase est bâtie par une fonction pure de l'interface
   (`presenter/model/documentVoice.ts`), sans état ni appel, testée seule.

## Conséquences

- La carte, les fiches et les exports montrent toujours le nom entier du document : seule la présentation le dit
  autrement.
- Un commentaire de présentation (ADR 099) remplace toujours les phrases de l'étape, celles des documents comprises.
- Un nom déjà écrit comme un titre (« Bon de commande ») est dit comme avant.
- Saisir le support d'un document (Makigami) améliore ce que la présentation en dit.
- **Limites connues** (révision) : un code en minuscules, un sigle suivi d'un tiret bas et d'une date
  (`IMG_20240312_101500.jpg` : « une image : IMG »), une extension hors de la liste, une adresse sans schéma ni
  `www.` restent dits ; les mots de version des autres langues que le français et l'anglais, les mois en lettres et
  les formes collées ne sont pas reconnus : il faudrait une liste de mots par langue, qu'une personne de la langue
  n'a pas relue. Révision du 12/11/2026 (UX-MAJ-06) : un numéro de quatre chiffres au plus, seul à côté d'un mot,
  est dit comme un nombre du titre (« Lot 1234 », « Facture n° 12 » : « Facture 12 ») ; un sigle collé à un nombre
  par un trait d'union reste un code, retiré avec lui (`Certificat ISO-9001.pdf` : « Certificat ») ; une suite de
  nombres qui n'est pas une date (« Salle 12 14 ») est retirée entière.

## Alternatives écartées

- **Dire le nom nettoyé sans le type** : « Suivi CP » ne dit pas que c'est un tableur, ce que la salle veut savoir.
- **Un champ « titre parlé » par document** : une saisie de plus pour ce que le nom et le support disent déjà.
- **Demander la phrase à l'IA** : une présentation se joue sans réseau ni clé.
- **Le type dans le cœur** : c'est une règle de lecture à voix haute, pas une règle du processus.
