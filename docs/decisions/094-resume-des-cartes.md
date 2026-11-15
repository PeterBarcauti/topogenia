<!-- 094-resume-des-cartes.md -->
<!-- The summary of a map (3.1): one sentence per map and per detailed level, written by the AI after review or by hand, kept in its own table, shown in a bubble at the title when the map opens -->

# 094 - Le résumé d'une carte

Statut : acceptée (date 06/11/2026). Prolonge les ADR 078 (volet de l'assistant), 083 (langues), 091 (tracé choisi) et
093 (rapport raconté par l'IA). Demande du porteur du projet du 06/11/2026 : à l'ouverture d'un diagramme, une info-bulle
dit en une phrase courte ce que montre la carte ; une fonction du menu fait écrire ces phrases par l'IA pour chaque carte
et ses niveaux détaillés, et les enregistre dans la base. Décisions du porteur : la bulle s'accroche au titre de la carte
(rien sur le diagramme) et une phrase écrite par l'IA est relue avant d'être gardée.

## Contexte

- Une carte n'a qu'un nom, qui suit l'étape qu'elle détaille ou le nom du projet ; aucun texte de carte ne se saisit seul.
- Rien ne compte les changements d'une carte : sa version ne bouge que si son enregistrement change, et la version de
  structure du projet bouge à chaque commande, y compris celles qui ne changent rien.
- Les fiches vivent dans leur propre table à côté de l'étape, hors du journal des opérations.

## Décision

1. **Une table à part, `map_summary`**, une ligne par carte au plus (`UQ_map_summary_map_id`) : texte de 200 caractères au
   plus sur une ligne, langue d'origine, origine (`ai` ou `manual`), modèle de l'IA, empreinte de la carte décrite,
   version et audit. Jamais une colonne de `process_map` : elle changerait l'enregistrement de la carte à chaque résumé,
   bloquerait l'annulation des opérations qui la touchent, rendrait un ancien résumé à l'annulation d'un renommage et
   ferait bouger la version que vérifient la suppression et le rattachement d'un détail.
2. **Pas d'annulation** : un résumé ne passe pas par le journal des opérations (`LogKind` nul, comme les fiches et les
   traductions) ; il a ses propres versions.
3. **Péremption par empreinte du contenu** (`MapFingerprint`) : FNV-1a sur 64 bits des couloirs vivants (nom, type), des
   étapes vivantes (couloir, type, libellé, détaillée par un niveau qui a du contenu) et des flèches vivantes (source,
   cible, libellé), chacun trié par identifiant. Sans positions ni versions : un déplacement ne périme rien, une
   annulation rend l'empreinte d'avant. FNV-1a plutôt que SHA-256, que le cœur n'a pas le droit d'utiliser : l'empreinte
   signale un changement, elle ne protège rien.
4. **Droits** : écrire est réservé à l'animateur, comme une étape ou un couloir ; lire, à tout lecteur, dans la vue de la
   carte (`MapView.summary`, avec `stale`).
5. **Une seule route d'écriture** : `PUT /api/maps/{id}/summaries`, une liste de résumés (carte, texte, origine, modèle,
   version lue). Un texte vide efface le résumé et ses traductions. Exemption de l'If-Match « plusieurs résumés » : la
   version de chaque résumé vient dans le corps et se vérifie une à une, comme l'If-Match d'une fiche (0 pour un résumé
   jamais lu).
6. **L'IA par le volet** : intention et forme `summaries`, réservées à l'animateur, envoyées sur la carte racine. Le bloc
   `<maps>` liste les cartes demandées par leur référence (le code du projet pour la racine, le code de l'étape détaillée
   pour un niveau) : sans résumé ou périmées (menu Fichier), ou une seule (« Refaire avec l'IA »). Aucune carte à résumer :
   refus `RESUMES_A_JOUR`, sans appel au modèle. La réponse s'affiche en liste ; chaque phrase est gardée, corrigée ou
   écartée, et seules les phrases gardées partent à la route d'écriture, avec le modèle qui les a écrites.
7. **Interface** : une icône « i » à côté du titre, dans le fil d'Ariane de l'en-tête. À l'ouverture de chaque carte, une
   bulle sous le titre, repliée au premier clic ou au bout de 7 secondes ; le survol ou le focus de l'icône la remontre, un
   clic ouvre la fenêtre du résumé. Les touches restent au gestionnaire du clavier : la bulle, un statut, ne prend aucun
   focus. Préférence locale `app.showSummaryOnOpen`.
8. **Traductions** : nouveau type d'objet `summary` (champ `label`), un champ par type comme le code l'attend ; le classeur
   de traduction les porte ; un texte changé fait tomber ses traductions automatiques.
9. **Formats** : paquet 2.3 (`map.summary`, et `current` pour un résumé qui décrivait encore sa carte, l'empreinte étant
   reprise à l'import car les identifiants changent). Un paquet sans résumé s'écrit encore en 2.2 ou 2.1, lisible par les
   versions d'avant.
10. **Exports** : l'export Markdown et le rapport portent le résumé à jour en italique sous le titre ; un résumé périmé n'y
    figure pas.

## Conséquences

- Une table, deux migrations (SQLite et SQL Server), une contrainte des traductions élargie ; le cœur repaqueté.
- La duplication d'un projet ne reprend pas les résumés, comme elle ne reprend pas les fiches.
- Le projet d'exemple porte deux résumés, l'un écrit par l'IA, l'autre à la main.
