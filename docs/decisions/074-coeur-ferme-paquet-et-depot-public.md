<!-- 074-coeur-ferme-paquet-et-depot-public.md -->
<!-- The closed core delivered: the product name out of the domain, the switch to the package, the chain that signs and obfuscates it, and the public repository written and proved without the sources of the core (P7-T04b, AR-147) -->

# 074 - Cœur fermé : paquet, chaîne et dépôt public

Statut : acceptée (date 29/10/2026)

## Contexte

L'ADR 048 ferme le cœur (le domaine et ses tests) : paquet NuGet `Topogenia.Core`, assemblage signé par nom fort avec la
clé du titulaire et obscurci par Obfuscar ; le dépôt public référence le paquet, une propriété MSBuild garde la référence
au projet source pendant la construction ; la constante du nom du produit sort du domaine ; vérification : le dépôt
public compile avec le seul paquet. Le prompt interdit tout dépôt Git sans demande, tout envoi sur Internet et toute
installation (4.3, 4.4) : la séparation s'écrit et se prouve sur des dossiers locaux.

## Décision

1. **Le nom du produit** : `ProductIdentity.cs` passe dans `src/Topogenia.Application` (espace `Topogenia.Application`).
   `Directory.Build.props`, `build/sbom.mjs` et `web/scripts/build-mockup.mjs` le lisent là ; les règles D13 suivent
   (ADR 013). Les règles qui nommaient l'assemblage du domaine par ce type le nomment par `DomainException`.
2. **La bascule** (`Directory.Build.props`) : `UseCorePackage` vaut `false` là où `src/Topogenia.Domain` existe, `true`
   ailleurs ; l'application, l'échange et la persistance référencent alors le paquet `Topogenia.Core` (version centrale
   2.0.0) au lieu du projet, restauré depuis `CoreFeed` (`artifacts/core-feed` par défaut). Rien à régler à la main : la
   copie de travail compile ses sources, le dépôt public le paquet.
3. **La chaîne du cœur** tient dans `build/core/Topogenia.Core.csproj`, en une commande :
   `dotnet pack build/core/Topogenia.Core.csproj -c Release -p:CoreKeyFile=<clé>`. Elle construit le domaine en Release
   avec le nom fort, dans `artifacts/core/` (son `obj` et son `bin` habituels ne bougent pas) ; la clé est celle du
   titulaire (`sn.exe -k 2048`, outil du SDK .NET Framework livré avec Visual Studio), gardée hors du dépôt : la chaîne
   refuse une clé absente ou posée dans le dépôt. Obfuscar 2.2.50 (MIT, 15/12/2025, paquet NuGet de la chaîne, jamais
   livré ni copié dans le dépôt public) l'obscurcit selon `build/core/obfuscar.xml` : l'API publique reste (l'application,
   la persistance et l'échange s'en servent), les noms privés et internes sont renommés (2 883 à cette version), les
   chaînes restent telles qu'écrites (codes d'erreur et clés du contrat, que le dépôt public connaît), le nom fort est
   redonné. Le paquet porte l'assemblage, la documentation XML de sa seule API publique et `LICENSE.md` (acceptation
   exigée), sans symboles : `build/core/public-docs.mjs` ne garde de la documentation que les membres que
   l'obscurcissement a gardés (597 sur 750), les autres rendraient les noms qu'il a retirés ; le générateur OpenAPI ne lit
   que la documentation des références de projet, l'hôte lui donne celle du paquet dans le dépôt public, et le contrat
   y a les mêmes descriptions que dans la copie de travail ; la carte des noms (`Mapping.txt`, utile pour lire une pile d'appels) reste dans
   `artifacts/core/out`, chez le titulaire. La chaîne écrit l'empreinte SHA-256 à côté du paquet et retire du cache NuGet
   la copie de la même version, pour qu'une restauration prenne le paquet neuf.
4. **Le dépôt privé est la copie de travail** : elle garde tout, le domaine, ses tests, la chaîne et les règles
   d'architecture sur toutes les sources. `node build/split.mjs <destination>` écrit le dépôt public hors d'elle : ce que
   Git garderait (`.gitignore`), moins le domaine, ses tests, `build/core` et le script lui-même ; la solution sans les
   deux projets du cœur ; le paquet dans `artifacts/core-feed`. Il refuse une destination dans la copie, autour d'elle,
   ou un dossier qui n'est ni vide ni une copie publique écrite par lui ; un `.git` de la destination est gardé : la
   séparation suivante se lit comme un diff. **Écart à l'ADR 048**, qui écrivait un dépôt privé `topogenia-core` réduit au
   domaine : un titulaire seul travaillerait alors dans deux dépôts pour tout changement qui touche le domaine et ce qui
   s'en sert, avec un paquet à refaire entre les deux. La copie de travail en tient lieu ; son nom de dépôt Git, s'il en
   devient un, est au choix du titulaire.
5. **La preuve** (`split.mjs` sans `--no-proof`) : dans la copie publique, la restauration (les fichiers de
   verrouillage réécrits pour le paquet), la construction à 0 avertissement, l'assemblage du domaine à côté des tests
   identique, empreinte comprise, à celui du paquet, les paquets npm de l'interface (`npm ci --ignore-scripts`, lus par les
   règles des composants tiers), puis tous les tests de la solution.
