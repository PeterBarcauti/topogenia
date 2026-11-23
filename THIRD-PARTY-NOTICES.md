<!-- THIRD-PARTY-NOTICES.md -->
<!-- Third-party components, direct and transitive, with version, licence and whether they ship (generated) -->

# Composants tiers

Généré par `node build/third-party-notices.mjs` (`build/components.mjs`) depuis les graphes de restauration NuGet,
`web/package-lock.json` et la liste des paquets que l'interface construite sert (`web/served-packages.json`, écrite
par `npm run build`), dépendances transitives comprises. Colonne « Usage » : `livré` (dans les paquets), `tests` ou
`développement` (jamais livrés), `non servi` (dépendance de production dont le navigateur ne reçoit pas le code : la
partie Node d'exceljs, servi par son paquet navigateur, ADR 065). Les fichiers déjà assemblés que l'interface sert
tels quels embarquent d'autres paquets : ceux de leur carte des sources (`exceljs.min.js`), et les dépendances d'un
fichier assemblé sans carte qu'ils contiennent (`jszip.min.js`) ; leur version est « dans exceljs », celle de sa
construction et non celle du verrouillage ; leur licence est celle du verrouillage pour un paquet du même nom, sinon
celle de leur `package.json` embarqué, sinon « relevée » à la construction (à confirmer en ligne à la recette).
Vite retire les bannières de licence des morceaux servis : ce fichier porte l'attribution.
Règle : licence permissive (spec 0.2) pour tout composant livré, vérifiée par `ThirdPartyNoticesTests`, qui vérifie
aussi que ce fichier est celui que donne le générateur (`--check`) ; les exceptions sont justifiées dans
`docs/decisions/` (ADR 020 axe-core, ADR 024 composants natifs de SQL Server).

| Écosystème | Composant | Version | Licence | Usage |
|---|---|---|---|---|
| NuGet | `Microsoft.EntityFrameworkCore.Analyzers` | 10.0.13 | MIT | développement |
| NuGet | `Anthropic` | 13.3.0 | Apache-2.0 | livré |
| NuGet | `AWSSDK.BedrockRuntime` | 4.0.101.14 | Apache-2.0 | livré |
| NuGet | `AWSSDK.Core` | 4.0.102.15 | Apache-2.0 | livré |
| NuGet | `AWSSDK.SSO` | 4.0.100.23 | Apache-2.0 | livré |
| NuGet | `AWSSDK.SSOOIDC` | 4.0.100.22 | Apache-2.0 | livré |
| NuGet | `Microsoft.AspNetCore.Authentication.Negotiate` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.AspNetCore.OpenApi` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.Bcl.Cryptography` | 9.0.18 | MIT | livré |
| NuGet | `Microsoft.Data.SqlClient` | 7.1.1 | MIT | livré |
| NuGet | `Microsoft.Data.SqlClient.Extensions.Abstractions` | 7.1.1 | MIT | livré |
| NuGet | `Microsoft.Data.SqlClient.Internal.Logging` | 7.1.1 | MIT | livré |
| NuGet | `Microsoft.Data.SqlClient.SNI.runtime` | 7.1.0 | file: LICENSE.txt | livré |
| NuGet | `Microsoft.Data.Sqlite.Core` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore.Abstractions` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore.Relational` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore.Sqlite.Core` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.Extensions.AI.Abstractions` | 10.5.1 | MIT | livré |
| NuGet | `Microsoft.Extensions.DependencyModel` | 10.0.13 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.Abstractions` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.JsonWebTokens` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.Logging` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.Protocols` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.Protocols.OpenIdConnect` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.IdentityModel.Tokens` | 8.16.0 | MIT | livré |
| NuGet | `Microsoft.OpenApi` | 2.12.2 | MIT | livré |
| NuGet | `Microsoft.SqlServer.Server` | 1.0.0 | MIT | livré |
| NuGet | `SQLitePCLRaw.bundle_e_sqlite3` | 2.1.13 | Apache-2.0 | livré |
| NuGet | `SQLitePCLRaw.core` | 2.1.13 | Apache-2.0 | livré |
| NuGet | `SQLitePCLRaw.lib.e_sqlite3` | 2.1.13 | Apache-2.0 | livré |
| NuGet | `SQLitePCLRaw.provider.e_sqlite3` | 2.1.13 | Apache-2.0 | livré |
| NuGet | `System.Configuration.ConfigurationManager` | 10.0.13 | MIT | livré |
| NuGet | `System.DirectoryServices` | 10.0.13 | MIT | livré |
| NuGet | `System.DirectoryServices.AccountManagement` | 10.0.13 | MIT | livré |
| NuGet | `System.DirectoryServices.Protocols` | 10.0.13 | MIT | livré |
| NuGet | `System.IdentityModel.Tokens.Jwt` | 8.16.0 | MIT | livré |
| NuGet | `System.Security.Cryptography.Pkcs` | 10.0.13 | MIT | livré |
| NuGet | `System.Security.Cryptography.ProtectedData` | 10.0.13 | MIT | livré |
| NuGet | `CycleDetection` | 2.0.0 | MIT | tests |
| NuGet | `JetBrains.Annotations` | 2026.2.0 | MIT | tests |
| NuGet | `Microsoft.ApplicationInsights` | 2.23.0 | MIT | tests |
| NuGet | `Microsoft.AspNetCore.Connections.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.AspNetCore.TestHost` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Bcl.AsyncInterfaces` | 6.0.0 | MIT | tests |
| NuGet | `Microsoft.CodeAnalysis.Analyzers` | 5.9.0-1.26328.17 | MIT | tests |
| NuGet | `Microsoft.CodeAnalysis.Common` | 5.9.0 | MIT | tests |
| NuGet | `Microsoft.CodeAnalysis.CSharp` | 5.9.0 | MIT | tests |
| NuGet | `Microsoft.Extensions.Caching.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Caching.Memory` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.Binder` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.CommandLine` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.FileExtensions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.Json` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.DependencyInjection` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Diagnostics` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Features` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.FileProviders.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.FileProviders.Physical` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.FileSystemGlobbing` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Hosting` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Hosting.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.Abstractions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.Configuration` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.Console` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.Debug` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.EventLog` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Logging.EventSource` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Options` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Extensions.Primitives` | 10.0.13 | MIT | tests |
| NuGet | `Microsoft.Testing.Extensions.Telemetry` | 2.5.1 | MIT | tests |
| NuGet | `Microsoft.Testing.Extensions.TrxReport.Abstractions` | 2.5.1 | MIT | tests |
| NuGet | `Microsoft.Testing.Platform` | 2.5.1 | MIT | tests |
| NuGet | `Microsoft.Testing.Platform.MSBuild` | 2.5.1 | MIT | tests |
| NuGet | `Microsoft.Win32.Registry` | 5.0.0 | MIT | tests |
| NuGet | `Mono.Cecil` | 0.11.6 | MIT | tests |
| NuGet | `Newtonsoft.Json` | 13.0.4 | MIT | tests |
| NuGet | `System.Configuration.ConfigurationManager` | 9.0.18 | MIT | tests |
| NuGet | `System.Diagnostics.EventLog` | 10.0.13 | MIT | tests |
| NuGet | `System.Diagnostics.EventLog` | 9.0.18 | MIT | tests |
| NuGet | `System.Security.AccessControl` | 6.0.1 | MIT | tests |
| NuGet | `System.Security.Cryptography.ProtectedData` | 9.0.18 | MIT | tests |
| NuGet | `System.Threading.RateLimiting` | 9.0.18 | MIT | tests |
| NuGet | `System.ValueTuple` | 4.6.2 | MIT | tests |
| NuGet | `TngTech.ArchUnitNET` | 0.13.4 | Apache-2.0 | tests |
| NuGet | `TngTech.ArchUnitNET.xUnitV3` | 0.13.4 | Apache-2.0 | tests |
| NuGet | `xunit.analyzers` | 2.2.0 | Apache-2.0 | tests |
| NuGet | `xunit.v3` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.assert` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.common` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.core.mtp-v2` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.extensibility.core` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.mtp-v2` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.runner.common` | 4.0.2 | Apache-2.0 | tests |
| NuGet | `xunit.v3.runner.inproc.console` | 4.0.2 | Apache-2.0 | tests |
| npm | `@adobe/css-tools` | 4.5.0 | MIT | développement |
| npm | `@antfu/install-pkg` | 2.1.0 | MIT | développement |
| npm | `@asamuzakjp/css-color` | 7.1.7 | MIT | développement |
| npm | `@asamuzakjp/dom-selector` | 10.0.4 | MIT | développement |
| npm | `@axe-core/playwright` | 4.13.0 | MPL-2.0 | développement |
| npm | `@babel/code-frame` | 7.29.7 | MIT | développement |
| npm | `@babel/compat-data` | 7.29.7 | MIT | développement |
| npm | `@babel/core` | 7.29.7 | MIT | développement |
| npm | `@babel/generator` | 7.29.8 | MIT | développement |
| npm | `@babel/helper-compilation-targets` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-globals` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-module-imports` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-module-transforms` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-string-parser` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-validator-identifier` | 7.29.7 | MIT | développement |
| npm | `@babel/helper-validator-option` | 7.29.7 | MIT | développement |
| npm | `@babel/helpers` | 7.29.10 | MIT | développement |
| npm | `@babel/parser` | 7.29.9 | MIT | développement |
| npm | `@babel/template` | 7.29.7 | MIT | développement |
| npm | `@babel/traverse` | 7.29.10 | MIT | développement |
| npm | `@babel/types` | 7.29.8 | MIT | développement |
| npm | `@braintree/sanitize-url` | 7.1.2 | MIT | développement |
| npm | `@bramus/specificity` | 2.4.3 | MIT | développement |
| npm | `@cacheable/memory` | 2.2.0 | MIT | développement |
| npm | `@cacheable/utils` | 2.5.0 | MIT | développement |
| npm | `@chevrotain/types` | 11.1.2 | Apache-2.0 | développement |
| npm | `@csstools/color-helpers` | 6.1.2 | MIT-0 | développement |
| npm | `@csstools/css-calc` | 3.4.3 | MIT | développement |
| npm | `@csstools/css-color-parser` | 4.2.6 | MIT | développement |
| npm | `@csstools/css-parser-algorithms` | 4.0.2 | MIT | développement |
| npm | `@csstools/css-syntax-patches-for-csstree` | 1.1.15 | MIT-0 | développement |
| npm | `@csstools/css-tokenizer` | 4.0.2 | MIT | développement |
| npm | `@eslint-community/eslint-utils` | 4.10.1 | MIT | développement |
| npm | `@eslint-community/regexpp` | 4.12.2 | MIT | développement |
| npm | `@eslint/config-array` | 0.23.5 | Apache-2.0 | développement |
| npm | `@eslint/config-helpers` | 0.7.0 | Apache-2.0 | développement |
| npm | `@eslint/core` | 1.2.1 | Apache-2.0 | développement |
| npm | `@eslint/js` | 10.0.1 | MIT | développement |
| npm | `@eslint/object-schema` | 3.0.5 | Apache-2.0 | développement |
| npm | `@eslint/plugin-kit` | 0.7.3 | Apache-2.0 | développement |
| npm | `@exodus/bytes` | 1.16.0 | MIT | développement |
| npm | `@humanfs/core` | 0.19.2 | Apache-2.0 | développement |
| npm | `@humanfs/node` | 0.16.8 | Apache-2.0 | développement |
| npm | `@humanfs/types` | 0.15.0 | Apache-2.0 | développement |
| npm | `@humanwhocodes/module-importer` | 1.0.1 | Apache-2.0 | développement |
| npm | `@humanwhocodes/retry` | 0.4.3 | Apache-2.0 | développement |
| npm | `@iconify/types` | 2.0.0 | MIT | développement |
| npm | `@iconify/utils` | 3.1.7 | MIT | développement |
| npm | `@jridgewell/gen-mapping` | 0.3.13 | MIT | développement |
| npm | `@jridgewell/remapping` | 2.3.5 | MIT | développement |
| npm | `@jridgewell/resolve-uri` | 3.1.2 | MIT | développement |
| npm | `@jridgewell/sourcemap-codec` | 1.6.0 | MIT | développement |
| npm | `@jridgewell/trace-mapping` | 0.3.31 | MIT | développement |
| npm | `@keyv/bigmap` | 1.3.1 | MIT | développement |
| npm | `@keyv/serialize` | 1.1.1 | MIT | développement |
| npm | `@mermaid-js/parser` | 1.2.1 | MIT | développement |
| npm | `@oxc-project/types` | 0.155.0 | MIT | développement |
| npm | `@playwright/test` | 1.64.1 | Apache-2.0 | développement |
| npm | `@redocly/ajv` | 8.11.2 | MIT | développement |
| npm | `@redocly/config` | 0.22.0 | MIT | développement |
| npm | `@redocly/openapi-core` | 1.34.22 | MIT | développement |
| npm | `@rolldown/binding-android-arm-eabi` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-android-arm64` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-darwin-arm64` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-darwin-x64` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-freebsd-x64` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-arm-gnueabihf` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-arm64-gnu` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-arm64-musl` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-ppc64-gnu` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-s390x-gnu` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-x64-gnu` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-linux-x64-musl` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-openharmony-arm64` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-win32-arm64-msvc` | 1.2.15 | MIT | développement |
| npm | `@rolldown/binding-win32-x64-msvc` | 1.2.15 | MIT | développement |
| npm | `@rolldown/pluginutils` | 1.0.1 | MIT | développement |
| npm | `@testing-library/dom` | 10.4.2 | MIT | développement |
| npm | `@testing-library/jest-dom` | 7.0.1 | MIT | développement |
| npm | `@testing-library/react` | 16.3.3 | MIT | développement |
| npm | `@testing-library/user-event` | 14.6.8 | MIT | développement |
| npm | `@types/aria-query` | 5.0.4 | MIT | développement |
| npm | `@types/chai` | 5.2.3 | MIT | développement |
| npm | `@types/d3` | 7.4.3 | MIT | développement |
| npm | `@types/d3-array` | 3.2.2 | MIT | développement |
| npm | `@types/d3-axis` | 3.0.6 | MIT | développement |
| npm | `@types/d3-brush` | 3.0.6 | MIT | développement |
| npm | `@types/d3-chord` | 3.0.6 | MIT | développement |
| npm | `@types/d3-contour` | 3.0.6 | MIT | développement |
| npm | `@types/d3-delaunay` | 6.0.4 | MIT | développement |
| npm | `@types/d3-dispatch` | 3.0.7 | MIT | développement |
| npm | `@types/d3-dsv` | 3.0.7 | MIT | développement |
| npm | `@types/d3-ease` | 3.0.2 | MIT | développement |
| npm | `@types/d3-fetch` | 3.0.7 | MIT | développement |
| npm | `@types/d3-force` | 3.0.10 | MIT | développement |
| npm | `@types/d3-format` | 3.0.4 | MIT | développement |
| npm | `@types/d3-geo` | 3.1.1 | MIT | développement |
| npm | `@types/d3-hierarchy` | 3.1.7 | MIT | développement |
| npm | `@types/d3-path` | 3.1.1 | MIT | développement |
| npm | `@types/d3-polygon` | 3.0.2 | MIT | développement |
| npm | `@types/d3-quadtree` | 3.0.6 | MIT | développement |
| npm | `@types/d3-random` | 3.0.4 | MIT | développement |
| npm | `@types/d3-scale` | 4.0.9 | MIT | développement |
| npm | `@types/d3-scale-chromatic` | 3.1.0 | MIT | développement |
| npm | `@types/d3-shape` | 3.2.0 | MIT | développement |
| npm | `@types/d3-time` | 3.0.4 | MIT | développement |
| npm | `@types/d3-time-format` | 4.0.3 | MIT | développement |
| npm | `@types/d3-timer` | 3.0.2 | MIT | développement |
| npm | `@types/deep-eql` | 4.0.2 | MIT | développement |
| npm | `@types/esrecurse` | 4.3.1 | MIT | développement |
| npm | `@types/estree` | 1.0.9 | MIT | développement |
| npm | `@types/geojson` | 7946.0.16 | MIT | développement |
| npm | `@types/json-schema` | 7.0.15 | MIT | développement |
| npm | `@types/node` | 26.6.5 | MIT | développement |
| npm | `@types/react` | 19.3.0 | MIT | développement |
| npm | `@types/react-dom` | 19.3.0 | MIT | développement |
| npm | `@types/trusted-types` | 2.0.7 | MIT | développement |
| npm | `@typescript-eslint/eslint-plugin` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/parser` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/project-service` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/scope-manager` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/tsconfig-utils` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/type-utils` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/types` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/typescript-estree` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/utils` | 8.72.0 | MIT | développement |
| npm | `@typescript-eslint/visitor-keys` | 8.72.0 | MIT | développement |
| npm | `@upsetjs/venn.js` | 2.0.0 | MIT | développement |
| npm | `@vitejs/plugin-react` | 6.1.2 | MIT | développement |
| npm | `@vitest/mocker` | 5.0.5 | MIT | développement |
| npm | `@vitest/spy` | 5.0.5 | MIT | développement |
| npm | `acorn` | 8.19.0 | MIT | développement |
| npm | `acorn-jsx` | 5.3.2 | MIT | développement |
| npm | `agent-base` | 7.1.4 | MIT | développement |
| npm | `ajv` | 6.15.0 | MIT | développement |
| npm | `ajv` | 8.20.0 | MIT | développement |
| npm | `ansi-colors` | 4.1.3 | MIT | développement |
| npm | `ansi-regex` | 5.0.1 | MIT | développement |
| npm | `ansi-styles` | 5.2.0 | MIT | développement |
| npm | `argparse` | 2.0.1 | Python-2.0 | développement |
| npm | `aria-query` | 5.3.0 | Apache-2.0 | développement |
| npm | `assertion-error` | 2.0.1 | MIT | développement |
| npm | `axe-core` | 4.13.0 | MPL-2.0 | développement |
| npm | `balanced-match` | 1.0.2 | MIT | développement |
| npm | `balanced-match` | 4.0.4 | MIT | développement |
| npm | `baseline-browser-mapping` | 2.11.31 | Apache-2.0 | développement |
| npm | `bidi-js` | 1.1.0 | MIT | développement |
| npm | `bpmn-moddle` | 10.3.1 | MIT | développement |
| npm | `brace-expansion` | 2.1.7 | MIT | développement |
| npm | `brace-expansion` | 5.0.12 | MIT | développement |
| npm | `browserslist` | 4.29.4 | MIT | développement |
| npm | `cacheable` | 2.5.0 | MIT | développement |
| npm | `caniuse-lite` | 1.0.30001819 | CC-BY-4.0 | développement |
| npm | `chai` | 6.3.0 | MIT | développement |
| npm | `change-case` | 5.4.4 | MIT | développement |
| npm | `colorette` | 1.4.0 | MIT | développement |
| npm | `commander` | 7.2.0 | MIT | développement |
| npm | `commander` | 8.3.0 | MIT | développement |
| npm | `convert-source-map` | 2.0.0 | MIT | développement |
| npm | `cose-base` | 1.0.3 | MIT | développement |
| npm | `cose-base` | 2.2.0 | MIT | développement |
| npm | `cross-spawn` | 7.0.6 | MIT | développement |
| npm | `css-tree` | 3.2.1 | MIT | développement |
| npm | `css.escape` | 1.5.1 | MIT | développement |
| npm | `csstype` | 3.2.3 | MIT | développement |
| npm | `cytoscape` | 3.34.4 | MIT | développement |
| npm | `cytoscape-cose-bilkent` | 4.1.0 | MIT | développement |
| npm | `cytoscape-fcose` | 2.2.0 | MIT | développement |
| npm | `d3` | 7.9.2 | ISC | développement |
| npm | `d3-array` | 2.12.1 | BSD-3-Clause | développement |
| npm | `d3-array` | 3.2.5 | ISC | développement |
| npm | `d3-axis` | 3.0.1 | ISC | développement |
| npm | `d3-brush` | 3.0.1 | ISC | développement |
| npm | `d3-chord` | 3.0.2 | ISC | développement |
| npm | `d3-contour` | 4.0.3 | ISC | développement |
| npm | `d3-delaunay` | 6.0.6 | ISC | développement |
| npm | `d3-dsv` | 3.0.2 | ISC | développement |
| npm | `d3-fetch` | 3.0.2 | ISC | développement |
| npm | `d3-force` | 3.0.1 | ISC | développement |
| npm | `d3-format` | 3.1.3 | ISC | développement |
| npm | `d3-geo` | 3.1.2 | ISC | développement |
| npm | `d3-hierarchy` | 3.1.3 | ISC | développement |
| npm | `d3-path` | 1.0.9 | BSD-3-Clause | développement |
| npm | `d3-path` | 3.1.1 | ISC | développement |
| npm | `d3-polygon` | 3.0.2 | ISC | développement |
| npm | `d3-quadtree` | 3.0.2 | ISC | développement |
| npm | `d3-random` | 3.0.2 | ISC | développement |
| npm | `d3-sankey` | 0.12.3 | BSD-3-Clause | développement |
| npm | `d3-scale` | 4.0.3 | ISC | développement |
| npm | `d3-scale-chromatic` | 3.1.1 | ISC | développement |
| npm | `d3-shape` | 1.3.7 | BSD-3-Clause | développement |
| npm | `d3-shape` | 3.2.1 | ISC | développement |
| npm | `d3-time` | 3.1.1 | ISC | développement |
| npm | `d3-time-format` | 4.1.1 | ISC | développement |
| npm | `dagre-d3-es` | 7.0.14 | MIT | développement |
| npm | `data-urls` | 8.0.0 | MIT | développement |
| npm | `debug` | 4.4.3 | MIT | développement |
| npm | `decimal.js` | 10.6.0 | MIT | développement |
| npm | `deep-is` | 0.1.4 | MIT | développement |
| npm | `delaunator` | 5.1.0 | ISC | développement |
| npm | `dequal` | 2.0.3 | MIT | développement |
| npm | `detect-libc` | 2.1.2 | Apache-2.0 | développement |
| npm | `dom-accessibility-api` | 0.5.16 | MIT | développement |
| npm | `dom-accessibility-api` | 0.6.3 | MIT | développement |
| npm | `electron-to-chromium` | 1.5.463 | ISC | développement |
| npm | `entities` | 8.1.0 | BSD-2-Clause | développement |
| npm | `es-module-lexer` | 2.3.2 | MIT | développement |
| npm | `es-toolkit` | 1.53.1 | MIT | développement |
| npm | `escalade` | 3.2.0 | MIT | développement |
| npm | `escape-string-regexp` | 4.0.0 | MIT | développement |
| npm | `eslint` | 10.13.0 | MIT | développement |
| npm | `eslint-plugin-react-hooks` | 7.1.1 | MIT | développement |
| npm | `eslint-scope` | 9.1.2 | BSD-2-Clause | développement |
| npm | `eslint-visitor-keys` | 3.4.3 | Apache-2.0 | développement |
| npm | `eslint-visitor-keys` | 5.0.1 | Apache-2.0 | développement |
| npm | `espree` | 11.2.0 | BSD-2-Clause | développement |
| npm | `esquery` | 1.7.0 | BSD-3-Clause | développement |
| npm | `esrecurse` | 4.3.0 | BSD-2-Clause | développement |
| npm | `estraverse` | 5.3.0 | BSD-2-Clause | développement |
| npm | `estree-walker` | 3.0.3 | MIT | développement |
| npm | `esutils` | 2.0.3 | BSD-2-Clause | développement |
| npm | `expect-type` | 1.4.0 | Apache-2.0 | développement |
| npm | `fast-deep-equal` | 3.1.3 | MIT | développement |
| npm | `fast-json-stable-stringify` | 2.1.0 | MIT | développement |
| npm | `fast-levenshtein` | 2.0.6 | MIT | développement |
| npm | `fast-uri` | 3.1.9 | BSD-3-Clause | développement |
| npm | `fastdom` | 1.0.12 | MIT | développement |
| npm | `fdir` | 6.5.0 | MIT | développement |
| npm | `file-entry-cache` | 11.1.5 | MIT | développement |
| npm | `find-up` | 5.0.0 | MIT | développement |
| npm | `flat-cache` | 6.1.23 | MIT | développement |
| npm | `flatted` | 3.4.4 | ISC | développement |
| npm | `fsevents` | 2.3.3 | MIT | développement |
| npm | `gensync` | 1.0.0-beta.2 | MIT | développement |
| npm | `glob-parent` | 6.0.2 | ISC | développement |
| npm | `globals` | 17.13.0 | MIT | développement |
| npm | `hachure-fill` | 0.5.2 | MIT | développement |
| npm | `hashery` | 1.5.1 | MIT | développement |
| npm | `hermes-estree` | 0.25.1 | MIT | développement |
| npm | `hermes-parser` | 0.25.1 | MIT | développement |
| npm | `hookified` | 1.15.1 | MIT | développement |
| npm | `hookified` | 2.2.0 | MIT | développement |
| npm | `html-encoding-sniffer` | 7.0.0 | MIT | développement |
| npm | `https-proxy-agent` | 7.0.6 | MIT | développement |
| npm | `iconv-lite` | 0.6.3 | MIT | développement |
| npm | `ignore` | 5.3.2 | MIT | développement |
| npm | `ignore` | 7.0.13 | MIT | développement |
| npm | `import-meta-resolve` | 4.2.0 | MIT | développement |
| npm | `imurmurhash` | 0.1.4 | MIT | développement |
| npm | `indent-string` | 4.0.0 | MIT | développement |
| npm | `index-to-position` | 1.2.0 | MIT | développement |
| npm | `internmap` | 1.0.1 | ISC | développement |
| npm | `internmap` | 2.0.3 | ISC | développement |
| npm | `is-extglob` | 2.1.1 | MIT | développement |
| npm | `is-glob` | 4.0.3 | MIT | développement |
| npm | `is-potential-custom-element-name` | 1.0.1 | MIT | développement |
| npm | `isexe` | 2.0.0 | ISC | développement |
| npm | `js-levenshtein` | 1.1.6 | MIT | développement |
| npm | `js-tokens` | 4.0.0 | MIT | développement |
| npm | `js-yaml` | 4.3.2 | MIT | développement |
| npm | `jsdom` | 30.1.3 | MIT | développement |
| npm | `jsesc` | 3.1.0 | MIT | développement |
| npm | `json-schema-traverse` | 0.4.1 | MIT | développement |
| npm | `json-schema-traverse` | 1.0.0 | MIT | développement |
| npm | `json-stable-stringify-without-jsonify` | 1.0.1 | MIT | développement |
| npm | `json5` | 2.2.3 | MIT | développement |
| npm | `katex` | 0.16.47 | MIT | développement |
| npm | `keyv` | 5.6.0 | MIT | développement |
| npm | `khroma` | 2.1.0 | unknown | développement |
| npm | `layout-base` | 1.0.2 | MIT | développement |
| npm | `layout-base` | 2.0.1 | MIT | développement |
| npm | `levn` | 0.4.1 | MIT | développement |
| npm | `lightningcss` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-android-arm64` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-darwin-arm64` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-darwin-x64` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-freebsd-x64` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-linux-arm-gnueabihf` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-linux-arm64-gnu` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-linux-arm64-musl` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-linux-x64-gnu` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-linux-x64-musl` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-win32-arm64-msvc` | 1.33.0 | MPL-2.0 | développement |
| npm | `lightningcss-win32-x64-msvc` | 1.33.0 | MPL-2.0 | développement |
| npm | `locate-path` | 6.0.0 | MIT | développement |
| npm | `lodash-es` | 4.18.1 | MIT | développement |
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 5.1.1 | ISC | développement |
| npm | `lz-string` | 1.5.0 | MIT | développement |
| npm | `magic-string` | 1.4.3 | MIT | développement |
| npm | `marked` | 16.4.2 | MIT | développement |
| npm | `mdn-data` | 2.27.1 | CC0-1.0 | développement |
| npm | `mermaid` | 11.17.2 | MIT | développement |
| npm | `min-dash` | 5.1.0 | MIT | développement |
| npm | `min-indent` | 1.0.1 | MIT | développement |
| npm | `minimatch` | 10.2.6 | BlueOak-1.0.0 | développement |
| npm | `minimatch` | 5.1.9 | ISC | développement |
| npm | `moddle` | 8.2.1 | MIT | développement |
| npm | `moddle-xml` | 12.3.2 | MIT | développement |
| npm | `ms` | 2.1.3 | MIT | développement |
| npm | `nanoid` | 3.3.20 | MIT | développement |
| npm | `natural-compare` | 1.4.0 | MIT | développement |
| npm | `node-releases` | 2.0.60 | MIT | développement |
| npm | `obug` | 3.0.0 | MIT | développement |
| npm | `openapi-typescript` | 7.13.0 | MIT | développement |
| npm | `optionator` | 0.9.4 | MIT | développement |
| npm | `p-limit` | 3.1.0 | MIT | développement |
| npm | `p-locate` | 5.0.0 | MIT | développement |
| npm | `package-manager-detector` | 1.9.0 | MIT | développement |
| npm | `parse-json` | 8.3.0 | MIT | développement |
| npm | `parse5` | 8.0.1 | MIT | développement |
| npm | `path-data-parser` | 0.1.0 | MIT | développement |
| npm | `path-exists` | 4.0.0 | MIT | développement |
| npm | `path-key` | 3.1.1 | MIT | développement |
| npm | `picocolors` | 1.1.1 | ISC | développement |
| npm | `picomatch` | 4.0.7 | MIT | développement |
| npm | `playwright` | 1.64.1 | Apache-2.0 | développement |
| npm | `playwright-core` | 1.64.1 | Apache-2.0 | développement |
| npm | `pluralize` | 8.0.0 | MIT | développement |
| npm | `points-on-curve` | 0.2.0 | MIT | développement |
| npm | `points-on-path` | 0.2.1 | MIT | développement |
| npm | `postcss` | 8.5.31 | MIT | développement |
| npm | `prelude-ls` | 1.2.1 | MIT | développement |
| npm | `pretty-format` | 27.5.1 | MIT | développement |
| npm | `punycode` | 2.3.1 | MIT | développement |
| npm | `qified` | 0.10.1 | MIT | développement |
| npm | `react-is` | 17.0.2 | MIT | développement |
| npm | `redent` | 3.0.0 | MIT | développement |
| npm | `require-from-string` | 2.0.2 | MIT | développement |
| npm | `robust-predicates` | 3.0.3 | Unlicense | développement |
| npm | `rolldown` | 1.2.15 | MIT | développement |
| npm | `roughjs` | 4.6.6 | MIT | développement |
| npm | `rw` | 1.3.3 | BSD-3-Clause | développement |
| npm | `safer-buffer` | 2.1.2 | MIT | développement |
| npm | `saxen` | 11.2.0 | MIT | développement |
| npm | `saxes` | 6.0.0 | ISC | développement |
| npm | `semver` | 6.3.1 | ISC | développement |
| npm | `semver` | 7.8.5 | ISC | développement |
| npm | `shebang-command` | 2.0.0 | MIT | développement |
| npm | `shebang-regex` | 3.0.0 | MIT | développement |
| npm | `source-map-js` | 1.2.2 | BSD-3-Clause | développement |
| npm | `std-env` | 4.3.0 | MIT | développement |
| npm | `strictdom` | 1.0.1 | MIT | développement |
| npm | `strip-indent` | 3.0.0 | MIT | développement |
| npm | `stylis` | 4.4.0 | MIT | développement |
| npm | `supports-color` | 10.2.2 | MIT | développement |
| npm | `tinybench` | 6.2.2 | MIT | développement |
| npm | `tinyexec` | 1.3.1 | MIT | développement |
| npm | `tinyglobby` | 0.2.17 | MIT | développement |
| npm | `tldts` | 7.4.19 | MIT | développement |
| npm | `tldts-core` | 7.4.19 | MIT | développement |
| npm | `tough-cookie` | 6.0.2 | BSD-3-Clause | développement |
| npm | `tr46` | 7.0.0 | MIT | développement |
| npm | `ts-api-utils` | 2.5.0 | MIT | développement |
| npm | `ts-dedent` | 2.3.0 | MIT | développement |
| npm | `type-check` | 0.4.0 | MIT | développement |
| npm | `type-fest` | 4.41.0 | (MIT OR CC0-1.0) | développement |
| npm | `typescript` | 6.0.3 | Apache-2.0 | développement |
| npm | `typescript-eslint` | 8.72.0 | MIT | développement |
| npm | `undici` | 8.12.3 | MIT | développement |
| npm | `undici-types` | 8.9.0 | MIT | développement |
| npm | `update-browserslist-db` | 1.3.4 | MIT | développement |
| npm | `uri-js` | 4.4.1 | BSD-2-Clause | développement |
| npm | `uri-js-replace` | 1.0.2 | MIT | développement |
| npm | `vite` | 8.3.5 | MIT | développement |
| npm | `vitest` | 5.0.5 | MIT | développement |
| npm | `w3c-xmlserializer` | 6.0.0 | MIT | développement |
| npm | `webidl-conversions` | 8.0.1 | BSD-2-Clause | développement |
| npm | `whatwg-mimetype` | 5.0.0 | MIT | développement |
| npm | `whatwg-url` | 17.3.0 | MIT | développement |
| npm | `which` | 2.0.2 | ISC | développement |
| npm | `why-is-node-running` | 3.2.1 | MIT | développement |
| npm | `word-wrap` | 1.2.5 | MIT | développement |
| npm | `xml-name-validator` | 5.0.0 | Apache-2.0 | développement |
| npm | `xmllint-wasm` | 5.3.0 | MIT | développement |
| npm | `yallist` | 3.1.1 | ISC | développement |
| npm | `yaml-ast-parser` | 0.0.43 | Apache-2.0 | développement |
| npm | `yargs-parser` | 21.1.1 | ISC | développement |
| npm | `yocto-queue` | 0.1.0 | MIT | développement |
| npm | `zod` | 4.6.7 | MIT | développement |
| npm | `zod-validation-error` | 4.0.2 | MIT | développement |
| npm | `@babel/runtime` | 7.29.10 | MIT | livré |
| npm | `@fontsource-variable/inter` | 5.3.0 | OFL-1.1 | livré |
| npm | `@xyflow/react` | 12.12.0 | MIT | livré |
| npm | `@xyflow/system` | 0.0.83 | MIT | livré |
| npm | `canvg` | 3.0.11 | MIT | livré |
| npm | `classcat` | 5.0.5 | MIT | livré |
| npm | `core-js` | 3.51.0 | MIT | livré |
| npm | `d3-color` | 3.1.1 | ISC | livré |
| npm | `d3-dispatch` | 3.0.2 | ISC | livré |
| npm | `d3-drag` | 3.0.1 | ISC | livré |
| npm | `d3-ease` | 3.0.2 | BSD-3-Clause | livré |
| npm | `d3-interpolate` | 3.0.2 | ISC | livré |
| npm | `d3-selection` | 3.0.1 | ISC | livré |
| npm | `d3-timer` | 3.0.2 | ISC | livré |
| npm | `d3-transition` | 3.0.2 | ISC | livré |
| npm | `d3-zoom` | 3.0.1 | ISC | livré |
| npm | `dompurify` | 3.4.17 | (MPL-2.0 OR Apache-2.0) | livré |
| npm | `exceljs` | 4.4.0 | MIT | livré |
| npm | `fast-png` | 6.4.0 | MIT | livré |
| npm | `fflate` | 0.8.3 | MIT | livré |
| npm | `html-to-image` | 1.11.11 | MIT | livré |
| npm | `html2canvas` | 1.4.1 | MIT | livré |
| npm | `i18next` | 26.4.3 | MIT | livré |
| npm | `inter-font` | 3.19.0 | OFL-1.1 | livré |
| npm | `iobuffer` | 5.4.0 | MIT | livré |
| npm | `jspdf` | 4.2.1 | MIT | livré |
| npm | `lucide-react` | 1.61.0 | ISC | livré |
| npm | `pako` | 2.2.0 | (MIT AND Zlib) | livré |
| npm | `performance-now` | 2.1.0 | MIT | livré |
| npm | `raf` | 3.4.1 | MIT | livré |
| npm | `react` | 19.3.1 | MIT | livré |
| npm | `react-dom` | 19.3.1 | MIT | livré |
| npm | `react-i18next` | 17.0.16 | MIT | livré |
| npm | `rgbcolor` | 1.0.1 | MIT OR SEE LICENSE IN FEEL-FREE.md | livré |
| npm | `scheduler` | 0.28.0 | MIT | livré |
| npm | `stackblur-canvas` | 2.7.0 | MIT | livré |
| npm | `svg-pathdata` | 6.0.3 | MIT | livré |
| npm | `use-sync-external-store` | 1.7.0 | MIT | livré |
| npm | `zustand` | 4.5.7 | MIT | livré |
| npm | `zustand` | 5.0.15 | MIT | livré |
| npm | `@fast-csv/format` | 4.3.5 | MIT | non servi |
| npm | `@fast-csv/parse` | 4.3.6 | MIT | non servi |
| npm | `@types/d3-color` | 3.1.3 | MIT | non servi |
| npm | `@types/d3-drag` | 3.0.7 | MIT | non servi |
| npm | `@types/d3-interpolate` | 3.0.4 | MIT | non servi |
| npm | `@types/d3-selection` | 3.0.12 | MIT | non servi |
| npm | `@types/d3-transition` | 3.0.9 | MIT | non servi |
| npm | `@types/d3-zoom` | 3.0.9 | MIT | non servi |
| npm | `@types/node` | 14.18.63 | MIT | non servi |
| npm | `@types/node` | 14.18.63 | MIT | non servi |
| npm | `@types/pako` | 2.0.4 | MIT | non servi |
| npm | `@types/raf` | 3.4.3 | MIT | non servi |
| npm | `archiver` | 5.3.2 | MIT | non servi |
| npm | `archiver-utils` | 2.1.0 | MIT | non servi |
| npm | `archiver-utils` | 3.0.4 | MIT | non servi |
| npm | `array-buffer-byte-length` | 1.0.2 | MIT | non servi |
| npm | `arraybuffer.prototype.slice` | 1.0.4 | MIT | non servi |
| npm | `async` | 3.2.6 | MIT | non servi |
| npm | `async-function` | 1.0.0 | MIT | non servi |
| npm | `available-typed-arrays` | 1.0.7 | MIT | non servi |
| npm | `balanced-match` | 1.0.2 | MIT | non servi |
| npm | `balanced-match` | 1.0.2 | MIT | non servi |
| npm | `base64-arraybuffer` | 1.0.2 | MIT | non servi |
| npm | `base64-js` | 1.5.1 | MIT | non servi |
| npm | `big-integer` | 1.6.52 | Unlicense | non servi |
| npm | `binary` | 0.3.0 | MIT | non servi |
| npm | `bl` | 4.1.0 | MIT | non servi |
| npm | `bluebird` | 3.4.7 | MIT | non servi |
| npm | `brace-expansion` | 1.1.21 | MIT | non servi |
| npm | `brace-expansion` | 2.1.7 | MIT | non servi |
| npm | `buffer` | 5.7.1 | MIT | non servi |
| npm | `buffer-crc32` | 0.2.13 | MIT | non servi |
| npm | `buffer-indexof-polyfill` | 1.0.2 | MIT | non servi |
| npm | `buffers` | 0.1.1 | unknown | non servi |
| npm | `call-bind` | 1.0.9 | MIT | non servi |
| npm | `call-bind-apply-helpers` | 1.0.2 | MIT | non servi |
| npm | `call-bound` | 1.0.4 | MIT | non servi |
| npm | `chainsaw` | 0.1.0 | MIT/X11 | non servi |
| npm | `compress-commons` | 4.1.2 | MIT | non servi |
| npm | `concat-map` | 0.0.1 | MIT | non servi |
| npm | `core-util-is` | 1.0.3 | MIT | non servi |
| npm | `crc-32` | 1.2.2 | Apache-2.0 | non servi |
| npm | `crc32-stream` | 4.0.3 | MIT | non servi |
| npm | `css-line-break` | 2.1.0 | MIT | non servi |
| npm | `data-view-buffer` | 1.0.2 | MIT | non servi |
| npm | `data-view-byte-length` | 1.0.2 | MIT | non servi |
| npm | `data-view-byte-offset` | 1.0.1 | MIT | non servi |
| npm | `dayjs` | 1.11.23 | MIT | non servi |
| npm | `define-data-property` | 1.1.4 | MIT | non servi |
| npm | `define-properties` | 1.2.1 | MIT | non servi |
| npm | `dunder-proto` | 1.0.1 | MIT | non servi |
| npm | `duplexer2` | 0.1.4 | BSD-3-Clause | non servi |
| npm | `end-of-stream` | 1.4.5 | MIT | non servi |
| npm | `es-abstract` | 1.24.2 | MIT | non servi |
| npm | `es-abstract-get` | 1.0.0 | MIT | non servi |
| npm | `es-define-property` | 1.0.1 | MIT | non servi |
| npm | `es-errors` | 1.3.0 | MIT | non servi |
| npm | `es-object-atoms` | 1.1.2 | MIT | non servi |
| npm | `es-set-tostringtag` | 2.1.0 | MIT | non servi |
| npm | `es-to-primitive` | 1.3.4 | MIT | non servi |
| npm | `fast-csv` | 4.3.6 | MIT | non servi |
| npm | `for-each` | 0.3.5 | MIT | non servi |
| npm | `fs-constants` | 1.0.0 | MIT | non servi |
| npm | `fs.realpath` | 1.0.0 | ISC | non servi |
| npm | `fstream` | 1.0.12 | ISC | non servi |
| npm | `function-bind` | 1.1.2 | MIT | non servi |
| npm | `function.prototype.name` | 1.2.0 | MIT | non servi |
| npm | `functions-have-names` | 1.2.3 | MIT | non servi |
| npm | `generator-function` | 2.0.1 | MIT | non servi |
| npm | `get-intrinsic` | 1.3.0 | MIT | non servi |
| npm | `get-proto` | 1.0.1 | MIT | non servi |
| npm | `get-symbol-description` | 1.1.0 | MIT | non servi |
| npm | `glob` | 7.2.3 | ISC | non servi |
| npm | `globalthis` | 1.0.4 | MIT | non servi |
| npm | `gopd` | 1.2.0 | MIT | non servi |
| npm | `graceful-fs` | 4.2.11 | ISC | non servi |
| npm | `has-bigints` | 1.1.0 | MIT | non servi |
| npm | `has-property-descriptors` | 1.0.2 | MIT | non servi |
| npm | `has-proto` | 1.2.0 | MIT | non servi |
| npm | `has-symbols` | 1.1.0 | MIT | non servi |
| npm | `has-tostringtag` | 1.0.2 | MIT | non servi |
| npm | `hasown` | 2.0.4 | MIT | non servi |
| npm | `html-parse-stringify` | 4.0.1 | MIT | non servi |
| npm | `ieee754` | 1.2.1 | BSD-3-Clause | non servi |
| npm | `immediate` | 3.0.6 | MIT | non servi |
| npm | `inflight` | 1.0.6 | ISC | non servi |
| npm | `inherits` | 2.0.4 | ISC | non servi |
| npm | `internal-slot` | 1.1.0 | MIT | non servi |
| npm | `is-array-buffer` | 3.0.5 | MIT | non servi |
| npm | `is-async-function` | 2.1.1 | MIT | non servi |
| npm | `is-bigint` | 1.1.0 | MIT | non servi |
| npm | `is-boolean-object` | 1.2.2 | MIT | non servi |
| npm | `is-callable` | 1.2.7 | MIT | non servi |
| npm | `is-data-view` | 1.0.2 | MIT | non servi |
| npm | `is-date-object` | 1.1.0 | MIT | non servi |
| npm | `is-document.all` | 1.0.0 | MIT | non servi |
| npm | `is-finalizationregistry` | 1.1.1 | MIT | non servi |
| npm | `is-generator-function` | 1.1.2 | MIT | non servi |
| npm | `is-map` | 2.0.3 | MIT | non servi |
| npm | `is-negative-zero` | 2.0.3 | MIT | non servi |
| npm | `is-number-object` | 1.1.1 | MIT | non servi |
| npm | `is-regex` | 1.2.1 | MIT | non servi |
| npm | `is-set` | 2.0.3 | MIT | non servi |
| npm | `is-shared-array-buffer` | 1.0.4 | MIT | non servi |
| npm | `is-string` | 1.1.1 | MIT | non servi |
| npm | `is-symbol` | 1.1.1 | MIT | non servi |
| npm | `is-typed-array` | 1.1.15 | MIT | non servi |
| npm | `is-weakmap` | 2.0.2 | MIT | non servi |
| npm | `is-weakref` | 1.1.1 | MIT | non servi |
| npm | `is-weakset` | 2.0.4 | MIT | non servi |
| npm | `isarray` | 1.0.0 | MIT | non servi |
| npm | `isarray` | 1.0.0 | MIT | non servi |
| npm | `isarray` | 1.0.0 | MIT | non servi |
| npm | `isarray` | 1.0.0 | MIT | non servi |
| npm | `isarray` | 1.0.0 | MIT | non servi |
| npm | `isarray` | 2.0.5 | MIT | non servi |
| npm | `jszip` | 3.10.2 | (MIT OR GPL-3.0-or-later) | non servi |
| npm | `lazystream` | 1.0.1 | MIT | non servi |
| npm | `lie` | 3.3.0 | MIT | non servi |
| npm | `listenercount` | 1.0.1 | ISC | non servi |
| npm | `lodash.defaults` | 4.2.0 | MIT | non servi |
| npm | `lodash.difference` | 4.5.0 | MIT | non servi |
| npm | `lodash.escaperegexp` | 4.1.2 | MIT | non servi |
| npm | `lodash.flatten` | 4.4.0 | MIT | non servi |
| npm | `lodash.groupby` | 4.6.0 | MIT | non servi |
| npm | `lodash.isboolean` | 3.0.3 | MIT | non servi |
| npm | `lodash.isequal` | 4.5.0 | MIT | non servi |
| npm | `lodash.isfunction` | 3.0.9 | MIT | non servi |
| npm | `lodash.isnil` | 4.0.0 | MIT | non servi |
| npm | `lodash.isplainobject` | 4.0.6 | MIT | non servi |
| npm | `lodash.isundefined` | 3.0.1 | MIT | non servi |
| npm | `lodash.union` | 4.6.0 | MIT | non servi |
| npm | `lodash.uniq` | 4.5.0 | MIT | non servi |
| npm | `math-intrinsics` | 1.1.0 | MIT | non servi |
| npm | `minimatch` | 3.1.5 | ISC | non servi |
| npm | `minimatch` | 5.1.9 | ISC | non servi |
| npm | `minimist` | 1.2.8 | MIT | non servi |
| npm | `mkdirp` | 0.5.6 | MIT | non servi |
| npm | `normalize-path` | 3.0.0 | MIT | non servi |
| npm | `object-inspect` | 1.13.4 | MIT | non servi |
| npm | `object-keys` | 1.1.1 | MIT | non servi |
| npm | `object.assign` | 4.1.7 | MIT | non servi |
| npm | `once` | 1.4.0 | ISC | non servi |
| npm | `own-keys` | 1.0.2 | MIT | non servi |
| npm | `pako` | 1.0.11 | (MIT AND Zlib) | non servi |
| npm | `path-is-absolute` | 1.0.1 | MIT | non servi |
| npm | `possible-typed-array-names` | 1.1.0 | MIT | non servi |
| npm | `process-nextick-args` | 2.0.1 | MIT | non servi |
| npm | `readable-stream` | 2.3.8 | MIT | non servi |
| npm | `readable-stream` | 2.3.8 | MIT | non servi |
| npm | `readable-stream` | 2.3.8 | MIT | non servi |
| npm | `readable-stream` | 2.3.8 | MIT | non servi |
| npm | `readable-stream` | 2.3.8 | MIT | non servi |
| npm | `readable-stream` | 3.6.2 | MIT | non servi |
| npm | `readdir-glob` | 1.1.3 | Apache-2.0 | non servi |
| npm | `reflect.getprototypeof` | 1.0.10 | MIT | non servi |
| npm | `regenerator-runtime` | 0.13.11 | MIT | non servi |
| npm | `regexp.prototype.flags` | 1.5.4 | MIT | non servi |
| npm | `rimraf` | 2.7.1 | ISC | non servi |
| npm | `safe-array-concat` | 1.1.4 | MIT | non servi |
| npm | `safe-buffer` | 5.1.2 | MIT | non servi |
| npm | `safe-buffer` | 5.1.2 | MIT | non servi |
| npm | `safe-buffer` | 5.1.2 | MIT | non servi |
| npm | `safe-buffer` | 5.1.2 | MIT | non servi |
| npm | `safe-buffer` | 5.1.2 | MIT | non servi |
| npm | `safe-buffer` | 5.2.1 | MIT | non servi |
| npm | `safe-push-apply` | 1.0.0 | MIT | non servi |
| npm | `safe-regex-test` | 1.1.0 | MIT | non servi |
| npm | `saxes` | 5.0.1 | ISC | non servi |
| npm | `set-function-length` | 1.2.2 | MIT | non servi |
| npm | `set-function-name` | 2.0.2 | MIT | non servi |
| npm | `set-proto` | 1.0.0 | MIT | non servi |
| npm | `setimmediate` | 1.0.5 | MIT | non servi |
| npm | `side-channel` | 1.1.1 | MIT | non servi |
| npm | `side-channel-list` | 1.0.1 | MIT | non servi |
| npm | `side-channel-map` | 1.0.1 | MIT | non servi |
| npm | `side-channel-weakmap` | 1.0.2 | MIT | non servi |
| npm | `stop-iteration-iterator` | 1.1.0 | MIT | non servi |
| npm | `string_decoder` | 1.1.1 | MIT | non servi |
| npm | `string_decoder` | 1.1.1 | MIT | non servi |
| npm | `string_decoder` | 1.1.1 | MIT | non servi |
| npm | `string_decoder` | 1.1.1 | MIT | non servi |
| npm | `string_decoder` | 1.1.1 | MIT | non servi |
| npm | `string_decoder` | 1.3.0 | MIT | non servi |
| npm | `string.prototype.trim` | 1.2.11 | MIT | non servi |
| npm | `string.prototype.trimend` | 1.0.10 | MIT | non servi |
| npm | `string.prototype.trimstart` | 1.0.8 | MIT | non servi |
| npm | `tar-stream` | 2.2.0 | MIT | non servi |
| npm | `text-segmentation` | 1.0.3 | MIT | non servi |
| npm | `tmp` | 0.2.7 | MIT | non servi |
| npm | `traverse` | 0.3.10 | MIT | non servi |
| npm | `typed-array-buffer` | 1.0.3 | MIT | non servi |
| npm | `typed-array-byte-length` | 1.0.3 | MIT | non servi |
| npm | `typed-array-byte-offset` | 1.0.5 | MIT | non servi |
| npm | `typed-array-length` | 1.0.8 | MIT | non servi |
| npm | `typedarray.prototype.slice` | 1.0.6 | MIT | non servi |
| npm | `unbox-primitive` | 1.1.0 | MIT | non servi |
| npm | `unzipper` | 0.10.14 | MIT | non servi |
| npm | `util-deprecate` | 1.0.2 | MIT | non servi |
| npm | `utrie` | 1.0.2 | MIT | non servi |
| npm | `uuid` | 11.1.1 | MIT | non servi |
| npm | `which-boxed-primitive` | 1.1.1 | MIT | non servi |
| npm | `which-builtin-type` | 1.2.1 | MIT | non servi |
| npm | `which-collection` | 1.0.2 | MIT | non servi |
| npm | `which-typed-array` | 1.1.24 | MIT | non servi |
| npm | `wrappy` | 1.0.2 | ISC | non servi |
| npm | `xmlchars` | 2.2.0 | MIT | non servi |
| npm | `zip-stream` | 4.1.1 | MIT | non servi |
| npm | `@fast-csv/format` | dans exceljs 4.4.0 | MIT | livré |
| npm | `@fast-csv/parse` | dans exceljs 4.4.0 | MIT | livré |
| npm | `asn1.js` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `base64-js` | dans exceljs 4.4.0 | MIT | livré |
| npm | `bn.js` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `brorand` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browser-pack` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browserify-aes` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browserify-cipher` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browserify-des` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browserify-rsa` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `browserify-sign` | dans exceljs 4.4.0, licence relevée | ISC | livré |
| npm | `buffer` | dans exceljs 4.4.0 | MIT | livré |
| npm | `buffer-xor` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `cipher-base` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `core-js` | dans exceljs 4.4.0 | MIT | livré |
| npm | `core-util-is` | dans exceljs 4.4.0 | MIT | livré |
| npm | `create-ecdh` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `create-hash` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `create-hmac` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `crypto-browserify` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `dayjs` | dans exceljs 4.4.0 | MIT | livré |
| npm | `des.js` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `diffie-hellman` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `elliptic` | dans exceljs 4.4.0, version 6.5.4 | MIT | livré |
| npm | `events` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `evp_bytestokey` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `fast-csv` | dans exceljs 4.4.0 | MIT | livré |
| npm | `hash-base` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `hash.js` | dans exceljs 4.4.0, licence relevée | (MIT AND Apache-2.0) | livré |
| npm | `hmac-drbg` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `ieee754` | dans exceljs 4.4.0 | BSD-3-Clause | livré |
| npm | `immediate` | dans exceljs 4.4.0, par jszip | MIT | livré |
| npm | `inherits` | dans exceljs 4.4.0 | ISC | livré |
| npm | `isarray` | dans exceljs 4.4.0 | MIT | livré |
| npm | `jszip` | dans exceljs 4.4.0 | (MIT OR GPL-3.0-or-later) | livré |
| npm | `lie` | dans exceljs 4.4.0, par jszip | MIT | livré |
| npm | `lodash.escaperegexp` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.groupby` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.isboolean` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.isequal` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.isfunction` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.isnil` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.isundefined` | dans exceljs 4.4.0 | MIT | livré |
| npm | `lodash.uniq` | dans exceljs 4.4.0 | MIT | livré |
| npm | `md5.js` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `miller-rabin` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `minimalistic-assert` | dans exceljs 4.4.0, licence relevée | ISC | livré |
| npm | `minimalistic-crypto-utils` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `pako` | dans exceljs 4.4.0, par jszip | (MIT AND Zlib) | livré |
| npm | `parse-asn1` | dans exceljs 4.4.0, licence relevée | ISC | livré |
| npm | `pbkdf2` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `process` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `process-nextick-args` | dans exceljs 4.4.0 | MIT | livré |
| npm | `public-encrypt` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `randombytes` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `randomfill` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `readable-stream` | dans exceljs 4.4.0 | MIT | livré |
| npm | `regenerator-runtime` | dans exceljs 4.4.0 | MIT | livré |
| npm | `ripemd160` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `safe-buffer` | dans exceljs 4.4.0 | MIT | livré |
| npm | `safer-buffer` | dans exceljs 4.4.0 | MIT | livré |
| npm | `saxes` | dans exceljs 4.4.0 | ISC | livré |
| npm | `setimmediate` | dans exceljs 4.4.0, par jszip | MIT | livré |
| npm | `sha.js` | dans exceljs 4.4.0, licence relevée | (MIT AND BSD-3-Clause) | livré |
| npm | `stream-browserify` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `string_decoder` | dans exceljs 4.4.0 | MIT | livré |
| npm | `timers-browserify` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `util` | dans exceljs 4.4.0, licence relevée | MIT | livré |
| npm | `util-deprecate` | dans exceljs 4.4.0 | MIT | livré |
| npm | `uuid` | dans exceljs 4.4.0 | (MIT AND BSD-3-Clause) | livré |
| npm | `xmlchars` | dans exceljs 4.4.0 | MIT | livré |

La police Inter (OFL-1.1) et les icônes Lucide (ISC) sont embarquées dans l'interface ; leurs textes de
licence sont livrés avec les paquets (dossier `licenses` des ZIP).

Livrés, et importés aussi par les essais et les parcours de l'interface (déclarés en dépendances de
développement de `web/package.json`) : `fflate`.

