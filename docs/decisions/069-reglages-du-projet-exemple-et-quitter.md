<!-- 069-reglages-du-projet-exemple-et-quitter.md -->
<!-- Classification et libellé du groupe de partage réglés dans l'interface, projet d'exemple et commande « Quitter » du mode Poste (porte P6 round 1, AR-131 à AR-135) -->

# 069 - Réglages du projet, projet d'exemple et « Quitter » du mode Poste

Statut : acceptée (date 29/10/2026)

## Contexte

Le round 1 de la porte de la phase 6 a relevé quatre manques de l'interface, que l'hôte couvrait déjà en partie :
aucun projet d'exemple au premier lancement du mode Poste (Fidélité B1, S-6.1-10) ; une classification ni affichée ni
réglable, si bien que tout projet créé dans l'interface s'imprimait « Interne » sur ses exports (Fidélité B2, S-4.1-07,
AR-127) ; un libellé du groupe de partage que seule l'API réglait (Fidélité M2, B-4-69, B-8.1-44) ; aucune commande
« Quitter » en mode Poste, dont la route `POST /api/desktop/quit` existait (Fidélité M3, Sécurité M-1, S-5.2-16).
Le profil ne disait pas à l'interface dans quel mode l'hôte tourne.

## Décision

1. **Le mode Poste connu de l'interface** (AR-131) : champ `desktop` de `ProfileView`, posé par l'hôte
   (`ProfileApi`) sur les trois réponses du profil, facultatif au contrat ; absent ou faux, l'interface se conduit
   comme en mode Serveur (rien de plus n'est montré). Écartés : une route `GET /api/desktop` qui répondrait 404 en mode
   Serveur (un refus pris pour un signal), la santé (supervision, une lecture de la base de plus), un essai de la route
   `quit`.
2. **Projet d'exemple** (AR-132) : route `POST /api/desktop/sample-project` (404 `INTROUVABLE` en mode Serveur, comme
   « Quitter »), qui importe le paquet `samples/conges.topogenia` lui-même, embarqué par le projet de l'hôte (un test
   le prouve égal octet pour octet au fichier de `samples/`), par la chaîne de tout import : droit de l'administrateur,
   contrôles du paquet, code libre (`PROJET_CODE_EXISTANT`, `PROJET_CODE_CORBEILLE`), audit `project.imported`, sans
   les personnes, l'utilisateur seul animateur. L'accueil montre la carte « Projet d'exemple » en mode Poste, à
   l'administrateur, tant qu'aucun projet vivant n'existe ; l'ignorer ne change rien. Après la création, la liste est
   relue, une notification le dit et le projet devient celui du bouton principal ; l'utilisateur reste sur l'accueil.
3. **Classification** (AR-133) : choisie à la création (« Interne » par défaut, envoyée explicitement) ; montrée sur la
   carte du projet à l'accueil et dans la barre de l'éditeur, juste après le code, en bouton qui ouvre les propriétés
   du projet ; changée par un animateur par `PUT /api/projects/{id}/classification` (modèle de `sharing-group-label` :
   `If-Match` sur la version du projet, non annulable, valeur exacte de la liste fermée, absente ou blanche refusée par
   `CHAMP_OBLIGATOIRE`). Le changement est tracé (`project.classification_changed`, niveaux avant et après), le même
   niveau n'écrit rien. `ProjectListItem` porte la classification (lue de l'enregistrement déjà chargé).
4. **Un seul endroit pour les réglages** (AR-134) : dialogue « Propriétés du projet » (classification et « Groupe de
   partage des documents », 100 caractères), ouvert par le bouton de la classification de la barre de l'éditeur et par
   l'entrée « Propriétés du projet… » du menu Fichier de la carte. Il relit le projet à l'ouverture (comme la
   suppression, ADR 058 point 2) et met l'en-tête à jour ; l'enregistrement n'envoie que ce qui a changé, chaque
   réglage par sa route avec la version lue, le second avec la version rendue par le premier ; un libellé vidé est
   retiré ; un refus reste dans le dialogue. Un membre qui n'est pas animateur voit les valeurs sans pouvoir les changer.
5. **« Quitter »** (AR-135) : en mode Poste seulement, le compte de l'en-tête (présent sur tous les écrans) ouvre un
   menu avec « Quitter Topogenia » ; une confirmation précède l'appel ; la page « Topogenia est arrêté. Cet onglet peut
   être fermé. » remplace alors toute l'application (plus aucune requête vers un hôte arrêté) ; un échec laisse
   l'application en marche et dit comment l'arrêter autrement. En mode Serveur, le compte reste un nom. **Précisé au
   round 2 (AR-143)** : avant l'arrêt, « Quitter » envoie ce qui attend encore dans les files d'écriture inscrites
   (`shared/state/pendingWrites.ts` : la fiche) ; une saisie qui n'a pas pu partir laisse l'application ouverte et le
   dit. Le menu s'ouvre sous le compte, leurs bords droits alignés.
6. **ADR 058 point 3 amendé** : la classification n'est plus envoyée vide à la création ; le libellé du groupe de
   partage l'est toujours, réglé ensuite dans les propriétés.

## Conséquences

- Deux routes nouvelles au contrat (`openapi.json` réécrit, client régénéré) et à la table des routes de
  `couverture.md` ; `ProfileView.desktop` facultatif, `ProjectListItem.classification` requis.
- L'audit du changement de classification va au-delà de la liste de la spec 5.5 : la classification gouverne le
  traitement des exports (spec 4.1, TISAX), un abaissement doit se retrouver. Une ligne nouvelle de la couverture le
  porte.
- Restes acceptés, inscrits au registre `a-peaufiner.md` : une fiche ouverte garde le libellé du groupe lu à son
  ouverture jusqu'à sa réouverture ; la carte de l'exemple et la classification de l'accueil empruntent les styles de
  `.project` et `.project-meta` (aucune couleur propre aux niveaux « Confidentiel » et « Strictement confidentiel »).
- Le parcours « Quitter » arrête l'hôte : il se joue sur un hôte à lui ou en dernier.
