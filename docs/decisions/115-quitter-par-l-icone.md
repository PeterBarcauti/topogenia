<!-- 115-quitter-par-l-icone.md -->
<!-- Quitting by the icon near the clock (3.4, lot 7): the page declares to the host the writes still waiting, the icon waits 5 s at most then asks « Quitter quand même ? » (No by default), one route, one service, one more method on the startup window; a form changed and not validated is declared apart and asked for at once (R11, 21/11/2026) -->

# 115 - Quitter par l'icône près de l'horloge

Statut : acceptée le 21/11/2026 (3.4, lot 7, F-204). Prolonge les ADR 044 (« Quitter » du mode Poste), 064 (la file
d'enregistrement de la fiche), 069 (« Quitter » du menu du compte, saisies en attente envoyées d'abord, AR-135 et
AR-143) et 090 (l'icône près de l'horloge). Demande du plan de la 3.4 : l'icône ne doit plus effacer une frappe faite
juste avant.

## Contexte

- Le menu du compte passe par `web\src\app\quit.ts` : `sendPendingWrites()` (`shared\state\pendingWrites.ts`) envoie ce
  qui attend, puis `POST /api/desktop/quit` (`DesktopQuit.Stop`). L'icône, elle, arrête l'application sans rien
  demander à la page : `DesktopTray.cs`, `case TrayCommand.Quit: lifetime.StopApplication();`.
- Ce qui attend est connu de la page seule : `pendingWrites.ts` suit, par fiche ouverte, la file de la saisie
  (`useSheetState.ts`) et la file des documents ; une file dont le panneau se ferme reste suivie jusqu'à sa dernière
  réponse (lot 4). L'hôte n'en sait rien.