## Le runtime .NET

Le runtime .NET 10 (`Microsoft.NETCore.App` et `Microsoft.AspNetCore.App`, licence MIT, © .NET Foundation and
Contributors) est embarqué dans l'exécutable du mode Poste (ZIP portable et paquet du Store) ; le mode Serveur
utilise celui du serveur. Sa version exacte est dans le SBOM (`sbom.cdx.json`). Son texte de licence et la liste des
composants qu'il inclut (`LICENSE.TXT` et `THIRD-PARTY-NOTICES.TXT` de ses paquets d'exécution), comme la liste
que porte chaque paquet de Microsoft livré, sont copiés dans le dossier `licenses/microsoft` des paquets.

## Textes des licences

Pour chaque composant livré : le texte de licence que porte son paquet (avec son avis de copyright), sinon le texte
de la licence qu'il déclare, précédé de l'avis tiré de ses métadonnées (copyright du `.nuspec`, auteur du
`package.json`, lignes de copyright de ses sources dans la carte de l'assemblage). Pour un paquet porté dans un
fichier déjà assemblé, sa version n'est pas connue : le texte est celui de la copie du même nom installée quand elle
déclare la même licence. Un même texte n'est écrit qu'une fois, après les composants qu'il couvre.

### 1. Apache-2.0

- `Anthropic` 13.3.0

~~~text
Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.

      To apply the Apache License to your work, attach the following
      boilerplate notice, with the fields enclosed by brackets "[]"
      replaced with your own identifying information. (Don't include
      the brackets!)  The text should be enclosed in the appropriate
      comment syntax for the file format. We also recommend that a
      file or class name and description of purpose be included on the
      same "printed page" as the copyright notice for easier
      identification within third-party archives.

   Copyright 2023 Anthropic, PBC.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

- - -

This repository changed its license from the MIT License to the Apache License, Version 2.0 (see LICENSE) in version 13.0.0 of the Anthropic package. The MIT License below still applies to every version published under it, and to third-party contributions merged into this repository before the change.

--------------------------------------------------------------------------------

Copyright 2023 Anthropic, PBC.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 2. Apache-2.0

- `AWSSDK.BedrockRuntime` 4.0.101.14 : © Amazon Web Services (auteurs déclarés du paquet) (texte de la licence déclarée)
- `AWSSDK.Core` 4.0.102.15 : © Amazon Web Services (auteurs déclarés du paquet) (texte de la licence déclarée)
- `AWSSDK.SSO` 4.0.100.23 : © Amazon Web Services (auteurs déclarés du paquet) (texte de la licence déclarée)
- `AWSSDK.SSOOIDC` 4.0.100.22 : © Amazon Web Services (auteurs déclarés du paquet) (texte de la licence déclarée)
- `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 : Copyright 2014-2024 SourceGear, LLC (texte de la licence déclarée)
- `SQLitePCLRaw.core` 2.1.13 : Copyright 2014-2024 SourceGear, LLC (texte de la licence déclarée)
- `SQLitePCLRaw.lib.e_sqlite3` 2.1.13 : Copyright 2014-2024 SourceGear, LLC (texte de la licence déclarée)
- `SQLitePCLRaw.provider.e_sqlite3` 2.1.13 : Copyright 2014-2024 SourceGear, LLC (texte de la licence déclarée)

~~~text
Apache License

Version 2.0, January 2004

http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.

"License" shall mean the terms and conditions for use, reproduction, and distribution as defined by Sections 1 through 9 of this document.

"Licensor" shall mean the copyright owner or entity authorized by the copyright owner that is granting the License.

"Legal Entity" shall mean the union of the acting entity and all other entities that control, are controlled by, or are under common control with that entity. For the purposes of this definition, "control" means (i) the power, direct or indirect, to cause the direction or management of such entity, whether by contract or otherwise, or (ii) ownership of fifty percent (50%) or more of the outstanding shares, or (iii) beneficial ownership of such entity.

"You" (or "Your") shall mean an individual or Legal Entity exercising permissions granted by this License.

"Source" form shall mean the preferred form for making modifications, including but not limited to software source code, documentation source, and configuration files.

"Object" form shall mean any form resulting from mechanical transformation or translation of a Source form, including but not limited to compiled object code, generated documentation, and conversions to other media types.

"Work" shall mean the work of authorship, whether in Source or Object form, made available under the License, as indicated by a copyright notice that is included in or attached to the work (an example is provided in the Appendix below).

"Derivative Works" shall mean any work, whether in Source or Object form, that is based on (or derived from) the Work and for which the editorial revisions, annotations, elaborations, or other modifications represent, as a whole, an original work of authorship. For the purposes of this License, Derivative Works shall not include works that remain separable from, or merely link (or bind by name) to the interfaces of, the Work and Derivative Works thereof.

"Contribution" shall mean any work of authorship, including the original version of the Work and any modifications or additions to that Work or Derivative Works thereof, that is intentionally submitted to Licensor for inclusion in the Work by the copyright owner or by an individual or Legal Entity authorized to submit on behalf of the copyright owner. For the purposes of this definition, "submitted" means any form of electronic, verbal, or written communication sent to the Licensor or its representatives, including but not limited to communication on electronic mailing lists, source code control systems, and issue tracking systems that are managed by, or on behalf of, the Licensor for the purpose of discussing and improving the Work, but excluding communication that is conspicuously marked or otherwise designated in writing by the copyright owner as "Not a Contribution."

"Contributor" shall mean Licensor and any individual or Legal Entity on behalf of whom a Contribution has been received by Licensor and subsequently incorporated within the Work.

2. Grant of Copyright License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable copyright license to reproduce, prepare Derivative Works of, publicly display, publicly perform, sublicense, and distribute the Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as stated in this section) patent license to make, have made, use, offer to sell, sell, import, and otherwise transfer the Work, where such license applies only to those patent claims licensable by such Contributor that are necessarily infringed by their Contribution(s) alone or by combination of their Contribution(s) with the Work to which such Contribution(s) was submitted. If You institute patent litigation against any entity (including a cross-claim or counterclaim in a lawsuit) alleging that the Work or a Contribution incorporated within the Work constitutes direct or contributory patent infringement, then any patent licenses granted to You under this License for that Work shall terminate as of the date such litigation is filed.

4. Redistribution. You may reproduce and distribute copies of the Work or Derivative Works thereof in any medium, with or without modifications, and in Source or Object form, provided that You meet the following conditions:

You must give any other recipients of the Work or Derivative Works a copy of this License; and

You must cause any modified files to carry prominent notices stating that You changed the files; and

You must retain, in the Source form of any Derivative Works that You distribute, all copyright, patent, trademark, and attribution notices from the Source form of the Work, excluding those notices that do not pertain to any part of the Derivative Works; and

If the Work includes a "NOTICE" text file as part of its distribution, then any Derivative Works that You distribute must include a readable copy of the attribution notices contained within such NOTICE file, excluding those notices that do not pertain to any part of the Derivative Works, in at least one of the following places: within a NOTICE text file distributed as part of the Derivative Works; within the Source form or documentation, if provided along with the Derivative Works; or, within a display generated by the Derivative Works, if and wherever such third-party notices normally appear. The contents of the NOTICE file are for informational purposes only and do not modify the License. You may add Your own attribution notices within Derivative Works that You distribute, alongside or as an addendum to the NOTICE text from the Work, provided that such additional attribution notices cannot be construed as modifying the License. You may add Your own copyright statement to Your modifications and may provide additional or different license terms and conditions for use, reproduction, or distribution of Your modifications, or for any such Derivative Works as a whole, provided Your use, reproduction, and distribution of the Work otherwise complies with the conditions stated in this License.

5. Submission of Contributions. Unless You explicitly state otherwise, any Contribution intentionally submitted for inclusion in the Work by You to the Licensor shall be under the terms and conditions of this License, without any additional terms or conditions. Notwithstanding the above, nothing herein shall supersede or modify the terms of any separate license agreement you may have executed with Licensor regarding such Contributions.

6. Trademarks. This License does not grant permission to use the trade names, trademarks, service marks, or product names of the Licensor, except as required for reasonable and customary use in describing the origin of the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or agreed to in writing, Licensor provides the Work (and each Contributor provides its Contributions) on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied, including, without limitation, any warranties or conditions of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A PARTICULAR PURPOSE. You are solely responsible for determining the appropriateness of using or redistributing the Work and assume any risks associated with Your exercise of permissions under this License.

8. Limitation of Liability. In no event and under no legal theory, whether in tort (including negligence), contract, or otherwise, unless required by applicable law (such as deliberate and grossly negligent acts) or agreed to in writing, shall any Contributor be liable to You for damages, including any direct, indirect, special, incidental, or consequential damages of any character arising as a result of this License or out of the use or inability to use the Work (including but not limited to damages for loss of goodwill, work stoppage, computer failure or malfunction, or any and all other commercial damages or losses), even if such Contributor has been advised of the possibility of such damages.

9. Accepting Warranty or Additional Liability. While redistributing the Work or Derivative Works thereof, You may choose to offer, and charge a fee for, acceptance of support, warranty, indemnity, or other liability obligations and/or rights consistent with this License. However, in accepting such obligations, You may act only on Your own behalf and on Your sole responsibility, not on behalf of any other Contributor, and only if You agree to indemnify, defend, and hold each Contributor harmless for any liability incurred by, or claims asserted against, such Contributor by reason of your accepting any such warranty or additional liability.

END OF TERMS AND CONDITIONS
~~~

### 3. MIT

- `Microsoft.AspNetCore.Authentication.Negotiate` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.AspNetCore.OpenApi` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Data.SqlClient` 7.1.1 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Data.SqlClient.Extensions.Abstractions` 7.1.1 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Data.SqlClient.Internal.Logging` 7.1.1 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Data.Sqlite.Core` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore.Abstractions` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore.Relational` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore.Sqlite.Core` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Extensions.AI.Abstractions` 10.5.1 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.Extensions.DependencyModel` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.Abstractions` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.JsonWebTokens` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.Logging` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.Protocols` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.Protocols.OpenIdConnect` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.IdentityModel.Tokens` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.OpenApi` 2.12.2 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `Microsoft.SqlServer.Server` 1.0.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.Configuration.ConfigurationManager` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.DirectoryServices` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.DirectoryServices.AccountManagement` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.DirectoryServices.Protocols` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.IdentityModel.Tokens.Jwt` 8.16.0 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.Security.Cryptography.Pkcs` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `System.Security.Cryptography.ProtectedData` 10.0.13 : © Microsoft Corporation. All rights reserved. (texte de la licence déclarée)
- `browser-pack` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `asn1.js` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `bn.js` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `brorand` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `browserify-aes` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `browserify-cipher` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `browserify-des` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `browserify-rsa` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `buffer-xor` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `cipher-base` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `create-ecdh` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `create-hash` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `create-hmac` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `crypto-browserify` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `des.js` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `diffie-hellman` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `elliptic` dans exceljs 4.4.0, version 6.5.4 : © Fedor Indutny (auteur déclaré du paquet) (texte de la licence déclarée)
- `events` dans exceljs 4.4.0, licence relevée : Copyright Joyent, Inc. and other Node contributors. (texte de la licence déclarée)
- `evp_bytestokey` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `hash-base` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `hmac-drbg` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `md5.js` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `miller-rabin` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `minimalistic-crypto-utils` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `pbkdf2` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `process` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `public-encrypt` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `randombytes` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `randomfill` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `ripemd160` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `stream-browserify` dans exceljs 4.4.0, licence relevée : Copyright Joyent, Inc. and other Node contributors. (texte de la licence déclarée)
- `isarray` dans exceljs 4.4.0 : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `timers-browserify` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `util` dans exceljs 4.4.0, licence relevée : Copyright Joyent, Inc. and other Node contributors. (texte de la licence déclarée)

~~~text
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 4. MIT

- `Microsoft.Bcl.Cryptography` 9.0.18

~~~text
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 5. file: LICENSE.txt

- `Microsoft.Data.SqlClient.SNI.runtime` 7.1.0

~~~text
MICROSOFT SOFTWARE LICENSE TERMS

MICROSOFT.DATA.SQLCLIENT.SNI LIBRARY

These license terms are an agreement between you and Microsoft Corporation (or based on where you live, one of its affiliates). They apply to the software named above. The terms also apply to any Microsoft services or updates for the software, except to the extent those have different terms.

IF YOU COMPLY WITH THESE LICENSE TERMS, YOU HAVE THE RIGHTS BELOW.

1.  INSTALLATION AND USE RIGHTS.
    You may install and use any number of copies of the software to develop and test your applications.
2.  THIRD PARTY COMPONENTS. The software may include third party components with separate legal notices or governed by other agreements, as may be described in the ThirdPartyNotices file(s) accompanying the software.
3.  ADDITIONAL LICENSING REQUIREMENTS AND/OR USE RIGHTS.
    a. DISTRIBUTABLE CODE.  The software is comprised of Distributable Code. "Distributable Code" is code that you are permitted to distribute in applications you develop if you comply with the terms below.
       i. Right to Use and Distribute.
          * You may copy and distribute the object code form of the software.
          * Third Party Distribution. You may permit distributors of your applications to copy and distribute the Distributable Code as part of those applications.
      ii. Distribution Requirements. For any Distributable Code you distribute, you must
          * use the Distributable Code in your applications and not as a standalone distribution;
          * require distributors and external end users to agree to terms that protect it at least as much as this agreement; and
          * indemnify, defend, and hold harmless Microsoft from any claims, including attorneys' fees, related to the distribution or use of your applications, except to the extent that any claim is based solely on the unmodified Distributable Code.
     iii. Distribution Restrictions. You may not
          * use Microsoft's trademarks in your applications' names or in a way that suggests your applications come from or are endorsed by Microsoft; or
          * modify or distribute the source code of any Distributable Code so that any part of it becomes subject to an Excluded License. An "Excluded License" is one that requires, as a condition of use, modification or distribution of code, that (i) it be disclosed or distributed in source code form; or (ii) others have the right to modify it.
4.  DATA.
    a. Data Collection. Some features in the software may enable collection of data from users of your applications that access or use the software. If you use these features to enable data collection in your applications, you must comply with applicable law, including getting any required user consent, and maintain a prominent privacy policy that accurately informs users about how you use, collect, and share their data. You agree to comply with all applicable provisions of the Microsoft Privacy Statement at [https://go.microsoft.com/fwlink/?LinkId=521839].
5.  SCOPE OF LICENSE. The software is licensed, not sold. This agreement only gives you some rights to use the software. Microsoft reserves all other rights. Unless applicable law gives you more rights despite this limitation, you may use the software only as expressly permitted in this agreement. In doing so, you must comply with any technical limitations in the software that only allow you to use it in certain ways. You may not
    * work around any technical limitations in the software;
    * reverse engineer, decompile or disassemble the software, or otherwise attempt to derive the source code for the software, except and to the extent required by third party licensing terms governing use of certain open source components that may be included in the software;
    * remove, minimize, block or modify any notices of Microsoft or its suppliers in the software;
    * use the software in any way that is against the law; or
    * share, publish, rent or lease the software, provide the software as a stand-alone offering for others to use, or transfer the software or this agreement to any third party.
6.  EXPORT RESTRICTIONS. You must comply with all domestic and international export laws and regulations that apply to the software, which include restrictions on destinations, end users, and end use. For further information on export restrictions, visit www.microsoft.com/exporting.
7.  SUPPORT SERVICES. Because this software is "as is," we may not provide support services for it.
8.  ENTIRE AGREEMENT. This agreement, and the terms for supplements, updates, Internet-based services and support services that you use, are the entire agreement for the software and support services.
9.  APPLICABLE LAW.  If you acquired the software in the United States, Washington law applies to interpretation of and claims for breach of this agreement, and the laws of the state where you live apply to all other claims. If you acquired the software in any other country, its laws apply.
10. CONSUMER RIGHTS; REGIONAL VARIATIONS. This agreement describes certain legal rights. You may have other rights, including consumer rights, under the laws of your state or country. Separate and apart from your relationship with Microsoft, you may also have rights with respect to the party from which you acquired the software. This agreement does not change those other rights if the laws of your state or country do not permit it to do so. For example, if you acquired the software in one of the below regions, or mandatory country law applies, then the following provisions apply to you:
    a) Australia. You have statutory guarantees under the Australian Consumer Law and nothing in this agreement is intended to affect those rights.
    b) Canada. If you acquired this software in Canada, you may stop receiving updates by turning off the automatic update feature, disconnecting your device from the Internet (if and when you re-connect to the Internet, however, the software will resume checking for and installing updates), or uninstalling the software. The product documentation, if any, may also specify how to turn off updates for your specific device or software.
    c) Germany and Austria.
       (i) Warranty. The software will perform substantially as described in any Microsoft materials that accompany it. However, Microsoft gives no contractual guarantee in relation to the software.
       (ii) Limitation of Liability. In case of intentional conduct, gross negligence, claims based on the Product Liability Act, as well as in case of death or personal or physical injury, Microsoft is liable according to the statutory law.
    Subject to the foregoing clause (ii), Microsoft will only be liable for slight negligence if Microsoft is in breach of such material contractual obligations, the fulfillment of which facilitate the due performance of this agreement, the breach of which would endanger the purpose of this agreement and the compliance with which a party may constantly trust in (so-called "cardinal obligations"). In other cases of slight negligence, Microsoft will not be liable for slight negligence
