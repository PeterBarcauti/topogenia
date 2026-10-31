{{PRODUCT}} 3.0 - mode Serveur (IIS)
==================================

Contenu
-------
- {{PRODUCT}}.dll, {{PRODUCT}}.exe et leurs bibliothèques : l'application, dépendante du Hosting Bundle .NET 10 (à
  installer sur le serveur par l'exploitation, avec IIS).
- web.config : le module ASP.NET Core en mode « in-process », la taille de requête la plus grande que l'application
  accepte (un paquet d'échange de 100 Mo), aucun en-tête qui nomme le serveur.
- appsettings.Production.example.json : la configuration à copier en appsettings.Production.json et à adapter (base,
  noms du site, administrateurs, langues). Toutes ses valeurs sont des exemples.
- exploitation-serveur.md : le guide d'exploitation du serveur.
- wwwroot : l'interface.
- licenses : la licence du produit (Apache-2.0), celle du coeur, et celles des composants tiers.
- sbom.cdx.json : les composants livrés (CycloneDX 1.5).

Installer
---------
Le guide d'exploitation, exploitation-serveur.md, à côté de ce fichier, décrit le pool d'applications, le compte de
service, les droits SQL, le certificat, la migration par « {{PRODUCT}}.exe --migrate », les sauvegardes SQL Server et la
supervision.

Vérifier la copie
-----------------
L'empreinte SHA-256 du ZIP est publiée à côté de lui (fichier .sha256). Sous PowerShell :
  Get-FileHash -Algorithm SHA256 .\{{PRODUCT}}-*-server.zip

------------------------------------------------------------------------------------------------------------------

{{PRODUCT}} 3.0 - server mode (IIS): the application (needs the .NET 10 Hosting Bundle), its web.config, an example of
appsettings.Production.json, the interface, the licences and the components (sbom.cdx.json). Installation guide:
exploitation-serveur.md, beside this file.