6. **Les règles d'architecture du dépôt public** : celles qui lisent les sources du domaine, ou son assemblage compilé
   par les noms de son code généré (l'obscurcissement efface les noms qui distinguent les machines à états des
   itérateurs et le code des expressions régulières, que ArchUnitNET écarte sinon), sont sautées avec leur raison
   (`CoreSources.Require`, `Assert.SkipUnless`) ; celle qui couvre le domaine et l'application garde l'application ;
   celles qui lisent les projets admettent le paquet du cœur, et lui seul, sous la condition de la bascule.
   `The_domain_is_built_from_its_sources_or_comes_signed_from_its_package` empêche de sauter une règle sans le paquet
   signé : le domaine est non signé là où ses sources sont, signé ailleurs. Dans la copie de travail, rien n'est sauté.
   Deux des règles sautées (`Only_the_workspace_stamps_a_record_or_commits_a_change_set`,
   `Record_keys_are_built_only_by_the_records_of_the_model`) portent aussi sur l'application, la persistance et l'hôte :
   ces couches n'y sont vérifiées que dans la copie de travail (porte P7 round 1, Intégrité m1).
7. **Composants** : le paquet du cœur est un composant du produit, pas d'un tiers (`build/components.mjs` l'écarte de
   `THIRD-PARTY-NOTICES.md`) ; la SBOM du dépôt public le porte, lue dans le `deps.json` ; `build/package.mjs` prend la
   licence du cœur dans son paquet. Obfuscar, outil de la seule chaîne privée, n'entre pas
   dans `THIRD-PARTY-NOTICES.md` (ni livré ni utilisé par le dépôt public) ; sa licence et sa date sont relevées ici.
8. **Ce qui est livré porte le paquet** (porte P7-T06, round 1 : les ZIP portaient le domaine construit depuis ses
   sources, non signé et non obscurci) : `build/package.mjs` (ADR 076) publie avec `-p:UseCorePackage=true` dans la copie de
   travail aussi, ses fichiers de verrouillage écrits dans `obj` (`NuGetLockFilePath`) ; `build/package.mjs` refuse un
   paquet plus ancien que les sources du domaine ou de la chaîne, un `deps.json` sans `Topogenia.Core` ou avec le projet
   du domaine, sur le serveur un `Topogenia.Domain.dll` qui n'est pas celui du paquet, et un fichier de verrouillage du
   dépôt changé. La licence du cœur du ZIP est celle de son paquet.
9. **L'intégration continue du dépôt public** restaure le cœur depuis une adresse privée que donne le titulaire (secret
   `CORE_PACKAGE_URL`, variable `CORE_PACKAGE_SHA256`, vérifiée) ; sans elles, l'étape échoue en disant pourquoi. La copie
   qui a les sources du cœur saute cette étape. Rendre le paquet public (le porter dans Git) serait une décision de
   licence du titulaire, pas de la construction.
10. **La destination de `split.mjs`** (garde-fou, même round) : un dossier non vide n'est vidé que s'il a la solution de
   la copie, la bascule du cœur dans son `Directory.Build.props`, aucune source du cœur, et en haut seulement ce que la
   séparation écrit ou ce que Git ignore ; sinon le script échoue sans rien effacer (prouvé sur deux leurres).

## Conséquences

- Le dépôt public compile et passe ses tests avec le seul paquet (chiffres au journal, `Travail/split-p7-t04b.log`).
- Le paquet n'est pas dans le dépôt public (`artifacts/` est ignoré) : celui qui reçoit la licence écrite reçoit le
  paquet et le pose dans `artifacts/core-feed` (`CONTRIBUTING.md`). Aucun flux de paquets public.
- Un correctif proposé au dépôt public se reporte à la main dans la copie de travail, puis une nouvelle séparation.
- Clé de développement créée pour la preuve hors du dépôt (`45_Topogenia\Cle-coeur\core.snk`, jeton
  `0124debc601359fe`) ; le titulaire peut en créer une autre : seul le jeton change.
- Un premier script PowerShell de la chaîne, qui créait la clé par la CryptoAPI, a été verrouillé puis supprimé par
  l'antivirus du poste dès son écriture, avant toute exécution : la clé se crée par `sn.exe`, la chaîne est en MSBuild,
  la séparation en Node.

## Vérification

`dotnet pack build/core/Topogenia.Core.csproj -c Release -p:CoreKeyFile=...` (paquet, jeton, empreinte) ;
`node build/split.mjs ../topogenia-public` (preuve complète) ; dans la copie de travail, `DependencyRulesTests`
(dont `The_domain_is_built_from_its_sources_or_comes_signed_from_its_package` et la condition du paquet du cœur) et toute
la suite d'architecture sans test sauté.
