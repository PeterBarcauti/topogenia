<!-- 048-licence-et-coeur-ferme.md -->
<!-- Licence Apache-2.0 du depot et coeur du domaine livre en binaire sous licence proprietaire -->

# 048 - Licence Apache-2.0 et cœur fermé

Statut : acceptée (date 24/10/2026)

## Contexte

Le porteur du projet a tranché Q3 le 24/10/2026 : licence Apache-2.0. Il ajoute que tout le code n'est pas donné :
une partie stratégique, au cœur de l'application et sans laquelle elle ne fonctionne pas, doit être livrée en binaire
(une DLL obscurcie par exemple), pour qu'il garde la main sur elle. Le choix de la stratégie est laissé à la
construction.

Contraintes relevées :

- le client web (TypeScript) est lu par le navigateur : aucune partie de l'interface ne peut être fermée ;
- l'obscurcissement d'un assemblage .NET ne renomme que ce qui n'est pas public ; les corps de méthode restent
  décompilables (ILSpy, dnSpy). Il retarde la lecture, il ne l'empêche pas. Le droit français autorise en outre la
  décompilation pour l'interopérabilité (CPI, L122-6-1 IV), quoi que dise une licence ;
- la compilation en code natif (Native AOT) protégerait mieux, mais elle ne s'applique qu'à un exécutable entier ;
  EF Core et l'hôte ASP.NET Core ne la prennent pas entièrement en charge ;
- une licence Apache-2.0 accordée sur une partie du code ne se retire pas : ce qui est publié reste libre.

Pratique du marché (modèle dit « open core ») : un dépôt public sous licence permissive et un composant propriétaire
dans un dépôt privé, livré en paquet binaire avec ses propres conditions (Sidekiq / Sidekiq Pro, GitLab CE / EE,
Duende IdentityServer). La protection tient d'abord au droit (licence propriétaire, source jamais publiée), puis à la
difficulté de réécrire le composant ; l'obscurcissement n'est qu'un frein.

## Options

| Option | Partie fermée | Contrainte pour qui veut s'en passer | Coût |
|---|---|---|---|
| (a) | Le format `.pmap` et son import (phase 5) | Faible : l'application marche sans, à la main | Faible |
| (b) | Le domaine entier, `Topogenia.Domain`, et ses tests | Totale : aucune commande, aucune lecture, aucune persistance sans lui | Faible : c'est déjà un assemblage séparé |
| (c) | Une partie du domaine (placement, sous-cartes, remontée) | Moyenne | Élevé : découpe d'un assemblage cohérent |
| (d) | Une clé de licence vérifiée par l'application | Contournable sans (b) | Moyen, et une contrainte de plus pour l'utilisateur |

## Décision

**(b) : le cœur fermé est `Topogenia.Domain`.**

- Il porte toutes les règles métier (carte, couloirs, étapes, flèches, sous-cartes, remontée, RACI, fiches, placement,
  corbeille, traductions) ; l'application, la persistance et l'hôte n'en sont que les adaptateurs. Sans lui, rien ne
  compile ni ne tourne : il est « au cœur et contraignant ».