11. DISCLAIMER OF WARRANTY. THE SOFTWARE IS LICENSED "AS-IS." YOU BEAR THE RISK OF USING IT. MICROSOFT GIVES NO EXPRESS WARRANTIES, GUARANTEES OR CONDITIONS. TO THE EXTENT PERMITTED UNDER YOUR LOCAL LAWS, MICROSOFT EXCLUDES THE IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT.
12. LIMITATION ON AND EXCLUSION OF REMEDIES AND DAMAGES. YOU CAN RECOVER FROM MICROSOFT AND ITS SUPPLIERS ONLY DIRECT DAMAGES UP TO U.S. $5.00. YOU CANNOT RECOVER ANY OTHER DAMAGES, INCLUDING CONSEQUENTIAL, LOST PROFITS, SPECIAL, INDIRECT OR INCIDENTAL DAMAGES.
    This limitation applies to (a) anything related to the software, services, content (including code) on third party Internet sites, or third party applications; and (b) claims for breach of contract, breach of warranty, guarantee or condition, strict liability, negligence, or other tort to the extent permitted by applicable law.
    It also applies even if Microsoft knew or should have known about the possibility of the damages. The above limitation or exclusion may not apply to you because your state or country may not allow the exclusion or limitation of incidental, consequential or other damages.
