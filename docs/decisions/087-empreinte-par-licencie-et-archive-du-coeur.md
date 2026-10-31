<!-- 087-empreinte-par-licencie-et-archive-du-coeur.md -->
<!-- Each package of the closed core carries the licence it was delivered under and a mark only the holder can make; each package is archived with its readable assembly, its map of names and its sources -->

# 087 - Empreinte par licencié et archive du cœur

Statut : acceptée (date 31/10/2026). Prolonge les ADR 048 (licence et cœur fermé) et 074 (paquet, chaîne et dépôt
public). Décision du porteur du projet du 31/10/2026, après une question sur le « tatouage » du code : « oui fais
l'archivage et l'empreinte par licencié ».

## Contexte

Le cœur fermé est livré en paquet NuGet, signé par le nom fort du titulaire et obscurci. La signature prouve d'où vient
un assemblage ; la carte des noms de l'obscurcissement (`Mapping.txt`), que seul l'auteur peut produire, relie
l'assemblage obscurci à ses sources. Mais la chaîne réécrivait cette carte à chaque construction, sans en garder trace,
et rien ne disait à qui un paquet avait été remis. Un tatouage caché dans le code du domaine (un comportement secret
déclenché par une entrée choisie) aurait mis dans le code qu'on veut auditable un comportement que personne ne voit,
pour une preuve juridique plus faible qu'un dépôt daté.

## Décision

1. **Empreinte** : chaque paquet du cœur porte, dans les attributs de son assemblage (`AssemblyMetadata`), le numéro de
   la licence sous laquelle il est remis (`Licence`) et une empreinte (`LicenceMark`) : HMAC-SHA256 d'un secret du
   titulaire sur le paquet, la licence, le licencié et la version, les 128 premiers bits en hexadécimal. Nul ne fabrique
   une empreinte sans le secret ; modifier l'attribut casse la signature par nom fort. Le paquet du titulaire (sa
   distribution, le ZIP, le dépôt public) porte la licence `HOLDER`.
2. **Point d'entrée unique** : `node build/core/licence.mjs` (`secret`, `pack`, `verify`). `pack` construit le cœur par
   sa chaîne, vérifie l'empreinte dans l'assemblage obscurci, puis archive ; `verify` lit l'empreinte d'un assemblage ou
   d'un paquet trouvé, l'authentifie par le secret et nomme son licencié. La chaîne refuse un `dotnet pack` sans
   empreinte.
3. **Archive**, hors du dépôt, chez le titulaire : pour chaque paquet, le paquet et son SHA-256, l'assemblage lisible et
   l'obscurci, la carte des noms, une copie des sources du domaine, un manifeste (licence, licencié, version, date,
   empreinte, empreintes SHA-256) ; un registre des licences, qui refuse un même numéro pour deux licenciés. La clé, le
   secret et l'archive sont refusés dans le dépôt.
4. Rien de cela n'entre dans le dépôt public : `build/core` reste chez le titulaire (ADR 074).

## Conséquences

- Le titulaire garde trois choses, à sauvegarder ensemble : la clé du nom fort, le secret des empreintes, l'archive.
  Perdre le secret empêche d'authentifier les empreintes déjà remises.
- Un paquet trouvé dit à quel licencié il a été remis ; un paquet sans empreinte authentique ne vient pas du titulaire.
- L'archive d'une version, avec ses sources et sa carte des noms, est ce que le titulaire dépose pour dater son œuvre
  (enveloppe e-Soleau ou dépôt APP).
