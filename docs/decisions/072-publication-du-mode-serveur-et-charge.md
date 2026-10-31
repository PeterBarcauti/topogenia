<!-- 072-publication-du-mode-serveur-et-charge.md -->
<!-- The publication of the server mode for IIS (profile, web.config, example of settings, ZIP), the load of 30 clients on the published host, and what moves to the installation recette (P7-T02, AR-145) -->

# 072 - Publication du mode Serveur et charge de 30 clients

Statut : acceptée (date 29/10/2026)

## Contexte

La spec 6.2 veut `Topogenia-x.y.z-server.zip`, dépendant du Hosting Bundle .NET 10, un `web.config`, un
`appsettings.Production.example.json` et le guide `docs/exploitation-serveur.md`. NF07 (S-NF-09) veut 30 utilisateurs
simultanés sans dégradation perceptible, mesurés sur l'hôte publié (renvoi de la phase 4). AR-110 prévoyait de démarrer
l'hôte publié sous un compte membre du seul rôle `topogenia_app`. Sur le poste de construction, IIS n'est pas installé
et ne peut pas l'être (prompt 4.4) ; il n'y a qu'un compte Windows ; aucun objet de serveur SQL hors des bases
`Topogenia_*` ne se crée (prompt 4.2).

## Décision

1. **Profil** `src/Topogenia.Host/Properties/PublishProfiles/Server.pubxml` : `win-x64`, dépendant du framework, ni fichier
   unique, ni symboles, ni documentation.
2. **`web.config`** du dépôt (`src/Topogenia.Host/web.config`), complété à la publication du module ASP.NET Core
   (« in-process ») : `maxAllowedContentLength` = 101 Mio (le plus grand corps de l'hôte, un paquet de 100 Mio,
   `ExchangeApi.PackageLimit`, et 1 Mio d'enveloppe : la limite par défaut d'IIS, 30 Mo, refuserait un gros paquet),
   `removeServerHeader`, `X-Powered-By` retiré. La publication du mode Poste ne le prend pas.
3. **Exemple de réglages** `build/server/appsettings.Production.example.json` (valeurs fictives, authentification Windows,
   aucun mot de passe) et `build/server/server-readme.txt` ; ZIP par `node build/package.mjs Server` (ADR 076), le même script que
   le mode Poste (ADR 071), avec le guide d'exploitation et les licences.
4. **Charge** (AR-145) : `build/load/ServerLoad.cs`, jamais livré, sur l'hôte publié lancé sur Kestrel en HTTPS (certificat
   de test en mémoire), base `Topogenia_Charge` créée par `--migrate` puis supprimée ; 30 clients HTTP en Negotiate, chacun
   sur son projet, 10 tours des gestes courants ; un client seul, puis les 30 ensemble avec une seconde entre deux gestes
   (rythme bien au-delà de 30 personnes), puis en rafale. **Critère** : aucun échec, et chaque geste sous une seconde au
   95e centile avec la pause (la limite de la fluidité perçue d'une action) ; la rafale est donnée pour la marge.
5. **Renvoyé à la recette d'installation** (`docs/recette/finale/`) : le comportement sous IIS (module, `web.config`,
   authentification Windows d'IIS) et le démarrage sous un compte membre du seul rôle `topogenia_app` (AR-110 ajusté :
   créer ce compte de connexion sort des bases `Topogenia_*`) ; les droits du rôle restent prouvés par la persistance.

## Conséquences

- Mesuré (`docs/couverture-p7/verification-serveur.md`) : avec la pause, 95e centile de 48 à 101 ms pour les gestes de la
  carte et de la fiche, 542 ms pour la liste des projets de l'accueil ; en rafale, 1,4 s pour l'accueil, moins de 0,7 s
  pour le reste ; 2 334 requêtes par passage, aucun échec. La liste des projets est le geste qui s'alourdit (F-42).
- 30 sessions d'un même compte, faute d'autres comptes : l'hôte ne garde par utilisateur que son profil, l'écart est
  écrit dans la vérification.
- Le ZIP du serveur se construit une fois le guide d'exploitation écrit (P7-T04).