- La sauvegarde de la fiche part 1 200 ms après la dernière frappe (`autoSave.ts`). Une saisie « tenue » (borne de
  durée à l'envers, `ChangeOptions.hold`) ne part jamais seule. Kestrel termine les requêtes reçues à l'arrêt, pas
  celles que la page n'a pas encore envoyées.
- Il n'existe aucun canal de l'hôte vers la page (ni `EventSource`, ni `WebSocket`, ni interrogation périodique dans
  `web\src`). Le mode Poste est servi sur `127.0.0.1` ; aucune requête ne sort (ADR 014).
- Plusieurs onglets peuvent être ouverts sur la même application.

## Décision

1. **La page déclare à l'hôte ce qui attend.** Un module de `web\src\app\`, monté par le shell quand `profile.desktop`
   (rien en mode Serveur), tient un identifiant d'onglet (`crypto.randomUUID()`) et s'abonne à `pendingWrites`. À chaque
   bascule de `writesWaiting()`, il envoie `POST /api/desktop/writes` `{ tab, waiting }`. À `pagehide`, il envoie
   `{ waiting: false }` avec `keepalive`.
2. **`pendingWrites.ts` gagne `subscribe`.** Chaque file prévient quand son `unsaved()` peut avoir changé ; la file des
   documents prévient à `add` et à son `finally`. Ordre exigé dans `autoSave.ts` : la saisie refusée est remise « en
   attente » avant que l'abonné soit prévenu ; aujourd'hui `setState({ status: 'failed' })` précède la remise en
   attente, et un abonné qui lit `unsaved()` à ce moment voit « rien n'attend » alors que la saisie attend. Un essai
   tombe sans la correction de l'ordre.
3. **L'hôte tient l'ensemble des onglets en attente et l'icône l'attend.** Route `POST /api/desktop/writes` (mode
   Poste, cookie de session ; `INTROUVABLE` en mode Serveur, comme `/api/desktop/quit` ; sans `If-Match`, comme elle).
   Un service dans `src\Topogenia.Host\Desktop\` (inscrit dans `DesktopServices.cs`) tient l'ensemble : identifiant de
   **64 caractères au plus**, **32 onglets au plus**, refus 400 au-delà. `DesktopTrayService.Choose(Quit)` : aucun onglet
   en attente, arrêt immédiat comme aujourd'hui ; sinon l'attente se fait hors du fil de l'icône (sa boucle de messages
   ne se bloque jamais), **bornée à 5 s** ; tout est parti, arrêt ; il reste une attente, la boîte de la décision 4.
4. **Une boîte de Windows pour ce qui ne peut pas partir.** L'icône n'a pas de page pour dire un refus. Après les 5 s,
   une boîte Oui/Non demande « Quitter quand même ? », **Non par défaut**. Non : l'application continue. Oui : arrêt.
   Refuser sans choix laisserait la personne sans moyen de quitter si un onglet a été tué. La boîte passe par
   `IStartupWindow` (`Startup\ConsoleWindow.cs`), une méthode de plus, `Confirm`, qui réutilise `MessageBoxW` déjà
   déclaré dans ce fichier : la liste des fonctions natives de `NativeCodeRulesTests` ne change pas, `DesktopTray.cs`
   n'en déclare aucune. Textes nouveaux dans les 18 `Startup\messages.*.json` (modèle : `ICONE_OUVRIR`,
   `ICONE_QUITTER`).
5. **Inchangés** : la fin de session de Windows (le navigateur se ferme en même temps), le menu du compte, `POST
   /api/desktop/quit`.
6. **Le détail `unit` du refus 413** (lot 7, « 0 Mo », arbitré le 21/11/2026) est ajouté au corps de l'erreur sans rien
   retirer : `max` se dit en Mo (`unit` valant `MB`) quand la limite est un nombre entier de Mo, en Ko dès qu'elle ne l'est pas
   (1,5 Mo donne `max` = 1536 et `unit` = `KB`, texte `REQUETE_TROP_GROSSE_KO`). Rien d'autre dans cette décision n'en dépend.

## Écarté

- **L'hôte demande à la page** : chaque onglet garde une requête ouverte (attente longue ou `EventSource`) et l'icône y
  répond « quitter ». Un navigateur n'ouvre que six connexions par adresse en HTTP/1.1 sur `127.0.0.1`, toutes pages
  confondues : au-delà, l'application se figerait ; sinon il faut un onglet chef et `BroadcastChannel`. Une connexion
  permanente, son renouvellement et sa libération à l'arrêt coûtent plus que deux petites requêtes par rafale de
  frappe. La saisie tenue demanderait en plus une réponse `unsaved` remontée par `quitApplication()`.
- **Refuser sans choix** quand une saisie ne peut pas partir : sans page ouverte pour la porter, la personne ne
  pourrait plus quitter.
- **Une attente sans borne** : l'icône semblerait morte.
- **Une fonction native de plus** pour la boîte : refusée par `NativeCodeRulesTests` ; `MessageBoxW` suffit.

## Conséquences

- Une route nouvelle (`POST /api/desktop/writes`, mode Poste seulement), un service dans `Desktop\`, une méthode de
  `IStartupWindow`, dix-huit fichiers de messages de l'hôte ; contrat OpenAPI et `schema.ts` régénérés. Aucune
  migration, aucun fichier du cœur fermé, aucune dépendance, aucun appel sortant (`connect-src 'self'` inchangé).
- Deux restes assumés : un onglet tué sans `pagehide` laisse sa déclaration, l'icône pose alors la question et Oui
  quitte ; une déclaration encore en route au clic exact sur l'icône (quelques millisecondes après la première frappe)
  n'est pas vue, ce qui est impossible en pratique, la main passant du clavier au menu.
- La spécification (§ 3.2.1, « n'attend pas une saisie de fiche encore en route ») est corrigée au lot 7 avec cette
  décision pour référence ; `docs\exploitation-poste.md` § 2.4 est mis à jour ; le manuel (Quitter, En cas de problème)
  dit les deux chemins.

## Vérification

Essais écrits avec le code de chaque partie :

- Hôte : `DesktopWritesRoutesTests` et `DesktopWindowedTests` (`The_icon_quits_at_once_when_no_page_waits` ; l'icône
  attend la page qui a déclaré une saisie, pas d'arrêt 500 ms plus tard, arrêt en moins de 2 s une fois la déclaration
  levée ; encore en attente après 5 s, la boîte est posée, Non laisse l'application ouverte, Oui l'arrête ; deux
  onglets attendus chacun ; identifiant de plus de 64 caractères et 33e onglet refusés en 400 ; session exigée et 404 en
  mode Serveur) ; la route dans `RouteTableTests`, `RightsMatrixTests`, `IfMatchRequirementTests`, `ApiContractTests` ;
  architecture verte.
- Interface : `pendingWrites.test.ts` (un abonné apprend chaque bascule ; une sauvegarde refusée laisse « en attente »,
  rouge sans l'ordre corrigé) ; un essai du module de `app\` (une frappe envoie `{ waiting: true }` une fois, puis
  `{ waiting: false }` après la réponse ; les documents de même ; `pagehide` envoie `false` avec `keepalive` ; rien en
  mode Serveur) ; `saving.test.tsx` et `quit.test.tsx` restent verts ; parcours `web\e2e\quit.spec.ts`.
- Témoin de mutation : retirer l'attente de l'icône, voir l'essai « attend la page » tomber, remettre.

## Retouche R11 (21/11/2026) : un formulaire ouvert, modifié, non validé

**Constat du porteur.** Dans une fiche, une nouvelle entrée extérieure, un nom tapé sans « Enregistrer le document »,
puis « Quitter » par l'icône : l'application s'arrête sans rien demander, la saisie est perdue. La décision ne suivait
que les écritures en attente (file de la saisie, file des documents) ; un formulaire à valider n'en est pas une, rien ne
le déclarait.

**Inventaire des formulaires à valider.** Dans la fiche : le formulaire d'un document (`DocumentForm` : ajout ou
modification d'une sortie, ajout ou modification d'une entrée extérieure), la question de l'animateur
(`ClarificationRequest`) et sa réécriture (`ClarificationNote`). Dans les volets qui écrivent dans le projet : le libellé
d'une photo ou de la référence relue (`SnapshotsSection`, volet « Analyse »). Les autres champs ne se valident pas : la
saisie de la fiche (finalité, déroulé et ses opérations, outils en pastilles, texte libre) part seule 1 200 ms après la
dernière frappe ; l'usage d'une entrée, son irritant et son plan d'action partent à la sortie du champ, que le
navigateur provoque aussi quand sa fenêtre perd la main (clic sur l'icône), puis passent par la file des documents ; le
statut d'un irritant part au choix. Le champ flottant du flux de valeur est abandonné à la sortie par conception (« un
clic ailleurs abandonne ») ; le nom d'une étape ou d'un couloir tapé sur la carte se valide à la sortie. Les boîtes de
dialogue modales (propriétés du projet, traductions…) ne sont pas des volets : hors de R11.

**Décision.**

1. `pendingWrites.ts` gagne `trackUnsavedForm()` et `formsUnsaved()` : un ensemble à part des files d'écriture, annoncé
   par le même `subscribeWrites`. `sendPendingWrites()` ne l'attend pas : rien ne l'envoie. Le crochet
   `shared\state\useUnsavedForm.ts` suit un formulaire tant qu'il diffère de ce qu'il était à l'ouverture ; validé,
   annulé, fermé ou revenu à son état d'ouverture, la déclaration est levée. Ouvert sans rien changer : rien. Le
   formulaire d'un document compare l'entrée à celle de son ouverture, chaque document ayant son formulaire (`key`).
2. **Déclaration distincte**, ajout pur au contrat : `POST /api/desktop/writes` `{ tab, waiting, unsaved? }`, `unsaved`
   envoyé seulement quand il est vrai, absent lu faux (une page d'avant reste comprise). L'hôte tient un second ensemble
   (`DesktopWrites.Unsaved`) ; un onglet qui dit les deux compte une fois dans les 32 ; 64 caractères et 32 onglets
   inchangés.
3. **L'icône demande tout de suite.** Un formulaire non validé ne partira jamais seul : attendre 5 s ne servirait à
   rien. La boîte est posée aussitôt (hors du fil de l'icône), avec sa question à elle, `ICONE_QUITTER_NON_VALIDE`
   (« Un formulaire ouvert n'est pas enregistré : sa saisie sera perdue. Quitter quand même ? »), Non par défaut. Oui :
   les écritures en route (une frappe de la fiche dans ses 1 200 ms) ont encore leurs 5 s au plus, puis l'arrêt, sans
   seconde question. Les écritures attendues parties mais un formulaire resté non validé : la même question, aussitôt.
4. **Le « Quitter » du menu du compte** : sa confirmation le dit (« Un formulaire ouvert n'est pas enregistré : sa
   saisie sera perdue. »), son bouton devient « Quitter quand même » (danger) ; « Annuler » garde le focus, comme Non par
   défaut. Il ne refuse pas : la page est sous les yeux, le formulaire derrière la boîte.
5. **Mode Serveur** : rien ne change ; la route n'existe pas, le module de déclaration n'est pas monté, le menu n'a pas
   « Quitter ».

**Écarté.** L'attente de 5 s puis la question : un formulaire ne part pas seul, l'attente ne ferait que retarder la même
question. Un `waiting` vrai pour un formulaire : l'icône attendrait 5 s pour rien et parlerait de « saisie de fiche »
pour le libellé d'une photo. Le formulaire levé pendant sa propre requête d'envoi : la levée et la fin de l'écriture
arriveraient par deux déclarations, l'icône pourrait s'arrêter entre les deux et perdre un formulaire refusé ; pendant
les quelques millisecondes de la requête, la question est posée et Non ne perd rien.

**Essais.** Interface : `desktopWrites.test.tsx` (ouvert sans frappe, rien ; un nom tapé, déclaré ; annulé, effacé,
validé ou Échap, levé ; un autre document ouvert repart de lui ; question de l'animateur et sa réécriture ; formulaire et
écriture dits ensemble, `pagehide` lève les deux), `pendingWrites.test.ts`, `quit.test.tsx`, `snapshots.test.tsx`.
Hôte : `DesktopWritesRoutesTests.A_form_not_validated_is_counted_apart_and_a_tab_counts_once_within_the_same_bounds`,
`DesktopWindowedTests` (boîte en moins d'une seconde avec sa question ; Non laisse ouvert ; Oui avec une écriture en
route l'attend ; Oui seul arrête aussitôt ; formulaire resté après les écritures, demandé) ; vus rouges avec un
`Unsaved` toujours nul. Parcours `web\e2e\quit.spec.ts` (écrit, non lancé).
