<!-- 081-traductions-d-une-proposition.md -->
<!-- The translations of a proposal of the assistant (3.0, after the trial of lot 4): carried by the state wanted, compared and written as automatic ones in the closed core, a manual one never replaced, the languages said to the model, the content read by the preview and the application -->

# 081 - Traductions d'une proposition

Statut : acceptée (date 30/10/2026). Corrige l'ADR 079 (propositions appliquées à la carte). Spécification :
`TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 3.1, 3.4 et 7.

## Contexte

À l'essai de l'exécutable du lot 4, le porteur du projet a demandé au volet, interface en anglais sur un projet en
français, de traduire toute la carte en anglais. Le modèle a répondu par une proposition qui portait bien les traductions
(`translations.en` du `.pmap` 1.1), mais rien n'a été traduit : la carte voulue du cœur (`DesiredLane`, `DesiredStep`,
`DesiredFlow`) ne portait pas les traductions. En le corrigeant, un test de l'hôte a montré que l'aperçu ne chargeait que
la structure du projet (`WorkspaceContent.None`) : il ne voyait pas les traductions existantes. La consigne ne disait pas
non plus au modèle les langues du projet.

## Décision

1. **La carte voulue porte les traductions** des noms de couloirs, des libellés d'étapes et des libellés de flèches, par
   langue ; le format les recopie du `.pmap` (`PmapFiles.Desired`).
2. **Contrôle** : une langue qui n'est pas configurée est refusée d'abord par le lecteur du `.pmap` (`FICHIER_INVALIDE`,
   chemin de la traduction ; le volet le montre comme un défaut du fichier et le renvoie une fois au modèle), puis par le
   cœur (`LANGUE_INVALIDE` sur `translations`, comme une traduction saisie). Les textes suivent les règles des textes
   saisis. La langue d'origine de l'objet est ignorée ; une langue que la proposition ne donne pas est gardée ; un texte
   vide ne supprime rien.
3. **Écriture** : les traductions d'une proposition s'écrivent comme des **traductions automatiques** (source `auto`,
   comme l'import du classeur de traductions) : une traduction saisie ensuite à la main l'emporte, et un libellé changé
   plus tard fait tomber la traduction automatique au lieu de la laisser fausse. Elles s'écrivent **en dernier**, une fois
   chaque texte à sa valeur voulue (un libellé changé dans la même proposition fait d'abord tomber les anciennes
   automatiques), pour les objets gardés comme pour les nouveaux, dans la même opération (un seul Ctrl+Z). La carte
   détaillée qui porte le libellé de son étape suit sa traduction, comme à l'import.
4. **Une traduction saisie à la main n'est jamais remplacée** par une proposition : le travail d'une personne passe avant
   celui du modèle. L'aperçu le dit (avertissement `translation_kept:CODE`) ; le champ `translations` figure parmi les
   champs changés d'un objet quand une langue change.
5. **Contenu chargé** : l'aperçu (`PreviewProposalQuery`) charge désormais les traductions. « Appliquer »
   (`ApplyProposalCommand`) n'a rien à ajouter : comme toute commande d'une carte, il charge avec elle les fiches, les
   documents et les traductions de la carte qu'il rend (`MapViewBuilder.Content`).
6. **Consigne** : le message de chaque échange dit la langue d'origine du projet et les langues configurées
   (`<languages origin="fr" configured="fr, en" />`, sans `origin` pour un projet à créer) ; la consigne commune
   (section Proposals, « Translating ») fait d'une demande de traduction une proposition qui garde chaque nom et chaque
   libellé dans sa langue et remplit `translations`, seulement dans les langues configurées, jamais dans la langue
   d'origine.

## Conséquences

- Pas de migration : les traductions sont les enregistrements polymorphes de l'annexe A 2.14.
- Le paquet du cœur fermé est refait (le domaine a changé) avant le paquet de l'application.
- Une étape nouvelle d'une proposition prend, comme une étape saisie, la langue de l'interface de la personne.
- Tests : domaine (`ProposalTranslationTests`), hôte sur les deux bases (`AssistantProposalTests`, langues du message
  dans `AssistantPanelTests` et `AssistantExchangeConnectorTests`), interface (`ProposalPreview.test.tsx`), parcours
  « traduire, basculer en EN, Ctrl+Z » sur le faux connecteur (`assistant-panel.spec.ts`).