~~~

### 6. MIT

- `@babel/runtime` 7.29.10

~~~text
MIT License

Copyright (c) 2014-present Sebastian McKenzie and other contributors

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 7. OFL-1.1

- `@fontsource-variable/inter` 5.3.0

~~~text
Copyright 2016 The Inter Project Authors (https://github.com/rsms/inter) Inter-Italic[opsz,wght].ttf: Copyright 2016 The Inter Project Authors (https://github.com/rsms/inter)

This Font Software is licensed under the SIL Open Font License, Version 1.1.
This license is copied below, and is also available with a FAQ at:
http://scripts.sil.org/OFL


-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded,
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.
~~~

### 8. MIT

- `@xyflow/react` 12.12.0
- `@xyflow/system` 0.0.83

~~~text
MIT License

Copyright (c) 2019-2025 webkid GmbH

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 9. MIT

- `zustand` 4.5.7
- `zustand` 5.0.15

~~~text
MIT License

Copyright (c) 2019 Paul Henschel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 10. MIT

- `canvg` 3.0.11

~~~text
The MIT License (MIT)

Copyright (c) 2010 - present Gabe Lerner (gabelerner@gmail.com) - https://github.com/canvg/canvg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 11. MIT

- `classcat` 5.0.5

~~~text
Copyright © Jorge Bucaran <<https://jorgebucaran.com>>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 12. MIT

- `core-js` 3.51.0
- `core-js` dans exceljs 4.4.0

~~~text
Copyright (c) 2013–2025 Denis Pushkarev (zloirock.ru)
Copyright (c) 2025–2026 CoreJS Company (core-js.io)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 13. ISC

- `d3-color` 3.1.1
- `d3-dispatch` 3.0.2
- `d3-drag` 3.0.1
- `d3-interpolate` 3.0.2
- `d3-selection` 3.0.1
- `d3-timer` 3.0.2
- `d3-transition` 3.0.2
- `d3-zoom` 3.0.1

~~~text
Copyright 2010-2026 Mike Bostock

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
~~~

### 14. BSD-3-Clause

- `d3-ease` 3.0.2

~~~text
Copyright 2010-2026 Mike Bostock
Copyright 2001 Robert Penner
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the author nor the names of contributors may be used to
  endorse or promote products derived from this software without specific prior
  written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
~~~

### 15. (MPL-2.0 OR Apache-2.0)

- `dompurify` 3.4.17

~~~text
Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.

      To apply the Apache License to your work, attach the following
      boilerplate notice, with the fields enclosed by brackets "[]"
      replaced with your own identifying information. (Don't include
      the brackets!)  The text should be enclosed in the appropriate
      comment syntax for the file format. We also recommend that a
      file or class name and description of purpose be included on the
      same "printed page" as the copyright notice for easier
      identification within third-party archives.

   Copyright [yyyy] [name of copyright owner]

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

- - -

Mozilla Public License Version 2.0
==================================

1. Definitions
--------------

1.1. "Contributor"
    means each individual or legal entity that creates, contributes to
    the creation of, or owns Covered Software.

1.2. "Contributor Version"
    means the combination of the Contributions of others (if any) used
    by a Contributor and that particular Contributor's Contribution.

1.3. "Contribution"
    means Covered Software of a particular Contributor.

1.4. "Covered Software"
    means Source Code Form to which the initial Contributor has attached
    the notice in Exhibit A, the Executable Form of such Source Code
    Form, and Modifications of such Source Code Form, in each case
    including portions thereof.

1.5. "Incompatible With Secondary Licenses"
    means

    (a) that the initial Contributor has attached the notice described
        in Exhibit B to the Covered Software; or

    (b) that the Covered Software was made available under the terms of
        version 1.1 or earlier of the License, but not also under the
        terms of a Secondary License.

1.6. "Executable Form"
    means any form of the work other than Source Code Form.

1.7. "Larger Work"
    means a work that combines Covered Software with other material, in
    a separate file or files, that is not Covered Software.

1.8. "License"
    means this document.

1.9. "Licensable"
    means having the right to grant, to the maximum extent possible,
    whether at the time of the initial grant or subsequently, any and
    all of the rights conveyed by this License.

1.10. "Modifications"
    means any of the following:

    (a) any file in Source Code Form that results from an addition to,
        deletion from, or modification of the contents of Covered
        Software; or

    (b) any new file in Source Code Form that contains any Covered
        Software.

1.11. "Patent Claims" of a Contributor
    means any patent claim(s), including without limitation, method,
    process, and apparatus claims, in any patent Licensable by such
    Contributor that would be infringed, but for the grant of the
    License, by the making, using, selling, offering for sale, having
    made, import, or transfer of either its Contributions or its
    Contributor Version.

1.12. "Secondary License"
    means either the GNU General Public License, Version 2.0, the GNU
    Lesser General Public License, Version 2.1, the GNU Affero General
    Public License, Version 3.0, or any later versions of those
    licenses.

1.13. "Source Code Form"
    means the form of the work preferred for making modifications.

1.14. "You" (or "Your")
    means an individual or a legal entity exercising rights under this
    License. For legal entities, "You" includes any entity that
    controls, is controlled by, or is under common control with You. For
    purposes of this definition, "control" means (a) the power, direct
    or indirect, to cause the direction or management of such entity,
    whether by contract or otherwise, or (b) ownership of more than
    fifty percent (50%) of the outstanding shares or beneficial
    ownership of such entity.

2. License Grants and Conditions
--------------------------------

2.1. Grants

Each Contributor hereby grants You a world-wide, royalty-free,
non-exclusive license:

(a) under intellectual property rights (other than patent or trademark)
    Licensable by such Contributor to use, reproduce, make available,
    modify, display, perform, distribute, and otherwise exploit its
    Contributions, either on an unmodified basis, with Modifications, or
    as part of a Larger Work; and

(b) under Patent Claims of such Contributor to make, use, sell, offer
    for sale, have made, import, and otherwise transfer either its
    Contributions or its Contributor Version.

2.2. Effective Date

The licenses granted in Section 2.1 with respect to any Contribution
become effective for each Contribution on the date the Contributor first
distributes such Contribution.

2.3. Limitations on Grant Scope

The licenses granted in this Section 2 are the only rights granted under
this License. No additional rights or licenses will be implied from the
distribution or licensing of Covered Software under this License.
Notwithstanding Section 2.1(b) above, no patent license is granted by a
Contributor:

(a) for any code that a Contributor has removed from Covered Software;
    or

(b) for infringements caused by: (i) Your and any other third party's
    modifications of Covered Software, or (ii) the combination of its
    Contributions with other software (except as part of its Contributor
    Version); or

(c) under Patent Claims infringed by Covered Software in the absence of
    its Contributions.

This License does not grant any rights in the trademarks, service marks,
or logos of any Contributor (except as may be necessary to comply with
the notice requirements in Section 3.4).

2.4. Subsequent Licenses

No Contributor makes additional grants as a result of Your choice to
distribute the Covered Software under a subsequent version of this
License (see Section 10.2) or under the terms of a Secondary License (if
permitted under the terms of Section 3.3).

2.5. Representation

Each Contributor represents that the Contributor believes its
Contributions are its original creation(s) or it has sufficient rights
to grant the rights to its Contributions conveyed by this License.

2.6. Fair Use

This License is not intended to limit any rights You have under
applicable copyright doctrines of fair use, fair dealing, or other
equivalents.

2.7. Conditions

Sections 3.1, 3.2, 3.3, and 3.4 are conditions of the licenses granted
in Section 2.1.

3. Responsibilities
-------------------

3.1. Distribution of Source Form

All distribution of Covered Software in Source Code Form, including any
Modifications that You create or to which You contribute, must be under
the terms of this License. You must inform recipients that the Source
Code Form of the Covered Software is governed by the terms of this
License, and how they can obtain a copy of this License. You may not
attempt to alter or restrict the recipients' rights in the Source Code
Form.

3.2. Distribution of Executable Form

If You distribute Covered Software in Executable Form then:

(a) such Covered Software must also be made available in Source Code
    Form, as described in Section 3.1, and You must inform recipients of
    the Executable Form how they can obtain a copy of such Source Code
    Form by reasonable means in a timely manner, at a charge no more
    than the cost of distribution to the recipient; and

(b) You may distribute such Executable Form under the terms of this
    License, or sublicense it under different terms, provided that the
    license for the Executable Form does not attempt to limit or alter
    the recipients' rights in the Source Code Form under this License.

3.3. Distribution of a Larger Work

You may create and distribute a Larger Work under terms of Your choice,
provided that You also comply with the requirements of this License for
the Covered Software. If the Larger Work is a combination of Covered
Software with a work governed by one or more Secondary Licenses, and the
Covered Software is not Incompatible With Secondary Licenses, this
License permits You to additionally distribute such Covered Software
under the terms of such Secondary License(s), so that the recipient of
the Larger Work may, at their option, further distribute the Covered
Software under the terms of either this License or such Secondary
License(s).

3.4. Notices

You may not remove or alter the substance of any license notices
(including copyright notices, patent notices, disclaimers of warranty,
or limitations of liability) contained within the Source Code Form of
the Covered Software, except that You may alter any license notices to
the extent required to remedy known factual inaccuracies.

3.5. Application of Additional Terms

You may choose to offer, and to charge a fee for, warranty, support,
indemnity or liability obligations to one or more recipients of Covered
Software. However, You may do so only on Your own behalf, and not on
behalf of any Contributor. You must make it absolutely clear that any
such warranty, support, indemnity, or liability obligation is offered by
You alone, and You hereby agree to indemnify every Contributor for any
liability incurred by such Contributor as a result of warranty, support,
indemnity or liability terms You offer. You may include additional
disclaimers of warranty and limitations of liability specific to any
jurisdiction.

4. Inability to Comply Due to Statute or Regulation
---------------------------------------------------

If it is impossible for You to comply with any of the terms of this
License with respect to some or all of the Covered Software due to
statute, judicial order, or regulation then You must: (a) comply with
the terms of this License to the maximum extent possible; and (b)
describe the limitations and the code they affect. Such description must
be placed in a text file included with all distributions of the Covered
Software under this License. Except to the extent prohibited by statute
or regulation, such description must be sufficiently detailed for a
recipient of ordinary skill to be able to understand it.

5. Termination
--------------

5.1. The rights granted under this License will terminate automatically
if You fail to comply with any of its terms. However, if You become
compliant, then the rights granted under this License from a particular
Contributor are reinstated (a) provisionally, unless and until such
Contributor explicitly and finally terminates Your grants, and (b) on an
ongoing basis, if such Contributor fails to notify You of the
non-compliance by some reasonable means prior to 60 days after You have
come back into compliance. Moreover, Your grants from a particular
Contributor are reinstated on an ongoing basis if such Contributor
notifies You of the non-compliance by some reasonable means, this is the
first time You have received notice of non-compliance with this License
from such Contributor, and You become compliant prior to 30 days after
Your receipt of the notice.

5.2. If You initiate litigation against any entity by asserting a patent
infringement claim (excluding declaratory judgment actions,
counter-claims, and cross-claims) alleging that a Contributor Version
directly or indirectly infringes any patent, then the rights granted to
You by any and all Contributors for the Covered Software under Section
2.1 of this License shall terminate.

5.3. In the event of termination under Sections 5.1 or 5.2 above, all
end user license agreements (excluding distributors and resellers) which
have been validly granted by You or Your distributors under this License
prior to termination shall survive termination.

************************************************************************
*                                                                      *
*  6. Disclaimer of Warranty                                           *
*  -------------------------                                           *
*                                                                      *
*  Covered Software is provided under this License on an "as is"       *
*  basis, without warranty of any kind, either expressed, implied, or  *
*  statutory, including, without limitation, warranties that the       *
*  Covered Software is free of defects, merchantable, fit for a        *
*  particular purpose or non-infringing. The entire risk as to the     *
*  quality and performance of the Covered Software is with You.        *
*  Should any Covered Software prove defective in any respect, You     *
*  (not any Contributor) assume the cost of any necessary servicing,   *
*  repair, or correction. This disclaimer of warranty constitutes an   *
*  essential part of this License. No use of any Covered Software is   *
*  authorized under this License except under this disclaimer.         *
*                                                                      *
************************************************************************

************************************************************************
*                                                                      *
*  7. Limitation of Liability                                          *
*  --------------------------                                          *
*                                                                      *
*  Under no circumstances and under no legal theory, whether tort      *
*  (including negligence), contract, or otherwise, shall any           *
*  Contributor, or anyone who distributes Covered Software as          *
*  permitted above, be liable to You for any direct, indirect,         *
*  special, incidental, or consequential damages of any character      *
*  including, without limitation, damages for lost profits, loss of    *
*  goodwill, work stoppage, computer failure or malfunction, or any    *
*  and all other commercial damages or losses, even if such party      *
*  shall have been informed of the possibility of such damages. This   *
*  limitation of liability shall not apply to liability for death or   *
*  personal injury resulting from such party's negligence to the       *
*  extent applicable law prohibits such limitation. Some               *
*  jurisdictions do not allow the exclusion or limitation of           *
*  incidental or consequential damages, so this exclusion and          *
*  limitation may not apply to You.                                    *
*                                                                      *
************************************************************************

8. Litigation
-------------

Any litigation relating to this License may be brought only in the
courts of a jurisdiction where the defendant maintains its principal
place of business and such litigation shall be governed by laws of that
jurisdiction, without reference to its conflict-of-law provisions.
Nothing in this Section shall prevent a party's ability to bring
cross-claims or counter-claims.

9. Miscellaneous
----------------

This License represents the complete agreement concerning the subject
matter hereof. If any provision of this License is held to be
unenforceable, such provision shall be reformed only to the extent
necessary to make it enforceable. Any law or regulation which provides
that the language of a contract shall be construed against the drafter
shall not be used to construe this License against a Contributor.

10. Versions of the License
---------------------------

10.1. New Versions

Mozilla Foundation is the license steward. Except as provided in Section
10.3, no one other than the license steward has the right to modify or
publish new versions of this License. Each version will be given a
distinguishing version number.

10.2. Effect of New Versions

You may distribute the Covered Software under the terms of the version
of the License under which You originally received the Covered Software,
or under the terms of any subsequent version published by the license
steward.

10.3. Modified Versions

If you create software not governed by this License, and you want to
create a new license for such software, you may create and use a
modified version of this License if you rename the license and remove
any references to the name of the license steward (except to note that
such modified license differs from this License).

10.4. Distributing Source Code Form that is Incompatible With Secondary
Licenses

If You choose to distribute Source Code Form that is Incompatible With
Secondary Licenses under the terms of this version of the License, the
notice described in Exhibit B of this License must be attached.

Exhibit A - Source Code Form License Notice
-------------------------------------------

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

If it is not possible or desirable to put the notice in a particular
file, then You may include the notice in a location (such as a LICENSE
file in a relevant directory) where a recipient would be likely to look
for such a notice.

You may add additional accurate notices of copyright ownership.

Exhibit B - "Incompatible With Secondary Licenses" Notice
---------------------------------------------------------

  This Source Code Form is "Incompatible With Secondary Licenses", as
  defined by the Mozilla Public License, v. 2.0.
~~~

### 16. MIT

- `exceljs` 4.4.0

~~~text
The MIT License (MIT)

Copyright (c) 2014-2019 Guyon Roche

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 17. MIT

- `fast-png` 6.4.0

~~~text
MIT License

Copyright (c) 2015 Michaël Zasso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 18. (MIT AND Zlib)

- `pako` 2.2.0
- `pako` dans exceljs 4.4.0, par jszip

~~~text
(The MIT License)

Copyright (C) 2014-2017 by Vitaly Puzrin and Andrei Tuputcyn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 19. MIT

- `fflate` 0.8.3

~~~text
MIT License

Copyright (c) 2026 Arjun Barrett

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 20. MIT

- `html-to-image` 1.11.11

~~~text
MIT License

Copyright (c) 2017-2023 W.Y.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 21. MIT

- `html2canvas` 1.4.1

~~~text
Copyright (c) 2012 Niklas von Hertzen

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
~~~

### 22. MIT

- `i18next` 26.4.3

~~~text
The MIT License (MIT)

Copyright (c) 2011-present i18next

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 23. OFL-1.1

- `inter-font` 3.19.0

~~~text
# MIT License

Copyright (c) 2022 Open Web Foundation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 24. MIT

- `iobuffer` 5.4.0

~~~text
The MIT License (MIT)

Copyright (c) 2015 Michaël Zasso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 25. MIT

- `jspdf` 4.2.1

~~~text
Copyright
(c) 2010-2025 James Hall, https://github.com/MrRio/jsPDF
(c) 2015-2025 yWorks GmbH, https://www.yworks.com/

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 26. ISC

- `lucide-react` 1.61.0

~~~text
ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

---

The following Lucide icons are derived from the Feather project:

airplay, alert-circle, alert-octagon, alert-triangle, aperture, arrow-down-circle, arrow-down-left, arrow-down-right, arrow-down, arrow-left-circle, arrow-left, arrow-right-circle, arrow-right, arrow-up-circle, arrow-up-left, arrow-up-right, arrow-up, at-sign, calendar, cast, check, chevron-down, chevron-left, chevron-right, chevron-up, chevrons-down, chevrons-left, chevrons-right, chevrons-up, circle, clipboard, clock, code, columns, command, compass, corner-down-left, corner-down-right, corner-left-down, corner-left-up, corner-right-down, corner-right-up, corner-up-left, corner-up-right, crosshair, database, divide-circle, divide-square, dollar-sign, download, external-link, feather, frown, hash, headphones, help-circle, info, italic, key, layout, life-buoy, link-2, link, loader, lock, log-in, log-out, maximize, meh, minimize, minimize-2, minus-circle, minus-square, minus, monitor, moon, more-horizontal, more-vertical, move, music, navigation-2, navigation, octagon, pause-circle, percent, plus-circle, plus-square, plus, power, radio, rss, search, server, share, shopping-bag, sidebar, smartphone, smile, square, table-2, tablet, target, terminal, trash-2, trash, triangle, tv, type, upload, x-circle, x-octagon, x-square, x, zoom-in, zoom-out

The MIT License (MIT) (for the icons listed above)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 27. MIT

- `performance-now` 2.1.0

~~~text
Copyright (c) 2013 Braveg1rl

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 28. MIT

- `raf` 3.4.1

~~~text
Copyright 2013 Chris Dickinson <chris@neversaw.us>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 29. MIT

- `react` 19.3.1
- `react-dom` 19.3.1
- `scheduler` 0.28.0
- `use-sync-external-store` 1.7.0

~~~text
MIT License

Copyright (c) Meta Platforms, Inc. and affiliates.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 30. MIT

- `react-i18next` 17.0.16

~~~text
The MIT License (MIT)

Copyright (c) 2015-present i18next

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 31. MIT OR SEE LICENSE IN FEEL-FREE.md

- `rgbcolor` 1.0.1

~~~text
Copyright (c) 2016 Stoyan Stefanov, http://phpied.com/

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


Exemptions
==========

Please either apply this, the MIT license, or the license in './FEEL-FREE.md'
~~~

### 32. MIT

- `stackblur-canvas` 2.7.0

~~~text
Copyright (c) 2010 Mario Klingemann

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
~~~

### 33. MIT

- `svg-pathdata` 6.0.3

~~~text
The MIT License (MIT)
Copyright © 2017 Nicolas Froidure

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 34. MIT

- `@fast-csv/format` dans exceljs 4.4.0
- `@fast-csv/parse` dans exceljs 4.4.0
- `fast-csv` dans exceljs 4.4.0

~~~text
The MIT License

Copyright (c) 2011-2019 C2FO

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 35. MIT

- `base64-js` dans exceljs 4.4.0

~~~text
The MIT License (MIT)

Copyright (c) 2014 Jameson Little

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 36. ISC

- `browserify-sign` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `minimalistic-assert` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `parse-asn1` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)
- `saxes` dans exceljs 4.4.0 : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)

