<!-- architecture-simple.md -->
<!-- Vue d'ensemble pedagogique de l'architecture de Topogenia 2.0, en une page, avec schemas -->

# Topogenia 2.0 en une page

Topogenia sert à **dessiner des processus en couloirs** à plusieurs : chaque couloir est un acteur, chaque case
une étape, chaque flèche un enchaînement ; une étape peut s'ouvrir sur une sous-carte et porter une fiche.

Pour aller plus loin : `architecture-detaillee.md` (chaque élément, son rôle et sa phase de construction), puis
`architecture.md` (la référence technique complète).

## 1. Le principe : des couches qui ne se parlent que dans un sens

```mermaid
flowchart TB
    U([Utilisateur<br/>dans son navigateur])
    W["Interface web<br/>React - ce qu'on voit"]
    H["Hôte<br/>Topogenia.Host - la porte d'entrée"]
    A["Application<br/>Topogenia.Application - le chef d'orchestre"]
    D["Domaine<br/>Topogenia.Domain - les règles du métier"]
    P["Persistance<br/>Topogenia.Persistence - la mémoire"]
    X["Échange<br/>Topogenia.Interchange - les fichiers .topogenia et .pmap"]
    S1[(SQLite<br/>un fichier sur le poste)]
    S2[(SQL Server<br/>une base partagée)]

    U --> W --> H --> A --> D
    A -. "par des ports" .-> P
    A -. "par des ports" .-> X
    P --> S1
    P --> S2
```

Une règle simple, vérifiée par des tests à chaque compilation : **une couche ne connaît que celle du dessous**.
Le domaine, au centre, ne connaît personne : ni la base, ni le web. On peut donc changer de base ou d'écran
sans toucher aux règles du métier.

| Couche | Son rôle, en une phrase | Image |
|---|---|---|
| Interface web | Afficher la carte et transmettre les gestes de l'utilisateur | la vitrine |
| Hôte | Recevoir les requêtes, vérifier qui parle et qu'il en a le droit, répondre | le guichet |
| Application | Dérouler chaque action toujours dans le même ordre (droits, verrou, règles, écriture, journal) | le chef d'orchestre |
| Domaine | Dire ce qui est permis : une étape par case, un seul animateur au moins, etc. | le règlement |
| Persistance | Lire et écrire dans la base, la même chose sur les deux moteurs | la mémoire |
| Échange | Lire et écrire les fichiers d'import et d'export | la poste |

## 2. Deux façons de s'en servir

```mermaid
flowchart LR
    subgraph Poste["Mode Poste : seul sur son PC"]
        B1([Navigateur]) -->|127.0.0.1 seulement| E1[Topogenia.exe]
        E1 --> F[(fichier SQLite)]
    end
    subgraph Serveur["Mode Serveur : à plusieurs"]
        B2([Navigateurs de l'équipe]) -->|HTTPS + compte Windows| E2[Topogenia sur IIS]
        E2 --> Q[(SQL Server)]
        Q --> R[[Rapports Power BI<br/>vues bi en lecture]]
    end
```

Le **même code** sert les deux modes ; seuls changent l'écoute, l'authentification et la base.

## 3. Le trajet d'une action

Exemple : l'utilisateur ajoute un couloir « Achats ».

```mermaid
sequenceDiagram
    participant N as Navigateur
    participant H as Hôte
    participant A as Application
    participant D as Domaine
    participant B as Base
    N->>H: POST /api/maps/{id}/lanes {"name":"Achats"}
    H->>H: identité, garde anti-CSRF, taille du corps
    H->>A: commande "ajouter un couloir"
    A->>B: droits de l'utilisateur sur le projet, verrou du projet
    A->>D: appliquer la règle
    D-->>A: changements (avant / après)
    A->>B: écrire les changements + journal (pour Ctrl+Z) + audit
    A-->>H: résultat + carte à jour
    H-->>N: 200 et la carte redessinée
```

Toutes les actions suivent ce même trajet : c'est ce qui rend le comportement prévisible et les droits
impossibles à oublier.

## 4. Cinq idées à retenir

1. **Le domaine est pur** : il calcule les changements en mémoire, sans base ni réseau ; il se teste vite et seul.
2. **Un seul gabarit pour toutes les actions** : droits, verrou, règles, écriture, journal, toujours dans cet ordre.
3. **Annuler et rétablir côté serveur** : chaque action garde ses changements « avant / après » ; Ctrl+Z les rejoue
   à l'envers, et refuse si quelqu'un a modifié la même chose entre-temps.
4. **Deux moteurs, un seul comportement** : chaque test de données tourne sur SQLite **et** sur SQL Server.
5. **Sécurité par défaut** : écoute locale en mode Poste, HTTPS et compte Windows en mode Serveur, contrôle de
   version (`If-Match`) sur toute modification, aucune donnée sensible dans les journaux.

## 5. Où en est la construction

```mermaid
flowchart LR
    P0[0 Cadrage] --> P1[1 Squelette] --> P2[2 Domaine] --> P3[3 Persistance] --> P4[4 Application<br/>et hôte] --> P5[5 Échange] --> P6[6 Interface] --> P7[7 Paquets<br/>et recette]
    classDef fait fill:#d7ecd9,stroke:#3b7a46,color:#1d3b22
    classDef cours fill:#fdecc8,stroke:#a8741a,color:#4a3208
    classDef afaire fill:#eceff3,stroke:#7a8594,color:#2d333b
    class P0,P1,P2,P3,P4 fait
    class P5 cours
    class P6,P7 afaire
```

Vert : terminé ; orange : en cours (phase 5, l'export du paquet est fait) ; gris : à faire. Le détail par élément est
dans `architecture-detaillee.md`.
