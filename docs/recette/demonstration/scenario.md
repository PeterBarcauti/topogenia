<!-- scenario.md -->
<!-- Scénario pas à pas de la démonstration (P6-T11, S-P-19) : un projet fictif de notes de frais, du projet vide à la carte détaillée, partagée et exportée. -->

# Scénario de démonstration : les notes de frais

Un animateur décrit le parcours d'une note de frais dans une entreprise sans nom : la dépense engagée par un salarié,
la saisie, la validation par le manager, le contrôle par la comptabilité, le paiement et l'archivage. Toutes les
données sont fictives : le compte de l'animateur est `DEMO\camille`, les membres `DEMO\karim` et `DEMO\ines`.

Chaque pas donne le geste, ce que l'écran doit montrer, et la capture correspondante (dossier `captures/`, écran de
1366 × 768). Les fichiers produits par les pas 11, 13 et 14 sont dans `exports/`. Le scénario est joué tel quel par
`npm run demonstration` (voir `LISEZMOI.md`), qui vérifie chaque résultat attendu ; un écart arrête le jeu.

## 1. Premier lancement

- **Geste** : lancer l'application.
- **Attendu** : l'accueil, la personne connue par son compte Windows (`camille` en haut à droite), aucun projet ;
  « Décrire un processus » est grisé tant qu'aucun projet n'existe.
- **Capture** : `01-accueil-premier-lancement.png`.

## 2. Créer le projet