~~~text
ISC License

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE.
~~~

### 37. MIT

- `safe-buffer` dans exceljs 4.4.0

~~~text
The MIT License (MIT)

Copyright (c) Feross Aboukhadijeh

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 38. MIT

- `string_decoder` dans exceljs 4.4.0
- `readable-stream` dans exceljs 4.4.0

~~~text
Node.js is licensed for use as follows:

"""
Copyright Node.js contributors. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to
deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.
"""

This license applies to parts of Node.js originating from the
https://github.com/joyent/node repository:

"""
Copyright Joyent, Inc. and other Node contributors. All rights reserved.
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to
deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.
"""
~~~

### 39. MIT

- `buffer` dans exceljs 4.4.0

~~~text
The MIT License (MIT)

Copyright (c) Feross Aboukhadijeh, and other contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
~~~

### 40. MIT

- `core-util-is` dans exceljs 4.4.0

~~~text
Copyright Node.js contributors. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to
deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.
~~~

### 41. MIT

- `dayjs` dans exceljs 4.4.0

~~~text
MIT License

Copyright (c) 2018-present, iamkun

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 42. (MIT AND Apache-2.0)

- `hash.js` dans exceljs 4.4.0, licence relevée : les auteurs du paquet ; avis à relever en ligne à la recette (F-31) (texte de la licence déclarée)

