<!-- 088-paquet-msix-du-store.md -->
<!-- The desktop mode also ships as an MSIX package for the Microsoft Store: the identity reserved in Partner Center, its own data folder, the reports of the replies of a model, built by makepri and makeappx -->

# 088 - Le paquet MSIX du Microsoft Store

Statut : acceptée (date 01/11/2026). Prolonge les ADR 049 (paquets et signature), 071 (mode Poste) et 076 (chaîne des
paquets sans script du système). Décision du porteur du projet du 01/11/2026 : « on enchaîne par la version du
programme publiable sur le store Microsoft ».

## Contexte

L'exécutable du ZIP portable n'est pas signé : Windows affiche l'avertissement SmartScreen au premier lancement. Un
certificat de signature de code coûte chaque année, affiche le nom civil de l'éditeur, et n'efface l'avertissement
qu'après une réputation acquise. Une application publiée sur le Microsoft Store est signée par Microsoft : aucun
avertissement, compte individuel gratuit, nom d'éditeur au choix. Le Store distribue un paquet MSIX, dont Windows
virtualise une partie des écritures : sous `%LOCALAPPDATA%`, un fichier qui existe déjà s'ouvre à sa place réelle, mais
tout fichier nouveau part dans un dossier privé du paquet. Les politiques du Store (version 7.20) demandent aussi, pour
une application qui produit du contenu par une IA générative, un moyen de signaler un contenu inapproprié à
l'éditeur (11.16).

## Décision

1. **Le mode Poste seul**, en paquet MSIX, par `node build/package.mjs Store` : la publication du profil Desktop telle
   quelle (même exécutable, même interface, mêmes licences, même SBOM), plus le manifeste (`build/store/AppxManifest.xml`),
   les logos (`build/store/Assets`, tirés de la marque) et leur index (`resources.pri`, écrit par `makepri` selon
   `build/store/priconfig.xml`). `makepri` nomme une ressource par son chemin depuis le dossier qu'il indexe : il
   indexe, depuis sa racine, un dossier qui ne tient que `Assets`, pour que chaque logo nommé par le manifeste
   (`Assets\...`) soit une ressource de l'index ; sinon Windows montre son icône par défaut dans le menu Démarrer et la
   recherche. La construction le vérifie dans un vidage de l'index. L'exécutable porte son propre manifeste
   (`src/Topogenia.Host/app.manifest`) : attentif à l'échelle de chaque écran (`PerMonitorV2`, sans quoi le Windows App
   Certification Kit avertit), lancé avec les droits de la personne (`asInvoker`). Le mode Serveur n'est pas sur le
   Store ; le ZIP portable reste publié.
2. **L'identité réservée dans Partner Center**, écrite telle quelle dans le manifeste : `PeterBarcauti.Topogenia`,
   `CN=2AA7CE71-2550-41D9-812A-A5E5AC8EC7CA`, éditeur affiché « Peter Barcauti ». Version en quatre parties, la dernière
   à 0 (réservée au Store). Application de bureau en confiance totale (`runFullTrust`), aucune autre capacité. Langues
   déclarées : anglais et français (la fiche du Store) ; l'interface garde ses dix-huit langues.
3. **Le dossier des données du paquet** : quand le processus a une identité de paquet (`GetCurrentPackageFamilyName`),
   le dossier du produit par défaut est `%LOCALAPPDATA%\Packages\<famille du paquet>\LocalState\Topogenia`, jamais
   celui de la version portable ; un `Folder` ou un `DataFile` réglés restent prioritaires. Sans paquet, rien ne change.
4. **Signaler une réponse d'un modèle** : sous chaque réponse des connecteurs Anthropic et Bedrock, un lien ouvre un
   courriel à l'éditeur (`peter.barcauti@` suivi du domaine du produit), avec un objet prérempli et rien de la réponse.
   Une réponse collée depuis l'IA de la personne n'en a pas.
5. **Outils** : `makepri` et `makeappx` du SDK Windows, lancés directement comme `signtool` (ADR 076), trouvés dans le
   PATH ou dans le dossier des kits. Le paquet est relu par `makeappx unpack` et doit rendre ses fichiers octet pour
   octet. Il n'est signé qu'avec `--certificate`, pour un essai sur un poste (certificat d'essai au nom de l'éditeur) ;
   le paquet remis au Store part sans signature, Microsoft le signe.

6. **Le projet d'exemple dans la langue de l'interface** (décidé par le porteur pendant la fiche du Store) : traduit
   seulement, l'exemple s'affichait, sous une interface en anglais, comme une traduction verrouillée (ADR 083), avec ses
   fiches en français. `samples/leave.topogenia` est le même projet écrit en anglais (`LEAVE`), fiches comprises, avec ses
   traductions françaises ; `samples/conges.topogenia` garde le français, traduit en anglais en entier. L'hôte importe le
   premier pour toute interface qui n'est pas en français.

## Conséquences

- Installée depuis le Store, l'application ne voit pas les données de la version portable : on passe de l'une à l'autre
  par l'export et l'import des fichiers `.topogenia`. Désinstaller le paquet efface ses données (politique 10.2.7) :
  les exports restent où la personne les a mis.
- Le paquet garde la fenêtre de la console du mode Poste (messages de démarrage, restauration proposée) ; la fermer
  arrête l'application, comme pour le ZIP.
- Une identité de paquet changée dans Partner Center se recopie dans le manifeste ; une règle d'architecture la fige.
- La fiche du Store, la politique de confidentialité et la soumission se font hors du dépôt (`REPRISE_PUBLICATION.md`).