- **Geste** : « Nouveau projet » ; code court `ndf` (il s'écrit `NDF`) ; nom en français « Notes de frais », en anglais
  « Expense reports » ; une description ; « Ajouter une personne » deux fois : `DEMO\karim` contributeur, `DEMO\ines`
  lecteur et sponsor ; « Créer le projet ».
- **Attendu** : la carte du projet s'ouvre, vide, avec un premier couloir « Acteur 1 » et le message « Cette carte est
  vide. Double-cliquer dans un couloir pour ajouter une étape. » La personne qui crée le projet en est l'animateur.
- **Captures** : `02-nouveau-projet.png` (le formulaire rempli), `03-carte-vide.png`.

## 3. Les couloirs

- **Geste** : double-clic sur l'en-tête « Acteur 1 », taper « Salarié », Entrée ; le « + » sous le dernier couloir trois
  fois, pour « Manager », « Comptabilité », « Logiciel de frais » ; le menu « ⋯ » de chaque en-tête pour choisir son
  type : Personne ou rôle (Salarié, Manager), Service (Comptabilité), Système (Logiciel de frais).
- **Attendu** : quatre couloirs `NDF-C01` à `NDF-C04`, chacun avec l'icône de son type et sa couleur pastel ; le menu
  d'un couloir propose Renommer, Traduire…, les trois types et les huit couleurs, puis Supprimer.
- **Capture** : `04-menu-du-couloir.png`.

## 4. Les étapes

- **Geste** : pour chaque étape, choisir sa forme sur le ruban (Formes : Étape, Attente, Début, Fin ; Branchements :
  Décision, Parallèle), puis double-cliquer dans la case visée et taper son nom :

  | Code | Couloir | Colonne | Forme | Nom |
  |---|---|---|---|---|
  | NDF-E001 | Salarié | 1 | Début | Dépense engagée |
  | NDF-E002 | Salarié | 2 | Étape | Saisir la note de frais |
  | NDF-E003 | Manager | 3 | Étape | Valider la note |
  | NDF-E004 | Comptabilité | 4 | Décision | Note conforme ? |
  | NDF-E005 | Comptabilité | 5 | Parallèle | Payer et archiver |
  | NDF-E006 | Logiciel de frais | 6 | Étape | Préparer le virement |
  | NDF-E007 | Comptabilité | 6 | Étape | Archiver les justificatifs |
  | NDF-E008 | Logiciel de frais | 7 | Attente | Attendre la date de paiement |
  | NDF-E009 | Comptabilité | 8 | Parallèle | Paiement fait et note archivée |
  | NDF-E010 | Salarié | 9 | Fin | Note remboursée |

  Au-delà de la cinquième colonne, déplacer la vue en glissant le fond avec le bouton du milieu (ou le bouton droit).
- **Attendu** : chaque étape prend son code dans l'ordre de création ; tant qu'elle n'est reliée à rien, elle porte la
  pastille orange « ! » (« Cette étape n'est reliée à aucune autre »). Les losanges ont leur nom sous la forme.
- **Capture** : `05-etapes-a-relier.png` (les cinq premières étapes, toutes signalées).

## 5. Relier par la barre de commande

- **Geste** : un clic sur le fond, puis Ctrl+K ; taper une ligne par flèche, avec les codes courts :

  ```
  E1 > E2
  E2 > E3
  E3 > E4
  E4 > E5 : oui
  E4 > E2 : non
  E5 > E6
  E5 > E7
  E6 > E8
  E8 > E9
  E7 > E9
  E9 > E10
  ```

  puis Entrée (ou « Relier »).
- **Attendu** : « 11 flèches ajoutées » ; plus aucune pastille « ! » ; les flèches suivent les allées entre les
  colonnes ; « oui » et « non » sont écrits sur leurs flèches ; le retour « non » vers NDF-E002 passe sous les étapes.
- **Captures** : `06-barre-de-commande.png` (la barre remplie), `07-carte-reliee.png` (le début de la carte),
  `08-carte-reliee-suite.png` (la fin de la carte, après avoir déplacé la vue). Le bouton « ⇤ » en bas à droite
  ramène la vue au début.

## 6. Remplir une fiche

- **Geste** : un clic sur NDF-E002 « Saisir la note de frais », puis Entrée : sa fiche s'ouvre, ancrée à droite (AR-151 : le clic
  choisit l'étape, Entrée ou « Ouvrir la fiche » de son menu ouvre la fiche). Remplir : finalité
  « Déclarer les dépenses engagées pour être remboursé. », outils « Logiciel de frais, téléphone », trois opérations
  (Entrée passe à la suivante) avec leur nature : « Photographier chaque justificatif » (Classer), « Saisir chaque
  dépense dans le logiciel » (Ressaisir), « Envoyer la note au manager » (Envoyer) ; fréquence Mensuelle, durée
  20 minutes ; dans Sorties, « Ajouter une sortie » : « Note de frais », Entrée.
- **Attendu** : « Enregistré » en haut de la fiche à chaque modification, sans bouton à presser ; la fiche passe
  « brouillon » et sa jauge (complétude 86 %) apparaît sous l'étape, avec les pictogrammes du document et de la durée.
- **Captures** : `09-fiche-formulaire.png`, `10-fiche-sorties.png`.

## 7. La fiche suivante, validée

- **Geste** : Échap ferme la fiche ; un clic sur NDF-E003 « Valider la note », puis Entrée. Dans Entrées, cocher « Note de frais »
  (proposée parce que l'étape précédente la produit) ; utilisable tel quel à 70 % ; irritant « Justificatifs illisibles
  ou manquants », temps perdu 4 heures par mois. Puis finalité, outils, deux opérations (« Contrôler les montants et les
  justificatifs » en Contrôler, « Valider la note dans le logiciel » en Envoyer), fréquence Hebdomadaire, durée 15 ;
  « Je valide ma fiche ».
- **Attendu** : « Fiche validée : elle est visible par l'équipe. » ; l'irritant reçoit son code `NDF-I001` et remonte à
  l'étape qui produit le document (pictogramme de flamme sur NDF-E002) ; la flèche E002 → E003 porte le document
  (« 1 document : Note de frais ») ; la jauge de NDF-E003 passe au vert.
- **Capture** : `11-fiche-validee.png`.

## 8. Le RACI

- **Geste** : bouton RACI du ruban (groupe Affichage) ; un clic sur NDF-E002 ouvre son panneau : A pour Manager, I pour
  Comptabilité ; Échap. De même : NDF-E003, C pour Comptabilité et I pour Salarié ; NDF-E006, A pour Comptabilité et I
  pour Salarié. Puis le bouton Matrice.
- **Attendu** : le couloir de l'étape « réalise » (R) ; les lettres données s'affichent en pastilles sous les étapes
  (sous NDF-E002, à gauche de l'axe, à l'écart du retour « non » qui arrive par le bas) ;
  une étape ou une décision sans approbateur porte « A ? » ; la matrice reprend toutes les étapes de la carte,
  sauf le début, la fin et les losanges parallèles, et se copie pour Excel ou PowerPoint. Le bouton RACI éteint le calque.
- **Captures** : `12-raci-panneau.png`, `13-raci-calque.png`, `14-raci-matrice.png`.

## 9. Détailler une étape

- **Geste** : clic droit sur NDF-E002 : son menu ; « Détailler cette étape ». Dans la carte détaillée, quatre étapes
  dans le couloir Salarié : « Photographier les justificatifs », « Saisir les lignes de dépense », « Joindre les photos
  aux lignes », « Envoyer la note au manager » (NDF-E011 à NDF-E014), reliées par Ctrl+K (`E11 > E12`, `E12 > E13`,
  `E13 > E14`). Revenir par le fil d'Ariane « Notes de frais ».
- **Attendu** : le fil d'Ariane « Notes de frais › Saisir la note de frais » ; la carte détaillée a son propre couloir,
  du nom de celui de l'étape ; de retour, NDF-E002 porte la pastille « détaillée », une pile décalée et le badge de sa
  fréquence (« Mensuelle »), à gauche de l'axe : le retour « non » arrive par la pointe basse en venant de la droite, sa
  pointe reste visible (AR-128). Un double-clic sur elle rouvre sa carte.
- **Captures** : `15-menu-de-l-etape.png`, `16-carte-detaillee.png`, `17-etape-detaillee.png`.

## 10. Dissocier un sous-processus (remonter le détail dans le flux)

- **Geste** : la fiche de NDF-E006 « Préparer le virement » déclare une sortie « Fichier de virement ». Détailler
  NDF-E006 en deux étapes (« Générer le fichier de virement », « Transmettre le fichier à la banque », NDF-E015 et
  NDF-E016, reliées). Revenu sur la carte, clic droit sur NDF-E006 : « Dissocier le sous-processus » (AR-154 ; avant :
  « Remonter le détail dans le flux »).
- **Attendu** : les deux étapes prennent la place de NDF-E006 dans le flux, entourées d'un cadre en pointillés
  « Reliquat de NDF-E006 · 1 » ; un clic sur cette étiquette ouvre le panneau du reliquat (ce que portait l'étape :
  ici le document produit) ; « Affecter à… » NDF-E015 : le document passe à cette étape (pictogramme « 1 »), le
  reliquat est vide et le cadre disparaît. Ctrl+Z annulerait l'affectation, puis la remontée.
- **Captures** : `18-remontee-cadre.png`, `19-remontee-reliquat.png`, `20-remontee-faite.png`.

## 11. Traduire par un classeur

- **Geste** : bouton Traductions de l'en-tête. Glossaire du projet : « note de frais = expense report »,
  « justificatif = receipt », « virement = bank transfer » (un terme par ligne), « Enregistrer le glossaire » ;
  « Télécharger le classeur » (textes « Sans traduction »). Compléter la colonne EN du classeur (Copilot, une autre IA
  ou à la main : ici, le jeu le fait comme un traducteur), puis « Choisir le classeur traduit… ».
- **Attendu** : le classeur compte un texte par couloir, étape, carte et libellé de flèche sans traduction ; au retour,
  « 22 traductions enregistrées. » et la carte relue.
- **Fichiers** : `exports/NDF_traductions_<jour>.xlsx` (téléchargé), `exports/NDF_traductions_<jour>_EN.xlsx` (complété).
- **Captures** : `21-traductions.png`, `22-traductions-rapportees.png`.

## 12. Une traduction à la main, puis l'interface en anglais

- **Geste** : clic droit sur NDF-E004, « Traduire… » : la traduction du classeur est là (« Report compliant? ») et
  peut être corrigée à la main ; Annuler. Puis EN dans l'en-tête ; FR pour revenir.
- **Attendu** : en anglais, l'interface et le contenu de la carte : couloirs, étapes, losanges, « yes » / « no », nom
  du projet « Expense reports », badge « Monthly ».
- **Captures** : `23-traduire-une-etape.png`, `24-carte-en-anglais.png`.

## 13. Exporter la carte

- **Geste** : menu Fichier ; « Exporter en image PNG », puis « Exporter en PDF (A3 paysage) », puis « Exporter en
  Excel ».
- **Attendu** : « Export en cours… » puis « Export terminé : le fichier est dans le dossier Téléchargements. » pour
  chacun ; les trois fichiers portent le code, le nom et le jour (`NDF_Notes-de-frais_<jour>`) ; l'image montre toute la
  carte, pas seulement l'écran, avec « Classification : Interne » dessous ; le PDF a le titre, la date, la
  classification et le pied ; le classeur a trois onglets (Étapes, Liens, Matrice RACI).
- **Fichiers** : `exports/NDF_Notes-de-frais_<jour>.png`, `.pdf`, `.xlsx`.
- **Capture** : `25-menu-fichier.png`.

## 14. Exporter le projet en paquet

- **Geste** : menu Fichier, « Exporter le projet (.topogenia)… » ; laisser « Sans les membres ni les auteurs » coché ;
  Exporter.
- **Attendu** : « Paquet téléchargé : NDF_Notes-de-frais_<jour>.topogenia. » Le paquet porte cartes, fiches, documents,
  irritants et traductions, sans aucun nom de personne.
- **Fichier** : `exports/NDF_Notes-de-frais_<jour>.topogenia`.
- **Capture** : `26-export-du-paquet.png`.

## 15. Importer le paquet comme un nouveau projet

- **Geste** : retour à l'accueil (logo) ; « Importer un paquet .topogenia… » ; choisir le paquet ; « Vérifier le
  paquet ».
- **Attendu** : rien n'est écrit à la vérification ; le code NDF est déjà pris : le dialogue demande un autre code.
  Taper `ndfb`, vérifier de nouveau : l'aperçu compte 2 cartes, 5 couloirs, 15 étapes, 15 liens, 2 fiches,
  2 documents, 1 irritant, 25 traductions ; « Le projet prendra le code NDFB. » « Importer » ouvre la carte du nouveau
  projet, aux codes réécrits (`NDFB-E001`…).
- **Capture** : `27-import-apercu.png`.

## 16. La corbeille

- **Geste** : accueil ; sur la ligne du projet NDFB, « Supprimer… » ; taper son code ; « Mettre à la corbeille ».
- **Attendu** : le bouton ne s'arme qu'avec le code exact ; le projet passe dans la Corbeille, restaurable pendant
  30 jours, avec le jour de son effacement définitif ; « Qui participe » montre l'équipe du projet NDF (sponsor marqué).
- **Captures** : `28-mise-a-la-corbeille.png`, `29-accueil-et-corbeille.png`.

## 17. Les membres

- **Geste** : sur la ligne du projet NDF, « Membres ».
- **Attendu** : les trois membres, leur rôle et la case Sponsor ; « Ajouter une personne », « Retirer »,
  « Enregistrer », « Ouvrir la carte ».
- **Capture** : `30-membres.png`.