~~~text
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

- - -

Apache License

Version 2.0, January 2004

http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.

"License" shall mean the terms and conditions for use, reproduction, and distribution as defined by Sections 1 through 9 of this document.

"Licensor" shall mean the copyright owner or entity authorized by the copyright owner that is granting the License.

"Legal Entity" shall mean the union of the acting entity and all other entities that control, are controlled by, or are under common control with that entity. For the purposes of this definition, "control" means (i) the power, direct or indirect, to cause the direction or management of such entity, whether by contract or otherwise, or (ii) ownership of fifty percent (50%) or more of the outstanding shares, or (iii) beneficial ownership of such entity.

"You" (or "Your") shall mean an individual or Legal Entity exercising permissions granted by this License.

"Source" form shall mean the preferred form for making modifications, including but not limited to software source code, documentation source, and configuration files.

"Object" form shall mean any form resulting from mechanical transformation or translation of a Source form, including but not limited to compiled object code, generated documentation, and conversions to other media types.

"Work" shall mean the work of authorship, whether in Source or Object form, made available under the License, as indicated by a copyright notice that is included in or attached to the work (an example is provided in the Appendix below).

"Derivative Works" shall mean any work, whether in Source or Object form, that is based on (or derived from) the Work and for which the editorial revisions, annotations, elaborations, or other modifications represent, as a whole, an original work of authorship. For the purposes of this License, Derivative Works shall not include works that remain separable from, or merely link (or bind by name) to the interfaces of, the Work and Derivative Works thereof.

"Contribution" shall mean any work of authorship, including the original version of the Work and any modifications or additions to that Work or Derivative Works thereof, that is intentionally submitted to Licensor for inclusion in the Work by the copyright owner or by an individual or Legal Entity authorized to submit on behalf of the copyright owner. For the purposes of this definition, "submitted" means any form of electronic, verbal, or written communication sent to the Licensor or its representatives, including but not limited to communication on electronic mailing lists, source code control systems, and issue tracking systems that are managed by, or on behalf of, the Licensor for the purpose of discussing and improving the Work, but excluding communication that is conspicuously marked or otherwise designated in writing by the copyright owner as "Not a Contribution."

"Contributor" shall mean Licensor and any individual or Legal Entity on behalf of whom a Contribution has been received by Licensor and subsequently incorporated within the Work.

2. Grant of Copyright License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable copyright license to reproduce, prepare Derivative Works of, publicly display, publicly perform, sublicense, and distribute the Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as stated in this section) patent license to make, have made, use, offer to sell, sell, import, and otherwise transfer the Work, where such license applies only to those patent claims licensable by such Contributor that are necessarily infringed by their Contribution(s) alone or by combination of their Contribution(s) with the Work to which such Contribution(s) was submitted. If You institute patent litigation against any entity (including a cross-claim or counterclaim in a lawsuit) alleging that the Work or a Contribution incorporated within the Work constitutes direct or contributory patent infringement, then any patent licenses granted to You under this License for that Work shall terminate as of the date such litigation is filed.

