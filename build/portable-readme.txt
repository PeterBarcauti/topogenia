{{PRODUCT}} 3.0 - mode Poste (version portable)
==============================================

Lancer
------
1. Décompresser le ZIP dans un dossier à soi (par exemple Documents\{{PRODUCT}}). Garder les fichiers ensemble :
   {{PRODUCT}}.exe a besoin de wwwroot, e_sqlite3.dll et Microsoft.Data.SqlClient.SNI.dll à côté de lui.
2. Double-cliquer sur {{PRODUCT}}.exe. Une fenêtre de console s'ouvre, puis le navigateur sur l'application.
3. Pour quitter : le menu du compte, en haut à droite, puis « Quitter {{PRODUCT}} » (ou fermer la fenêtre de la console).

Un second lancement ouvre simplement le navigateur sur l'application déjà lancée.

Données
-------
Le fichier de données, ses sauvegardes et le journal sont dans %LOCALAPPDATA%\{{PRODUCT}} (rien n'est écrit ailleurs).
Réglages facultatifs (port, emplacement des données, langue) : %LOCALAPPDATA%\{{PRODUCT}}\settings.json.

Vérifier la copie
-----------------
L'empreinte SHA-256 du ZIP est publiée à côté de lui (fichier .sha256). Sous PowerShell :
  Get-FileHash -Algorithm SHA256 .\{{PRODUCT}}-*-portable.zip
Le fichier n'est pas signé : Windows peut afficher un avertissement SmartScreen au premier lancement.

Licences et composants
----------------------
Usage libre et gratuit, y compris en entreprise. Le dossier licenses contient la licence du produit (Apache-2.0),
celle du coeur, et celles des composants tiers ; sbom.cdx.json décrit les composants livrés (CycloneDX 1.5).

------------------------------------------------------------------------------------------------------------------

{{PRODUCT}} 3.0 - desktop mode (portable)

Unzip into a folder of your own and keep the files together; double-click {{PRODUCT}}.exe: a console window opens, then
the browser on the application. To quit: the account menu (top right), then "Quit {{PRODUCT}}". A second start opens the
browser on the application already running. Data, backups and log: %LOCALAPPDATA%\{{PRODUCT}}. Free to use,
including in a company. Licences: licenses folder; components: sbom.cdx.json.
