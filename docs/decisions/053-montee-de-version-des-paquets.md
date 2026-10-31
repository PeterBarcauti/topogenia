<!-- 053-montee-de-version-des-paquets.md -->
<!-- Montée de version des paquets : définition de chaque format, chaîne de convertisseurs, contrôle de leur sortie, chaîne vide en 2.0 -->

# 053 - Montée de version des paquets `.topogenia`

Statut : acceptée (date 25/10/2026)

## Contexte

La spec 7.3 demande qu'« un paquet d'une version de format antérieure [soit] converti par une chaîne de convertisseurs
testés » et qu'un paquet plus récent soit refusé avec un message clair (fait en P5-T02, `PAQUET_TROP_RECENT`). Le
format 2.0 est le premier du paquet `.topogenia` (ADR 051) : aucun paquet plus ancien n'existe, et la 1.x n'avait que le
`.pmap`, dont la lecture des fichiers 1.0 est l'affaire de P5-T04. Il faut pourtant que le mécanisme existe et soit prouvé,
pour que la première évolution du format n'ait qu'un convertisseur à écrire. Trois questions :

1. Contre quel schéma vérifier un paquet ancien, avant et après sa conversion.
2. Ce que les garde-fous de la spec 5.4 (entrées, tailles, empreintes) deviennent pour un format dont les entrées diffèrent.
3. Comment prouver une chaîne vide en production.

## Décision

### 1. Une définition par version de format

`PackageDefinition` décrit une version : son numéro, ses entrées dans l'ordre de l'archive (`members.json`, les personnes,
restant la seule facultative) et son schéma publié (`schemas/<format>-<version>.schema.json`, embarqué). La version
courante est `PackageDefinition.Current` (`PackageFormat` et le schéma 2.0). Un format suivant **garde le schéma de chaque
format antérieur** dans `schemas/` : un paquet ancien est vérifié contre le schéma de son propre format, jamais contre un
schéma qui n'était pas le sien.

### 2. La chaîne

- `IPackageConverter` : un maillon, d'un format (`From`, sa définition) vers le suivant (`To`). Il réécrit le contenu et
  les champs du manifeste que le format suivant change ; il peut ajouter, retirer ou renommer une entrée.
- `PackageChain` : les convertisseurs de l'application, contrôlés à la construction (une erreur de programmation, pas
  une donnée reçue) : chaque format de départ une seule fois, antérieur au format courant, et chaque maillon mène au
  format courant sans boucle. `PackageChain.Application` est **vide** : la 2.0 est le premier format. Le format suivant
  ajoutera le convertisseur depuis la 2.0, avec son schéma et ses tests sur des paquets 2.0 réels (`samples/`).
- `PackageImporter(limits, chain)` ; le constructeur sans argument prend les limites de la spec et la chaîne de
  l'application. Rien d'autre ne change pour l'application ni pour l'hôte.

### 3. L'ordre des garde-fous pour un paquet ancien

1. Archive, nombre d'entrées, chemins : un nom d'entrée est connu s'il appartient au format courant **ou** à un format
   de la chaîne (un format qui renomme une entrée se lit encore sous l'ancien nom).
2. Manifeste (1 Mo), `format`, puis `formatVersion` : la version courante n'a aucun maillon ; une version antérieure que la
   chaîne mène au format courant a ses maillons ; toute autre version antérieure est refusée `PAQUET_INVALIDE`, raison
   `format` ; une version plus récente reste `PAQUET_TROP_RECENT`.
3. Le manifeste suit le schéma **de son format** ; les entrées présentes sont exactement celles qu'il annonce et
   qu'admet son format (une entrée connue d'un autre format seulement est refusée, raison `entries`).
4. Tailles lues en flux et SHA-256 **des octets reçus**, contre le manifeste reçu ; chaque entrée suit le schéma de son
   format.
5. Pour chaque maillon : conversion ; le manifeste prend `formatVersion` et `schemaVersion` du format suivant ; ses
   entrées sont **annoncées à nouveau** (taille et SHA-256 de leur écriture canonique, dans l'ordre du format suivant) ;
   puis le manifeste et chaque entrée sont vérifiés contre le schéma du format suivant, comme un paquet écrit dans ce
   format. Une sortie non conforme est refusée `PAQUET_INVALIDE`, raison `schema` (une valeur admise par l'ancien format
   et plus par le nouveau est un refus légitime ; un défaut du convertisseur est aussi attrapé ici).
6. Puis la reconstruction d'essai (références, comptes) comme pour un paquet 2.0.

Un convertisseur ne reçoit donc qu'un contenu conforme au schéma de son format : il n'a pas à se défendre d'une entrée
mal formée, et ce qu'il écrit est prouvé, jamais cru.

### 4. La preuve d'une chaîne vide

Les tests (`PackageConversionTests`) écrivent deux formats fictifs, jamais livrés : 1.9 (le projet nommé `title`, pas de
`translations.json`) et 1.8 (1.9 avec `painpoints.json` nommé `irritants.json`), leurs schémas tirés du schéma 2.0 et
leurs convertisseurs. Ils prouvent : qu'un paquet 1.9 reconstruit exactement le projet de son paquet 2.0 (mêmes entrées
réécrites par l'export) ; qu'une chaîne de deux maillons fait de même depuis 1.8 ; le schéma de l'ancien format avant la
conversion ; le contrôle de la sortie d'un convertisseur fautif ; l'empreinte des octets reçus et les entrées de l'ancien
format ; le refus d'une entrée d'un autre format dans un paquet 2.0 ; le refus d'une version que nul maillon ne convertit ;
les chaînes refusées à la construction ; et la chaîne vide de l'application.

Options écartées : (a) écrire une ADR qui constate la chaîne vide sans code : la première évolution du format devrait
alors inventer le mécanisme et ses garde-fous sous contrainte ; (b) vérifier un paquet ancien contre le schéma courant
seulement, après conversion : le convertisseur devrait se défendre d'un contenu hostile non vérifié ; (c) livrer un
format 1.9 de démonstration : ce format n'a jamais existé, l'application accepterait des paquets que personne n'a écrits.

## Conséquences

- Une évolution du format 2.x ajoute : un fichier de schéma à côté des anciens, `PackageFormat.Version`, une définition
  pour le format quitté et son convertisseur dans `PackageChain.Application`, et les tests du convertisseur sur des
  paquets du format quitté.
- Les noms d'entrées admis avant la lecture du manifeste sont l'union des formats de la chaîne : la vérification exacte
  se fait contre le format du paquet, dès le manifeste lu.

## Vérification

`Topogenia.Interchange.Tests.Packages.PackageConversionTests` (9 tests) ; mutations tuées : contrôle de la sortie retiré,
schéma courant appliqué avant la conversion, entrée d'un autre format acceptée, entrées non annoncées à nouveau,
convertisseur depuis le format courant admis. Les tests existants du lecteur (`PackageReaderTests`) passent inchangés.
