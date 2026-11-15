<!-- 047-matrice-des-droits-et-fiche-du-reliquat.md -->
<!-- P4-T08 : matrice des droits par route et par role, portage des tests 1.x, fiche du reliquat dans la vue de la carte, auteur inconnu des messages -->

# 047 - Matrice des droits, portage des tests 1.x, fiche du reliquat

Statut : acceptée (date 24/10/2026)

## Contexte

P4-T08 porte les tests d'intégration API 1.x sur la matrice SQLite x SQL Server et demande un test de sécurité par point
d'entrée et par rôle (A-4-07). Le portage a révélé deux défauts du produit :

- la fiche du reliquat d'une remontée n'était lisible par aucune route : le cadre (`FrameView`) n'en donnait que
  l'identifiant, et `GET /api/steps/{id}/sheet` refuse une étape supprimée (A-2.10-44). La règle de visibilité du
  reliquat (`SheetVisibility.ForRemainder`, faiblesse 1.x 5.6-1) n'était appelée par personne, alors que la 1.x montrait
  cette fiche dans la vue de la remontée (`RemonteeVue`) ;
- un refus dont le message nomme un auteur (`OPERATION_DEPASSEE`) sans en connaître (après une purge) donnait une
  phrase sans sujet.

## Décision

- **Matrice des droits** (`Api/RightsMatrixTests`) : une table des 58 routes de l'API (la santé et Quitter sont prouvées
  ailleurs), chacune avec une requête valide et son droit déclaré ; pour chaque route et chaque base, un projet neuf, puis
  anonyme 401, non-membre 404 (403 sur une route d'administrateur), rôle insuffisant 403 (un contributeur qui ne tient
  aucun couloir n'a jamais le droit de remplir), puis le plus petit rôle autorisé et l'administrateur non membre
  acceptés (toute réponse sauf 401, 403, 404) ; annuler et rétablir reçoivent d'abord une opération dans la pile du rôle
  (pile vide : 404, ADR 042). La table est comparée à celle des routes de l'hôte : une route nouvelle sans ligne fait
  échouer le test.
- **Portage** : chaque test 1.x de cible `Host.Tests` (190 lignes de `portage-tests.md`) renvoie à un test 2.0 qui le
  prouve par HTTP sur les deux bases, ou à la phase 5 (`.pmap`, duplication), ou au niveau qui le prouve quand aucune
  route ne l'atteint ; les renvois sont dans `docs/couverture-p4/portage-*.md`. Les nouveaux tests sont dans
  `tests/Topogenia.Host.Tests/Ported/`.
- **Fiche du reliquat** : la vue de la carte porte `dissolution_sheet` à côté de `dissolution` (nulle sans cadre ouvert) :
  état, contenu, brouillon ou non, dernière version validée, demande de précision, telle que le lecteur a le droit de la
  voir (`SheetVisibility.ForRemainder` : l'animateur et l'administrateur voient le brouillon, les autres la dernière
  version validée ou une fiche vide). Personne ne la remplit. La route des fiches reste 404 sur l'étape remontée.
- **Auteur inconnu** : un message qui nomme un auteur (`{by}`) dit « quelqu'un » / « someone » quand le refus n'en porte
  pas, comme `CONFLIT_VERSION` le faisait déjà.

## Conséquences

- Le panneau de remontée de l'interface (phase 6) lit la fiche du reliquat dans la vue de la carte, sans autre appel.
- Ajouter une route oblige à l'ajouter à la matrice des droits.

## Vérification

`Api/RightsMatrixTests` (117 cas), `Ported/*` (portage, voir les quatre documents `docs/couverture-p4/portage-*.md`),
`Ported/DissolutionPortedTests.The_sheet_of_the_remainder_is_seen_as_a_draft_by_the_facilitator_and_as_validated_only_by_a_reader`,
`Errors/ErrorResponsesTests.A_message_that_names_an_author_says_someone_when_the_refusal_knows_none`,
`Api/ContractCodesTests` (codes du contrat sans test HTTP jusque-là).
