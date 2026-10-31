<!-- 021-journalisation-sans-paquet-tiers.md -->
<!-- Fournisseur de journaux en fichier écrit dans l'hôte, Serilog écarté -->

# 021 - Journalisation sans paquet tiers

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 5.2 demande des journaux dans `%LOCALAPPDATA%\Topogenia\logs\`, conservés 30 jours,
sans contenu métier (identifiants et codes seulement) ; la spec 5.4 interdit d'y écrire les
listes de membres. ASP.NET Core n'a pas de fournisseur de journaux en fichier. La solution
courante, Serilog, passe par `Serilog.Sinks.File`, dont la version 7.0.0 a été publiée le
28/04/2025, plus de 12 mois avant le 23/10/2026 : elle ne satisfait pas la règle de
maintenance de la spec 0.2.

## Décision

- Un fournisseur `ILoggerProvider` en fichier est écrit dans `Topogenia.Host` : un fichier par
  jour, effacement des fichiers de plus de 30 jours, écriture sérialisée.
- Les messages ne portent que des identifiants, des codes et des catégories ; aucun libellé,
  contenu de fiche ni liste de membres.
- Serilog est écarté.

## Conséquences

- Aucune dépendance tierce à suivre pour la journalisation ; le code reste court et sous les
  limites de taille.
- Le fournisseur se branche sur `Microsoft.Extensions.Logging` : le reste du code journalise
  par `ILogger` sans connaître la destination.

## Vérification

- Tests du fournisseur dans `tests/Topogenia.Host.Tests/` (phase 4, P4-T05) : rotation
  quotidienne, purge à 30 jours, absence de contenu métier.
- Porte de contrôle Sécurité (spec 5.2, 5.4).