4. Redistribution. You may reproduce and distribute copies of the Work or Derivative Works thereof in any medium, with or without modifications, and in Source or Object form, provided that You meet the following conditions:

You must give any other recipients of the Work or Derivative Works a copy of this License; and

You must cause any modified files to carry prominent notices stating that You changed the files; and

You must retain, in the Source form of any Derivative Works that You distribute, all copyright, patent, trademark, and attribution notices from the Source form of the Work, excluding those notices that do not pertain to any part of the Derivative Works; and

If the Work includes a "NOTICE" text file as part of its distribution, then any Derivative Works that You distribute must include a readable copy of the attribution notices contained within such NOTICE file, excluding those notices that do not pertain to any part of the Derivative Works, in at least one of the following places: within a NOTICE text file distributed as part of the Derivative Works; within the Source form or documentation, if provided along with the Derivative Works; or, within a display generated by the Derivative Works, if and wherever such third-party notices normally appear. The contents of the NOTICE file are for informational purposes only and do not modify the License. You may add Your own attribution notices within Derivative Works that You distribute, alongside or as an addendum to the NOTICE text from the Work, provided that such additional attribution notices cannot be construed as modifying the License. You may add Your own copyright statement to Your modifications and may provide additional or different license terms and conditions for use, reproduction, or distribution of Your modifications, or for any such Derivative Works as a whole, provided Your use, reproduction, and distribution of the Work otherwise complies with the conditions stated in this License.

5. Submission of Contributions. Unless You explicitly state otherwise, any Contribution intentionally submitted for inclusion in the Work by You to the Licensor shall be under the terms and conditions of this License, without any additional terms or conditions. Notwithstanding the above, nothing herein shall supersede or modify the terms of any separate license agreement you may have executed with Licensor regarding such Contributions.

6. Trademarks. This License does not grant permission to use the trade names, trademarks, service marks, or product names of the Licensor, except as required for reasonable and customary use in describing the origin of the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or agreed to in writing, Licensor provides the Work (and each Contributor provides its Contributions) on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied, including, without limitation, any warranties or conditions of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A PARTICULAR PURPOSE. You are solely responsible for determining the appropriateness of using or redistributing the Work and assume any risks associated with Your exercise of permissions under this License.

8. Limitation of Liability. In no event and under no legal theory, whether in tort (including negligence), contract, or otherwise, unless required by applicable law (such as deliberate and grossly negligent acts) or agreed to in writing, shall any Contributor be liable to You for damages, including any direct, indirect, special, incidental, or consequential damages of any character arising as a result of this License or out of the use or inability to use the Work (including but not limited to damages for loss of goodwill, work stoppage, computer failure or malfunction, or any and all other commercial damages or losses), even if such Contributor has been advised of the possibility of such damages.

9. Accepting Warranty or Additional Liability. While redistributing the Work or Derivative Works thereof, You may choose to offer, and charge a fee for, acceptance of support, warranty, indemnity, or other liability obligations and/or rights consistent with this License. However, in accepting such obligations, You may act only on Your own behalf and on Your sole responsibility, not on behalf of any other Contributor, and only if You agree to indemnify, defend, and hold each Contributor harmless for any liability incurred by, or claims asserted against, such Contributor by reason of your accepting any such warranty or additional liability.

END OF TERMS AND CONDITIONS
~~~

### 43. BSD-3-Clause

- `ieee754` dans exceljs 4.4.0

~~~text
Copyright 2008 Fair Oaks Labs, Inc.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
~~~

### 44. ISC

- `inherits` dans exceljs 4.4.0

~~~text
The ISC License

Copyright (c) Isaac Z. Schlueter

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE.
~~~

### 45. (MIT OR GPL-3.0-or-later)

- `jszip` dans exceljs 4.4.0

~~~text
JSZip is dual licensed. At your choice you may use it under the MIT license *or* the GPLv3
license.

The MIT License
===============

Copyright (c) 2009-2016 Stuart Knightley, David Duponchel, Franz Buchinger, António Afonso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.


GPL version 3
=============

                    GNU GENERAL PUBLIC LICENSE
                       Version 3, 29 June 2007

 Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
 Everyone is permitted to copy and distribute verbatim copies
 of this license document, but changing it is not allowed.

                            Preamble

  The GNU General Public License is a free, copyleft license for
software and other kinds of works.

  The licenses for most software and other practical works are designed
to take away your freedom to share and change the works.  By contrast,
the GNU General Public License is intended to guarantee your freedom to
share and change all versions of a program--to make sure it remains free
software for all its users.  We, the Free Software Foundation, use the
GNU General Public License for most of our software; it applies also to
any other work released this way by its authors.  You can apply it to
your programs, too.

  When we speak of free software, we are referring to freedom, not
price.  Our General Public Licenses are designed to make sure that you
have the freedom to distribute copies of free software (and charge for
them if you wish), that you receive source code or can get it if you
want it, that you can change the software or use pieces of it in new
free programs, and that you know you can do these things.

  To protect your rights, we need to prevent others from denying you
these rights or asking you to surrender the rights.  Therefore, you have
certain responsibilities if you distribute copies of the software, or if
you modify it: responsibilities to respect the freedom of others.

  For example, if you distribute copies of such a program, whether
gratis or for a fee, you must pass on to the recipients the same
freedoms that you received.  You must make sure that they, too, receive
or can get the source code.  And you must show them these terms so they
know their rights.

  Developers that use the GNU GPL protect your rights with two steps:
(1) assert copyright on the software, and (2) offer you this License
giving you legal permission to copy, distribute and/or modify it.

  For the developers' and authors' protection, the GPL clearly explains
that there is no warranty for this free software.  For both users' and
authors' sake, the GPL requires that modified versions be marked as
changed, so that their problems will not be attributed erroneously to
authors of previous versions.

  Some devices are designed to deny users access to install or run
modified versions of the software inside them, although the manufacturer
can do so.  This is fundamentally incompatible with the aim of
protecting users' freedom to change the software.  The systematic
pattern of such abuse occurs in the area of products for individuals to
use, which is precisely where it is most unacceptable.  Therefore, we
have designed this version of the GPL to prohibit the practice for those
products.  If such problems arise substantially in other domains, we
stand ready to extend this provision to those domains in future versions
of the GPL, as needed to protect the freedom of users.

  Finally, every program is threatened constantly by software patents.
States should not allow patents to restrict development and use of
software on general-purpose computers, but in those that do, we wish to
avoid the special danger that patents applied to a free program could
make it effectively proprietary.  To prevent this, the GPL assures that
patents cannot be used to render the program non-free.

  The precise terms and conditions for copying, distribution and
modification follow.

                       TERMS AND CONDITIONS

  0. Definitions.

  "This License" refers to version 3 of the GNU General Public License.

  "Copyright" also means copyright-like laws that apply to other kinds of
works, such as semiconductor masks.

  "The Program" refers to any copyrightable work licensed under this
License.  Each licensee is addressed as "you".  "Licensees" and
"recipients" may be individuals or organizations.

  To "modify" a work means to copy from or adapt all or part of the work
in a fashion requiring copyright permission, other than the making of an
exact copy.  The resulting work is called a "modified version" of the
earlier work or a work "based on" the earlier work.

  A "covered work" means either the unmodified Program or a work based
on the Program.

  To "propagate" a work means to do anything with it that, without
permission, would make you directly or secondarily liable for
infringement under applicable copyright law, except executing it on a
computer or modifying a private copy.  Propagation includes copying,
distribution (with or without modification), making available to the
public, and in some countries other activities as well.

  To "convey" a work means any kind of propagation that enables other
parties to make or receive copies.  Mere interaction with a user through
a computer network, with no transfer of a copy, is not conveying.

  An interactive user interface displays "Appropriate Legal Notices"
to the extent that it includes a convenient and prominently visible
feature that (1) displays an appropriate copyright notice, and (2)
tells the user that there is no warranty for the work (except to the
extent that warranties are provided), that licensees may convey the
work under this License, and how to view a copy of this License.  If
the interface presents a list of user commands or options, such as a
menu, a prominent item in the list meets this criterion.

  1. Source Code.

  The "source code" for a work means the preferred form of the work
for making modifications to it.  "Object code" means any non-source
form of a work.

  A "Standard Interface" means an interface that either is an official
standard defined by a recognized standards body, or, in the case of
interfaces specified for a particular programming language, one that
is widely used among developers working in that language.

  The "System Libraries" of an executable work include anything, other
than the work as a whole, that (a) is included in the normal form of
packaging a Major Component, but which is not part of that Major
Component, and (b) serves only to enable use of the work with that
Major Component, or to implement a Standard Interface for which an
implementation is available to the public in source code form.  A
"Major Component", in this context, means a major essential component
(kernel, window system, and so on) of the specific operating system
(if any) on which the executable work runs, or a compiler used to
produce the work, or an object code interpreter used to run it.

  The "Corresponding Source" for a work in object code form means all
the source code needed to generate, install, and (for an executable
work) run the object code and to modify the work, including scripts to
control those activities.  However, it does not include the work's
System Libraries, or general-purpose tools or generally available free
programs which are used unmodified in performing those activities but
which are not part of the work.  For example, Corresponding Source
includes interface definition files associated with source files for
the work, and the source code for shared libraries and dynamically
linked subprograms that the work is specifically designed to require,
such as by intimate data communication or control flow between those
subprograms and other parts of the work.

  The Corresponding Source need not include anything that users
can regenerate automatically from other parts of the Corresponding
Source.

  The Corresponding Source for a work in source code form is that
same work.

  2. Basic Permissions.

  All rights granted under this License are granted for the term of
copyright on the Program, and are irrevocable provided the stated
conditions are met.  This License explicitly affirms your unlimited
permission to run the unmodified Program.  The output from running a
covered work is covered by this License only if the output, given its
content, constitutes a covered work.  This License acknowledges your
rights of fair use or other equivalent, as provided by copyright law.

  You may make, run and propagate covered works that you do not
convey, without conditions so long as your license otherwise remains
in force.  You may convey covered works to others for the sole purpose
of having them make modifications exclusively for you, or provide you
with facilities for running those works, provided that you comply with
the terms of this License in conveying all material for which you do
not control copyright.  Those thus making or running the covered works
for you must do so exclusively on your behalf, under your direction
and control, on terms that prohibit them from making any copies of
your copyrighted material outside their relationship with you.

  Conveying under any other circumstances is permitted solely under
the conditions stated below.  Sublicensing is not allowed; section 10
makes it unnecessary.

  3. Protecting Users' Legal Rights From Anti-Circumvention Law.

  No covered work shall be deemed part of an effective technological
measure under any applicable law fulfilling obligations under article
11 of the WIPO copyright treaty adopted on 20 December 1996, or
similar laws prohibiting or restricting circumvention of such
measures.

  When you convey a covered work, you waive any legal power to forbid
circumvention of technological measures to the extent such circumvention
is effected by exercising rights under this License with respect to
the covered work, and you disclaim any intention to limit operation or
modification of the work as a means of enforcing, against the work's
users, your or third parties' legal rights to forbid circumvention of
technological measures.

  4. Conveying Verbatim Copies.

  You may convey verbatim copies of the Program's source code as you
receive it, in any medium, provided that you conspicuously and
appropriately publish on each copy an appropriate copyright notice;
keep intact all notices stating that this License and any
non-permissive terms added in accord with section 7 apply to the code;
keep intact all notices of the absence of any warranty; and give all
recipients a copy of this License along with the Program.

  You may charge any price or no price for each copy that you convey,
and you may offer support or warranty protection for a fee.

  5. Conveying Modified Source Versions.

  You may convey a work based on the Program, or the modifications to
produce it from the Program, in the form of source code under the
terms of section 4, provided that you also meet all of these conditions:

    a) The work must carry prominent notices stating that you modified
    it, and giving a relevant date.

    b) The work must carry prominent notices stating that it is
    released under this License and any conditions added under section
    7.  This requirement modifies the requirement in section 4 to
    "keep intact all notices".

    c) You must license the entire work, as a whole, under this
    License to anyone who comes into possession of a copy.  This
    License will therefore apply, along with any applicable section 7
    additional terms, to the whole of the work, and all its parts,
    regardless of how they are packaged.  This License gives no
    permission to license the work in any other way, but it does not
    invalidate such permission if you have separately received it.

    d) If the work has interactive user interfaces, each must display
    Appropriate Legal Notices; however, if the Program has interactive
    interfaces that do not display Appropriate Legal Notices, your
    work need not make them do so.

  A compilation of a covered work with other separate and independent
works, which are not by their nature extensions of the covered work,
and which are not combined with it such as to form a larger program,
in or on a volume of a storage or distribution medium, is called an
"aggregate" if the compilation and its resulting copyright are not
used to limit the access or legal rights of the compilation's users
beyond what the individual works permit.  Inclusion of a covered work
in an aggregate does not cause this License to apply to the other
parts of the aggregate.

  6. Conveying Non-Source Forms.

  You may convey a covered work in object code form under the terms
of sections 4 and 5, provided that you also convey the
machine-readable Corresponding Source under the terms of this License,
in one of these ways:

    a) Convey the object code in, or embodied in, a physical product
    (including a physical distribution medium), accompanied by the
    Corresponding Source fixed on a durable physical medium
    customarily used for software interchange.

    b) Convey the object code in, or embodied in, a physical product
    (including a physical distribution medium), accompanied by a
    written offer, valid for at least three years and valid for as
    long as you offer spare parts or customer support for that product
    model, to give anyone who possesses the object code either (1) a
    copy of the Corresponding Source for all the software in the
    product that is covered by this License, on a durable physical
    medium customarily used for software interchange, for a price no
    more than your reasonable cost of physically performing this
    conveying of source, or (2) access to copy the
    Corresponding Source from a network server at no charge.

    c) Convey individual copies of the object code with a copy of the
    written offer to provide the Corresponding Source.  This
    alternative is allowed only occasionally and noncommercially, and
    only if you received the object code with such an offer, in accord
    with subsection 6b.

    d) Convey the object code by offering access from a designated
    place (gratis or for a charge), and offer equivalent access to the
    Corresponding Source in the same way through the same place at no
    further charge.  You need not require recipients to copy the
    Corresponding Source along with the object code.  If the place to
    copy the object code is a network server, the Corresponding Source
    may be on a different server (operated by you or a third party)
    that supports equivalent copying facilities, provided you maintain
    clear directions next to the object code saying where to find the
    Corresponding Source.  Regardless of what server hosts the
    Corresponding Source, you remain obligated to ensure that it is
    available for as long as needed to satisfy these requirements.

    e) Convey the object code using peer-to-peer transmission, provided
    you inform other peers where the object code and Corresponding
    Source of the work are being offered to the general public at no
    charge under subsection 6d.

  A separable portion of the object code, whose source code is excluded
from the Corresponding Source as a System Library, need not be
included in conveying the object code work.

  A "User Product" is either (1) a "consumer product", which means any
tangible personal property which is normally used for personal, family,
or household purposes, or (2) anything designed or sold for incorporation
into a dwelling.  In determining whether a product is a consumer product,
doubtful cases shall be resolved in favor of coverage.  For a particular
product received by a particular user, "normally used" refers to a
typical or common use of that class of product, regardless of the status
of the particular user or of the way in which the particular user
actually uses, or expects or is expected to use, the product.  A product
is a consumer product regardless of whether the product has substantial
commercial, industrial or non-consumer uses, unless such uses represent
the only significant mode of use of the product.

  "Installation Information" for a User Product means any methods,
procedures, authorization keys, or other information required to install
and execute modified versions of a covered work in that User Product from
a modified version of its Corresponding Source.  The information must
suffice to ensure that the continued functioning of the modified object
code is in no case prevented or interfered with solely because
modification has been made.

  If you convey an object code work under this section in, or with, or
specifically for use in, a User Product, and the conveying occurs as
part of a transaction in which the right of possession and use of the
User Product is transferred to the recipient in perpetuity or for a
fixed term (regardless of how the transaction is characterized), the
Corresponding Source conveyed under this section must be accompanied
by the Installation Information.  But this requirement does not apply
if neither you nor any third party retains the ability to install
modified object code on the User Product (for example, the work has
been installed in ROM).

  The requirement to provide Installation Information does not include a
