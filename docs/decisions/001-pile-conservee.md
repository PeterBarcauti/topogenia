<!-- 001-pile-conservee.md -->
<!-- D01 : la pile technique de la 1.x est conservée pour la 2.0 -->

# 001 - Pile technique conservée

Statut : acceptée (date 23/10/2026)

## Contexte

La 1.x tourne sur .NET (ASP.NET Core) et React + TypeScript + Vite, avec @xyflow/react,
zustand et i18next. Elle fonctionne et elle est bien testée (334 tests API, 176 tests
d'interface, 49 parcours). Ses défauts tiennent à l'architecture (spec 1.2), pas aux
technologies. L'équipe interne pratique .NET et React.

## Décision

La 2.0 garde la pile (spec D01) : .NET 10 (ASP.NET Core), React 19 + TypeScript + Vite,
@xyflow/react 12, zustand, i18next. Les versions sont figées dans `Directory.Packages.props`
et `web/package.json` (versions exactes, `package-lock.json`) ; `global.json` fixe le SDK
10.0.402 avec montée de correctif seulement (`latestPatch`). @xyflow/react, zustand et
i18next entrent dans `web/package.json` en phase 6, quand le code qui s'en sert arrive.
TypeScript est en 6.0.3 et non en 7 : `typescript-eslint` ne prend pas encore en charge
TypeScript 7, et la spec exige ESLint sans erreur (NF10).

## Conséquences

- Les tests 1.x se portent par intention, sans changer de cadre de pensée.
- Pas de courbe d'apprentissage pour l'équipe (une développeuse React, une équipe .NET).
- Toute dépendance ajoutée à cette pile suit la règle de la spec 0.2 (licence permissive,
  publication dans les 12 derniers mois, `THIRD-PARTY-NOTICES.md`, SBOM).

## Vérification

- `global.json`, `Directory.Packages.props` et `web/package.json` relus par la porte de
  contrôle Architecture.
- La construction (`dotnet build`, 0 avertissement) et, côté interface, `typecheck`, `lint`,
  `test` et `build` sont des étapes de l'intégration continue (`.github/workflows/ci.yml`).
