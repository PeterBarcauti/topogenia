<!-- 049-diffusion-sans-signature-et-analyse-locale.md -->
<!-- Paquets non signes diffuses par un dossier interne, MSIX retire, analyse CodeQL en local -->

# 049 - Diffusion sans signature et analyse CodeQL en local

Statut : acceptée (date 24/10/2026)

## Contexte

Réponses du porteur du projet du 24/10/2026 :

- Q-02 : la DSI ne fournira pas de certificat de signature ; aucune signature payante tant que le logiciel n'a pas fait
  la preuve de son intérêt ; la version peut être copiée dans un dossier de téléchargement des sources de l'entreprise.
- Q-07 : l'option payante GitHub Code Security n'est pas activée ; CodeQL est à faire tourner en local, l'option pourra
  venir plus tard.

Faits relevés :

- un paquet MSIX ne s'installe que signé par un certificat auquel le poste fait confiance : sans certificat, il est
  inutilisable sur un poste ordinaire ;
- un exécutable non signé téléchargé depuis Internet porte la marque du web : SmartScreen avertit au premier lancement
  (« Exécuter quand même »). La réputation d'un fichier non signé se compte par fichier : chaque nouvelle version repart
  de zéro, Windows ne lui donne jamais une confiance durable. Un fichier copié depuis un partage du réseau interne ne
  porte en général pas cette marque ;
- la CLI CodeQL est gratuite pour analyser du code publié sous une licence libre ; le cœur fermé (ADR 048) n'en relève
  pas. Elle s'installe à part (interdit à la construction autonome : aucune installation de logiciel).

## Décision

- **Poste** : ZIP portable seul ; le MSIX est retiré de P7-T01. Exécutable et ZIP non signés, empreintes SHA-256
  publiées à côté (P7-T03) pour vérifier le fichier copié. La chaîne de construction garde le point d'entrée de la
  signature : un certificat fourni plus tard signe sans autre changement.
- **Diffusion** : la version est copiée dans le dossier interne de téléchargement ; `docs/exploitation-poste.md` (P7-T04)
  dira la vérification de l'empreinte et le message de SmartScreen pour une copie venue d'Internet.
- **CodeQL** : l'étape de `.github/workflows/ci.yml` est gardée, désactivée tant que l'option n'est pas activée ;
  `build/codeql-local.mjs` (P7-T03, en Node depuis l'ADR 076) lance la CLI CodeQL si elle est installée, sur le C# et le TypeScript du dépôt
  public, et écrit le rapport SARIF dans `TestResults/codeql/` ; sans la CLI, il dit comment l'obtenir et s'arrête sans
  rien installer. Le cœur est couvert par les analyseurs .NET de la construction et par ses tests.

## Conséquences

- Plan : P7-T01 sans MSIX ; P7-T03 ajoute `build/codeql-local.mjs` ; P7-T04 ajoute la diffusion par le dossier interne.
- Spec Q4 : réponse « ZIP portable » ; la question du certificat est close (aucun).

## Vérification

En phase 7 : paquets produits sans MSIX, empreintes publiées ; `build/codeql-local.mjs` sans la CLI s'arrête avec son
message et un code de sortie non nul.