requirement to continue to provide support service, warranty, or updates
for a work that has been modified or installed by the recipient, or for
the User Product in which it has been modified or installed.  Access to a
network may be denied when the modification itself materially and
adversely affects the operation of the network or violates the rules and
protocols for communication across the network.

  Corresponding Source conveyed, and Installation Information provided,
in accord with this section must be in a format that is publicly
documented (and with an implementation available to the public in
source code form), and must require no special password or key for
unpacking, reading or copying.

  7. Additional Terms.

  "Additional permissions" are terms that supplement the terms of this
License by making exceptions from one or more of its conditions.
Additional permissions that are applicable to the entire Program shall
be treated as though they were included in this License, to the extent
that they are valid under applicable law.  If additional permissions
apply only to part of the Program, that part may be used separately
under those permissions, but the entire Program remains governed by
this License without regard to the additional permissions.

  When you convey a copy of a covered work, you may at your option
remove any additional permissions from that copy, or from any part of
it.  (Additional permissions may be written to require their own
removal in certain cases when you modify the work.)  You may place
additional permissions on material, added by you to a covered work,
for which you have or can give appropriate copyright permission.

  Notwithstanding any other provision of this License, for material you
add to a covered work, you may (if authorized by the copyright holders of
that material) supplement the terms of this License with terms:

    a) Disclaiming warranty or limiting liability differently from the
    terms of sections 15 and 16 of this License; or

    b) Requiring preservation of specified reasonable legal notices or
    author attributions in that material or in the Appropriate Legal
    Notices displayed by works containing it; or

    c) Prohibiting misrepresentation of the origin of that material, or
    requiring that modified versions of such material be marked in
    reasonable ways as different from the original version; or

    d) Limiting the use for publicity purposes of names of licensors or
    authors of the material; or

    e) Declining to grant rights under trademark law for use of some
    trade names, trademarks, or service marks; or

    f) Requiring indemnification of licensors and authors of that
    material by anyone who conveys the material (or modified versions of
    it) with contractual assumptions of liability to the recipient, for
    any liability that these contractual assumptions directly impose on
    those licensors and authors.

  All other non-permissive additional terms are considered "further
restrictions" within the meaning of section 10.  If the Program as you
received it, or any part of it, contains a notice stating that it is
governed by this License along with a term that is a further
restriction, you may remove that term.  If a license document contains
a further restriction but permits relicensing or conveying under this
License, you may add to a covered work material governed by the terms
of that license document, provided that the further restriction does
not survive such relicensing or conveying.

  If you add terms to a covered work in accord with this section, you
must place, in the relevant source files, a statement of the
additional terms that apply to those files, or a notice indicating
where to find the applicable terms.

  Additional terms, permissive or non-permissive, may be stated in the
form of a separately written license, or stated as exceptions;
the above requirements apply either way.

  8. Termination.

  You may not propagate or modify a covered work except as expressly
provided under this License.  Any attempt otherwise to propagate or
modify it is void, and will automatically terminate your rights under
this License (including any patent licenses granted under the third
paragraph of section 11).

  However, if you cease all violation of this License, then your
license from a particular copyright holder is reinstated (a)
provisionally, unless and until the copyright holder explicitly and
finally terminates your license, and (b) permanently, if the copyright
holder fails to notify you of the violation by some reasonable means
prior to 60 days after the cessation.

  Moreover, your license from a particular copyright holder is
reinstated permanently if the copyright holder notifies you of the
violation by some reasonable means, this is the first time you have
received notice of violation of this License (for any work) from that
copyright holder, and you cure the violation prior to 30 days after
your receipt of the notice.

  Termination of your rights under this section does not terminate the
licenses of parties who have received copies or rights from you under
this License.  If your rights have been terminated and not permanently
reinstated, you do not qualify to receive new licenses for the same
material under section 10.

  9. Acceptance Not Required for Having Copies.

  You are not required to accept this License in order to receive or
run a copy of the Program.  Ancillary propagation of a covered work
occurring solely as a consequence of using peer-to-peer transmission
to receive a copy likewise does not require acceptance.  However,
nothing other than this License grants you permission to propagate or
modify any covered work.  These actions infringe copyright if you do
not accept this License.  Therefore, by modifying or propagating a
covered work, you indicate your acceptance of this License to do so.

  10. Automatic Licensing of Downstream Recipients.

  Each time you convey a covered work, the recipient automatically
receives a license from the original licensors, to run, modify and
propagate that work, subject to this License.  You are not responsible
for enforcing compliance by third parties with this License.

  An "entity transaction" is a transaction transferring control of an
organization, or substantially all assets of one, or subdividing an
organization, or merging organizations.  If propagation of a covered
work results from an entity transaction, each party to that
transaction who receives a copy of the work also receives whatever
licenses to the work the party's predecessor in interest had or could
give under the previous paragraph, plus a right to possession of the
Corresponding Source of the work from the predecessor in interest, if
the predecessor has it or can get it with reasonable efforts.

  You may not impose any further restrictions on the exercise of the
rights granted or affirmed under this License.  For example, you may
not impose a license fee, royalty, or other charge for exercise of
rights granted under this License, and you may not initiate litigation
(including a cross-claim or counterclaim in a lawsuit) alleging that
any patent claim is infringed by making, using, selling, offering for
sale, or importing the Program or any portion of it.

  11. Patents.

  A "contributor" is a copyright holder who authorizes use under this
License of the Program or a work on which the Program is based.  The
work thus licensed is called the contributor's "contributor version".

  A contributor's "essential patent claims" are all patent claims
owned or controlled by the contributor, whether already acquired or
hereafter acquired, that would be infringed by some manner, permitted
by this License, of making, using, or selling its contributor version,
but do not include claims that would be infringed only as a
consequence of further modification of the contributor version.  For
purposes of this definition, "control" includes the right to grant
patent sublicenses in a manner consistent with the requirements of
this License.

  Each contributor grants you a non-exclusive, worldwide, royalty-free
patent license under the contributor's essential patent claims, to
make, use, sell, offer for sale, import and otherwise run, modify and
propagate the contents of its contributor version.

  In the following three paragraphs, a "patent license" is any express
agreement or commitment, however denominated, not to enforce a patent
(such as an express permission to practice a patent or covenant not to
sue for patent infringement).  To "grant" such a patent license to a
party means to make such an agreement or commitment not to enforce a
patent against the party.

  If you convey a covered work, knowingly relying on a patent license,
and the Corresponding Source of the work is not available for anyone
to copy, free of charge and under the terms of this License, through a
publicly available network server or other readily accessible means,
then you must either (1) cause the Corresponding Source to be so
available, or (2) arrange to deprive yourself of the benefit of the
patent license for this particular work, or (3) arrange, in a manner
consistent with the requirements of this License, to extend the patent
license to downstream recipients.  "Knowingly relying" means you have
actual knowledge that, but for the patent license, your conveying the
covered work in a country, or your recipient's use of the covered work
in a country, would infringe one or more identifiable patents in that
country that you have reason to believe are valid.

  If, pursuant to or in connection with a single transaction or
arrangement, you convey, or propagate by procuring conveyance of, a
covered work, and grant a patent license to some of the parties
receiving the covered work authorizing them to use, propagate, modify
or convey a specific copy of the covered work, then the patent license
you grant is automatically extended to all recipients of the covered
work and works based on it.

  A patent license is "discriminatory" if it does not include within
the scope of its coverage, prohibits the exercise of, or is
conditioned on the non-exercise of one or more of the rights that are
specifically granted under this License.  You may not convey a covered
work if you are a party to an arrangement with a third party that is
in the business of distributing software, under which you make payment
to the third party based on the extent of your activity of conveying
the work, and under which the third party grants, to any of the
parties who would receive the covered work from you, a discriminatory
patent license (a) in connection with copies of the covered work
conveyed by you (or copies made from those copies), or (b) primarily
for and in connection with specific products or compilations that
contain the covered work, unless you entered into that arrangement,
or that patent license was granted, prior to 28 March 2007.

  Nothing in this License shall be construed as excluding or limiting
any implied license or other defenses to infringement that may
otherwise be available to you under applicable patent law.

  12. No Surrender of Others' Freedom.

  If conditions are imposed on you (whether by court order, agreement or
otherwise) that contradict the conditions of this License, they do not
excuse you from the conditions of this License.  If you cannot convey a
covered work so as to satisfy simultaneously your obligations under this
License and any other pertinent obligations, then as a consequence you may
not convey it at all.  For example, if you agree to terms that obligate you
to collect a royalty for further conveying from those to whom you convey
the Program, the only way you could satisfy both those terms and this
License would be to refrain entirely from conveying the Program.

  13. Use with the GNU Affero General Public License.

  Notwithstanding any other provision of this License, you have
permission to link or combine any covered work with a work licensed
under version 3 of the GNU Affero General Public License into a single
combined work, and to convey the resulting work.  The terms of this
License will continue to apply to the part which is the covered work,
but the special requirements of the GNU Affero General Public License,
section 13, concerning interaction through a network will apply to the
combination as such.

  14. Revised Versions of this License.

  The Free Software Foundation may publish revised and/or new versions of
the GNU General Public License from time to time.  Such new versions will
be similar in spirit to the present version, but may differ in detail to
address new problems or concerns.

  Each version is given a distinguishing version number.  If the
Program specifies that a certain numbered version of the GNU General
Public License "or any later version" applies to it, you have the
option of following the terms and conditions either of that numbered
version or of any later version published by the Free Software
Foundation.  If the Program does not specify a version number of the
GNU General Public License, you may choose any version ever published
by the Free Software Foundation.

  If the Program specifies that a proxy can decide which future
versions of the GNU General Public License can be used, that proxy's
public statement of acceptance of a version permanently authorizes you
to choose that version for the Program.

  Later license versions may give you additional or different
permissions.  However, no additional obligations are imposed on any
author or copyright holder as a result of your choosing to follow a
later version.

  15. Disclaimer of Warranty.

  THERE IS NO WARRANTY FOR THE PROGRAM, TO THE EXTENT PERMITTED BY
APPLICABLE LAW.  EXCEPT WHEN OTHERWISE STATED IN WRITING THE COPYRIGHT
HOLDERS AND/OR OTHER PARTIES PROVIDE THE PROGRAM "AS IS" WITHOUT WARRANTY
OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO,
THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
PURPOSE.  THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE PROGRAM
IS WITH YOU.  SHOULD THE PROGRAM PROVE DEFECTIVE, YOU ASSUME THE COST OF
ALL NECESSARY SERVICING, REPAIR OR CORRECTION.

  16. Limitation of Liability.

  IN NO EVENT UNLESS REQUIRED BY APPLICABLE LAW OR AGREED TO IN WRITING
WILL ANY COPYRIGHT HOLDER, OR ANY OTHER PARTY WHO MODIFIES AND/OR CONVEYS
THE PROGRAM AS PERMITTED ABOVE, BE LIABLE TO YOU FOR DAMAGES, INCLUDING ANY
GENERAL, SPECIAL, INCIDENTAL OR CONSEQUENTIAL DAMAGES ARISING OUT OF THE
USE OR INABILITY TO USE THE PROGRAM (INCLUDING BUT NOT LIMITED TO LOSS OF
DATA OR DATA BEING RENDERED INACCURATE OR LOSSES SUSTAINED BY YOU OR THIRD
PARTIES OR A FAILURE OF THE PROGRAM TO OPERATE WITH ANY OTHER PROGRAMS),
EVEN IF SUCH HOLDER OR OTHER PARTY HAS BEEN ADVISED OF THE POSSIBILITY OF
SUCH DAMAGES.

  17. Interpretation of Sections 15 and 16.

  If the disclaimer of warranty and limitation of liability provided
above cannot be given local legal effect according to their terms,
reviewing courts shall apply local law that most closely approximates
an absolute waiver of all civil liability in connection with the
Program, unless a warranty or assumption of liability accompanies a
copy of the Program in return for a fee.

                     END OF TERMS AND CONDITIONS
~~~

### 46. MIT

- `lodash.escaperegexp` dans exceljs 4.4.0
- `lodash.groupby` dans exceljs 4.4.0
- `lodash.uniq` dans exceljs 4.4.0

~~~text
Copyright jQuery Foundation and other contributors <https://jquery.org/>

Based on Underscore.js, copyright Jeremy Ashkenas,
DocumentCloud and Investigative Reporters & Editors <http://underscorejs.org/>

This software consists of voluntary contributions made by many
individuals. For exact contribution history, see the revision history
available at https://github.com/lodash/lodash

The following license applies to all parts of this software except as
documented below:

====

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

====

Copyright and related rights for sample code are waived via CC0. Sample
code is defined as all source code displayed within the prose of the
documentation.

CC0: http://creativecommons.org/publicdomain/zero/1.0/

====

Files located in the node_modules and vendor directories are externally
maintained libraries used by this software which have their own
licenses; we recommend you read them, as their terms may differ from the
terms above.
~~~

### 47. MIT

- `lodash.isboolean` dans exceljs 4.4.0
- `lodash.isnil` dans exceljs 4.4.0

~~~text
Copyright 2012-2016 The Dojo Foundation <http://dojofoundation.org/>
Based on Underscore.js, copyright 2009-2016 Jeremy Ashkenas,
DocumentCloud and Investigative Reporters & Editors <http://underscorejs.org/>

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 48. MIT

- `lodash.isequal` dans exceljs 4.4.0
- `lodash.isfunction` dans exceljs 4.4.0

~~~text
Copyright JS Foundation and other contributors <https://js.foundation/>

Based on Underscore.js, copyright Jeremy Ashkenas,
DocumentCloud and Investigative Reporters & Editors <http://underscorejs.org/>

This software consists of voluntary contributions made by many
individuals. For exact contribution history, see the revision history
available at https://github.com/lodash/lodash

The following license applies to all parts of this software except as
documented below:

====

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

====

Copyright and related rights for sample code are waived via CC0. Sample
code is defined as all source code displayed within the prose of the
documentation.

CC0: http://creativecommons.org/publicdomain/zero/1.0/

====

Files located in the node_modules and vendor directories are externally
maintained libraries used by this software which have their own
licenses; we recommend you read them, as their terms may differ from the
terms above.
~~~

### 49. MIT

- `lodash.isundefined` dans exceljs 4.4.0

~~~text
Copyright 2012-2015 The Dojo Foundation <http://dojofoundation.org/>
Based on Underscore.js, copyright 2009-2015 Jeremy Ashkenas,
DocumentCloud and Investigative Reporters & Editors <http://underscorejs.org/>

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 50. MIT

- `process-nextick-args` dans exceljs 4.4.0

~~~text
# Copyright (c) 2015 Calvin Metcalf

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

**THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.**
~~~

### 51. MIT

- `regenerator-runtime` dans exceljs 4.4.0

~~~text
MIT License

Copyright (c) 2014-present, Facebook, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 52. MIT

- `safer-buffer` dans exceljs 4.4.0

~~~text
MIT License

Copyright (c) 2018 Nikita Skovoroda <chalkerx@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
~~~

### 53. (MIT AND BSD-3-Clause)

- `sha.js` dans exceljs 4.4.0, licence relevée : Version 2.1a Copyright Paul Johnston 2000 - 2002. ; Version 2.2-beta Copyright Angel Marin, Paul Johnston 2000 - 2009. (texte de la licence déclarée)

~~~text
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

- - -

BSD 3-Clause License

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
~~~

### 54. MIT

- `util-deprecate` dans exceljs 4.4.0

~~~text
(The MIT License)

Copyright (c) 2014 Nathan Rajlich <nathan@tootallnate.net>

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
~~~

### 55. (MIT AND BSD-3-Clause)

- `uuid` dans exceljs 4.4.0

~~~text
The MIT License (MIT)

Copyright (c) 2010-2020 Robert Kieffer and other contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

- - -

BSD 3-Clause License

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
~~~

### 56. MIT

- `xmlchars` dans exceljs 4.4.0

~~~text
Copyright Louis-Dominique Dubeau and contributors to xmlchars

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 57. MIT

- `lie` dans exceljs 4.4.0, par jszip

~~~text
#Copyright (c) 2014-2018 Calvin Metcalf, Jordan Harband

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

**THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.**
~~~

### 58. MIT

- `immediate` dans exceljs 4.4.0, par jszip

~~~text
Copyright (c) 2012 Barnesandnoble.com, llc, Donavon West, Domenic Denicola, Brian Cavalier

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~

### 59. MIT

- `setimmediate` dans exceljs 4.4.0, par jszip

~~~text
Copyright (c) 2012 Barnesandnoble.com, llc, Donavon West, and Domenic Denicola

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
~~~
