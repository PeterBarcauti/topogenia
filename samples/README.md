<!-- README.md -->
<!-- Fictional sample projects shipped with the source: .topogenia packages and .pmap files -->

# Exemples

Projets **fictifs** d'exemple (spec 3.1) : paquets `.topogenia` et fichiers `.pmap`. Ils servent aux tests d'échange et
au projet d'exemple proposé au premier lancement en mode Poste. Aucune donnée réelle.

| Fichier | Contenu |
|---|---|
| `conges.topogenia` | Projet fictif « Demande de congés » (`CONGES`), exporté sans les personnes (option par défaut, AR-34) : carte racine à cinq couloirs, décision à deux sorties, macro-étape et son détail (losanges ET et OU inclusif, attente, automatisation en cours), sous-carte vide, remontée ouverte avec son reliquat, fiches (brouillon, validée deux fois, à préciser), documents et usages, irritants et leur historique, rôles RACI, traductions anglaises de tous les textes traduisibles (noms du projet, des cartes et des couloirs, étapes, flèches). Format : `docs/format-topogenia.md` |
| `leave.topogenia` | Le même projet écrit en anglais, « Leave request » (`LEAVE`), fiches, documents et irritants compris, avec ses traductions françaises : l'exemple proposé quand l'interface n'est pas en français (ADR 088). Mêmes textes que les traductions de `conges` (`SampleProjectEnglish`) |
| `minimal.pmap` | Demande d'achat : deux couloirs, quatre étapes, placement automatique (exemple de `docs/format-pmap.md`) |
| `cloture-mensuelle.pmap` | Clôture mensuelle des ventes (`VTE`) : quatre couloirs, décision, sous-carte, RACI, traductions du projet, d'un couloir, d'une étape et des flèches, deux fiches (déroulé, sortie et son usage par l'étape suivante, irritant), incertitude. Format : `docs/format-pmap.md` (1.2) |

`conges.topogenia` et `leave.topogenia` sont les exports du projet de `tests/Topogenia.Interchange.Tests/Support/SampleProject.cs`
(écrit en français, ou en anglais) ; le test `SamplePackageTests` vérifie qu'ils sont à jour. Après un changement du format, le réécrire en lançant ce test avec la
variable d'environnement `SAMPLES_WRITE=1`.

Les deux `.pmap` sont lus, ouverts et recopiés par `tests/Topogenia.Interchange.Tests/Pmap` et suivent le schéma
`schemas/pmap-1.2.schema.json` (`PmapDocumentationTests`).