- Il ne dépend de rien (règles d'architecture, phase 1) et n'a aucun `InternalsVisibleTo` : il se sort sans découpe.
- **Ses tests restent privés avec lui** (1302 tests du domaine) : ils sont la spécification exécutable des règles.
  Sans eux, réécrire un domaine équivalent demanderait de retrouver chaque règle à la main ; c'est la vraie barrière,
  plus que l'obscurcissement.
- La version 3.0 (IA intégrée, `TOPOGENIA_3.0_SPECIFICATION_IA.md`) rejoindra le cœur fermé.

**Licences :**

- dépôt public (`topogenia`) : Apache-2.0, fichier `LICENSE` à la racine, `NOTICE` avec le titulaire des droits ;
- cœur (`Topogenia.Domain` et `Topogenia.Domain.Tests`) : licence propriétaire, `LICENSE.md` dans chacun des deux
  dossiers ; tous droits réservés, usage seulement sous une licence écrite accordée par le titulaire. C'est cette
  licence écrite qui donne la main au titulaire : il peut accorder l'usage gratuitement, pour une durée, et le
  renouveler ou non ;
- pas d'en-tête de licence dans les fichiers (spec 0.2 : ni auteur ni date dans les en-têtes) : le dossier et ses
  fichiers `LICENSE` suffisent.

**Livraison du cœur (phase 7) :**

- dépôt privé `topogenia-core` (domaine, ses tests, la chaîne d'empaquetage) ; paquet NuGet `Topogenia.Core`,
  assemblage signé par nom fort (clé du titulaire, gratuite) et obscurci par Obfuscar (MIT, outil de construction
  seulement, jamais livré) ;
- le dépôt public référence le paquet ; pendant la construction (phases 4 à 6), une propriété MSBuild garde la
  référence au projet source, pour ne pas ralentir le travail ;
- la constante du nom du produit (`ProductIdentity.cs`, lue par `Directory.Build.props`) sort du domaine vers un fichier
  du dépôt public, sinon le dépôt public ne compile plus.

**Écartées :** (a) ne contraint rien ; (c) découpe un assemblage cohérent pour une protection moindre que (b) ;
(d) reste possible plus tard, au-dessus de (b), si le titulaire veut une échéance technique en plus de l'échéance
juridique (elle ne vaut que si le cœur est fermé, sinon on l'enlève du code).

## Conséquences

- Le dépôt public montre l'architecture, la persistance sur deux moteurs, l'hôte, la sécurité et l'interface : il reste
  une vitrine ; il ne compile qu'avec le paquet du cœur.
- Une contribution externe au cœur est impossible ; `CONTRIBUTING.md` le dira.
- L'analyse CodeQL gratuite ne couvre que du code publié sous licence libre : le cœur est vérifié par les analyseurs
  .NET de la construction (avertissements en erreurs) et par ses tests (ADR 049).
- La règle d'architecture `No_licence_file_and_no_licence_author_or_date_header` devient : `LICENSE` Apache-2.0 à la
  racine, `LICENSE.md` propriétaire dans les deux dossiers du cœur, toujours aucun en-tête de licence, d'auteur ni de
  date dans les fichiers.
- Plan : tâche P7-T04b (séparation du cœur, paquet, obscurcissement, référence conditionnelle).

## Vérification

`tests/Topogenia.Architecture.Tests/ConfigurationRulesTests` (fichiers de licence et absence d'en-têtes) ; en phase 7,
la construction du dépôt public avec le seul paquet `Topogenia.Core`, sans le dossier source du domaine.

## Révision (25/10/2026) : porte P4, round 1

- Le nom du titulaire des droits figure dans `NOTICE`, dans les deux `LICENSE.md` et dans la règle d'architecture qui
  les vérifie : c'est l'objet même d'une licence. Seul écart au prompt (section 4, point 5 : aucun nom de personne dans le
  dépôt), demandé par la décision du porteur sur Q3.
- La règle des en-têtes de licence reconnaît aussi `Copyright <année>` et `©`. Les fichiers qu'elle lit sont précisés
  par la révision du round 2 ci-dessous (la liste écrite ici au round 1 était plus large que ce que la règle lisait).
- Une fois le domaine sorti du dépôt public (P7-T04b), les règles d'architecture qui lisent ses sources passeraient à
  vide (un dossier absent est sauté) : P7-T04b les fait tourner dans le dépôt privé et, dans le dépôt public, sur
  l'assemblage du paquet.

## Révision (25/10/2026, round 2)

La règle des en-têtes de licence
(`ConfigurationRulesTests.The_repository_is_under_apache_the_core_under_its_own_licence_and_no_file_has_a_licence_author_or_date_header`)
lit les cinq premières lignes de ces fichiers, et d'eux seuls :

- dossiers lus en profondeur : `src`, `tests`, `web/src`, `web/scripts`, `web/e2e`, `build`, `.github`, `.config`,
  `samples`, `docs` (hors sorties de construction et caches d'outils : `bin`, `obj`, `node_modules`, `TestResults`...) ;
- fichiers posés directement à la racine du dépôt et à la racine de `web/` (sans descendre dans leurs sous-dossiers) ;
- extensions lues, aux deux endroits : `.cs`, `.ts`, `.tsx`, `.css`, `.mjs`, `.html`, `.sql`, `.ps1`, `.cmd`, `.bat`,
  `.yml`, `.yaml`, `.csproj`, `.props`, `.targets`, `.md`, `.json`, `.sln`, et les fichiers `.editorconfig`,
  `.gitattributes`, `.gitignore` ;
- sont exclus les fichiers de licence eux-mêmes, qui nomment le titulaire : `LICENSE`, `LICENSE.md`, `NOTICE`.

Ne sont pas lus : les fichiers d'une autre extension (`.js`, `.tsbuildinfo`, images et autres formats binaires...),
les sous-dossiers de la racine et de `web/` absents de la liste (`web/node_modules`, `web/.vitest`...), et les dossiers
`bin`, `obj`, `node_modules`, `test-results`, `playwright-report`, `TestResults`, `.git`, `.vs` à toute profondeur.

La règle d'architecture citée plus haut sous l'ancien nom `No_licence_file_and_no_licence_author_or_date_header` porte
aujourd'hui le nom donné ci-dessus.

## Révision (29/10/2026) : P7-T04b

Livraison du cœur faite selon l'ADR 074 : `ProductIdentity.cs` est dans `src/Topogenia.Application` ; la bascule
`UseCorePackage` suit la présence des sources du domaine ; la chaîne `build/core/Topogenia.Core.csproj` signe par nom fort,
obscurcit et emballe ; `build/split.mjs` écrit le dépôt public et prouve qu'il compile et passe ses tests avec le seul
paquet. Écart (AR-147) : pas de dépôt `topogenia-core` réduit au domaine, la copie de travail en tient lieu. Les règles
d'architecture qui lisent les sources du domaine sont sautées dans le dépôt public, avec leur raison, et tournent dans la
copie de travail ; elles ne tournent pas sur l'assemblage du paquet, dont l'obscurcissement efface les noms qui
distinguent le code généré.
