<!-- 090-contre-visite-et-poste-sans-console.md -->
<!-- The corrections of the second visit of the external auditor, in the core and outside it, and the desktop mode without a console window: an icon near the clock opens and quits it -->

# 090 - La contre-visite et le mode Poste sans console

Statut : acceptée (date 02/11/2026). Prolonge les ADR 029 (sauvegardes et restauration), 044 (hôte du mode Poste), 071
(second lancement), 088 (paquet du Store) et 089 (corrections de l'audit externe). Demandes du porteur du projet du
02/11/2026 : la contre-visite de l'auditeur externe, qui avait cette fois les sources du cœur, et « le masquage de la
console », jugée inquiétante à côté de la page. Chaque constat a été vérifié dans les sources ; le verdict de chacun est
dans `Travail\lot-3.0.2-plan.md` (hors du dépôt).

## Contexte

La contre-visite valide toutes les corrections de l'ADR 089. Elle relève, dans le cœur, des règles appliquées à un
endroit et oubliées à un autre :

- une flèche refaite à partir d'une autre (insertion d'une étape, collage avec ancre, regroupement, couture) reprend son
  libellé sans ses traductions, que la dissolution reprend ;
- la dissolution vers plusieurs entrées reporte la part de la flèche sur chacune (une décision à plus de 100 %), ce que
  le collage avec ancre évite ;
- la suppression d'une carte ne vérifie pas, comme la dissolution, qu'aucune remontée n'y est ouverte : la remontée
  devient invisible, puis la purge l'efface ;
- la purge efface l'usage d'un document entre deux étapes vivantes quand son irritant, déplacé par une remontée, part
  avec une autre étape ;
- un usage de document dont les deux étapes sont sur deux cartes (après un regroupement) s'affiche, mais sa
  modification répond « introuvable » ;
- une proposition de l'assistant écrase la traduction manuelle de la carte racine, et l'aperçu accepte un type sans
  contenu sur une étape détaillée que l'application refuse ensuite ;
- l'import accepte l'usage d'une entrée ou de sa propre sortie, que l'application ne produit jamais. Il accepte aussi une
  carte sans couloir : c'est un état que l'application écrit (le détail vide « Valider la demande » de l'exemple n'a
  pas de couloir), et aucun code ne suppose un premier couloir ; ce point n'est pas retenu.

La console du mode Poste (F-69) était gardée pour la première version du Store, fidèle au ZIP : elle disait l'adresse,
la façon de quitter, les refus du démarrage, et posait la question de la restauration (ADR 029).

## Décision

1. **Une règle pour les flèches refaites.** `TranslationRules.Carry` : une flèche faite à partir d'une autre
   dont elle porte le libellé en reprend les traductions. Insertion, ancre, regroupement, couture et dissolution
   l'appellent tous.
2. **La part d'une sortie n'est reportée qu'une fois.** Vers plusieurs entrées du détail, la dissolution garde le
   libellé sur chaque flèche et laisse la part à saisir.
3. **Une remontée ouverte retient sa carte.** Supprimer une carte qui porte une remontée ouverte, ou une carte au-dessus,
   est refusé (`REMONTEE_EN_COURS_DESSOUS`) ; un lot qui a supprimé une telle carte, écrit avant, n'est pas purgé.
4. **La purge retire l'irritant, pas l'usage.** Un usage gardé perd l'irritant purgé, comme au retrait d'un irritant ;
   la purge écrit ces usages avant ses suppressions (`PurgePlan.Detached`).
5. **Un usage déjà coché reste modifiable** quand ses étapes sont sur deux cartes ; un nouvel usage reste dans sa carte.
6. **Une proposition garde ce que l'utilisateur a saisi** (la traduction manuelle de la carte racine) et l'aperçu refuse
   un type sans contenu sur une étape détaillée, avec le code de l'étape (`ETAPE_NON_DETAILLABLE`).
7. **L'import contrôle ce que l'application garantit** : un usage porte sur la sortie d'une autre étape.
8. **Le dossier des clés du mode Serveur** reste à côté du dossier des journaux écrit avec un séparateur final.
9. **Le mode Poste sans console.** Le manifeste demande à Windows de ne pas créer de console
   (`consoleAllocationPolicy` `detached`, Windows 11 24H2 et suivants) ; avant, la console créée pour le seul processus
   est libérée au démarrage. Lancée depuis un terminal, l'application garde sa console, comme les commandes. Sans
   console :
   - une icône près de l'horloge, au nom du produit : son menu ouvre l'application (un jeton neuf, comme un second
     lancement) et la quitte (comme la commande de l'interface) ;
   - un refus du démarrage, des réglages invalides ou une panne s'affichent dans une boîte de Windows ;
   - la question de la restauration ouvre une console pour elle seule, fermée une fois la réponse donnée ;
   - les avertissements du démarrage (dossier lisible par d'autres comptes, sauvegarde restaurée) s'affichent dans une
     boîte une fois l'application lancée ;
   - la fin de la session de Windows arrête l'application proprement (fichier de données fermé).
   L'exécutable porte l'icône du produit, faite des logos du Store (`build/store/icon.mjs`).

## Conséquences

- Le cœur change : paquet 3.0.2 (`licence.mjs pack`), archive d'une nouvelle marque.
- Le constat 3.7 de la contre-visite (convergence des boucles qui rebouclent à 99,9 % et plus) est gardé : F-79.
- Sous Windows 10 et Windows 11 avant 24H2, une console apparaît un instant au lancement (F-80).
- La question de la restauration, rare, n'a été essayée qu'en test (fenêtre simulée) : F-80.
- Les guides (`exploitation-poste.md`), le README du ZIP et la fiche du Store disent l'icône au lieu de la console.
