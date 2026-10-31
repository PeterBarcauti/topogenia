<!-- consignes-agents-p3-fin.md -->
<!-- Instructions shared by the three agents that finish phase 3: snapshot read, P3-T07 part A, P3-T07 part B -->

# Consignes des agents de fin de phase 3

En plus de `docs/consignes-agents-persistance.md` (à appliquer entièrement) :

- État de départ : 2236 tests verts (architecture 490, domaine 1299, persistance 447), 0 avertissement. Les
  tâches P3-T01 à P3-T06 sont faites : lis `docs/modele-donnees.md`, les ADR 027 à 032 et le journal
  (`docs/journal.md`, section Phase 3) avant de coder.
- Trois agents travaillent en même temps. Chacun ne modifie que les fichiers de sa mission ; un fichier hors de
  ta liste se signale dans ton compte rendu, il ne se modifie pas. Si la compilation casse à cause d'un fichier
  qui n'est pas à toi, attends et relance.
- Pour chaque ligne de couverture : soit un test vert sur les deux moteurs la prouve (ou sur le seul moteur
  concerné, avec la raison), soit la ligne est reclassée. Reclasser veut dire : la part persistance est prouvée,
  et le reste est rattaché au niveau et à la phase du plan (`docs/plan.md`, en citant la tâche, par exemple
  P4-T05). Une ligne n'est jamais reclassée pour éviter un test faisable en phase 3.
- Une ligne déjà « couvert (domaine) » de niveau Persistance demande la preuve de la part persistance : le
  garde-fou de la base, ou l'aller-retour charger, opérer, enregistrer, recharger, sur les deux moteurs.
- Tests portés de la 1.x : le nom 2.0 du test et la correspondance vont dans `docs/portage-tests.md` (section de
  ta mission, sans toucher aux autres sections).
- Couverture : écris `docs/couverture-p3/<ta-mission>.md` au format `| ID | État | Test 2.0 | Remarque |`, avec
  les noms complets des tests (`Topogenia.Persistence.Tests.<Dossier>.<Classe>.<Méthode>`). Pour une ligne
  reclassée, ajoute en remarque `Niveaux : ... ; Phases : ...`.
- Vérifie par mutation au moins un test par exigence nouvelle (casse le code, constate le rouge, remets le code).
- Compte rendu final en français, verdict d'abord : chiffres des tests, mutations, décisions (ADR), texte à
  intégrer dans les documents partagés, lignes de couverture, ce qui reste, preuve qu'aucune base
  `Topogenia_*` ni aucun fichier de test ne reste.
