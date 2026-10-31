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
| NuGet | `Microsoft.EntityFrameworkCore.Analyzers` | 10.0.13 | MIT | livré |
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
| npm | `@bramus/specificity` | 2.4.3 | MIT | développement |
| npm | `@cacheable/memory` | 2.2.0 | MIT | développement |
| npm | `@cacheable/utils` | 2.5.0 | MIT | développement |
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
| npm | `@jridgewell/gen-mapping` | 0.3.13 | MIT | développement |
| npm | `@jridgewell/remapping` | 2.3.5 | MIT | développement |
| npm | `@jridgewell/resolve-uri` | 3.1.2 | MIT | développement |
| npm | `@jridgewell/sourcemap-codec` | 1.6.0 | MIT | développement |
| npm | `@jridgewell/trace-mapping` | 0.3.31 | MIT | développement |
| npm | `@keyv/bigmap` | 1.3.1 | MIT | développement |
| npm | `@keyv/serialize` | 1.1.1 | MIT | développement |
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
| npm | `@types/deep-eql` | 4.0.2 | MIT | développement |
| npm | `@types/esrecurse` | 4.3.1 | MIT | développement |
| npm | `@types/estree` | 1.0.9 | MIT | développement |
| npm | `@types/json-schema` | 7.0.15 | MIT | développement |
| npm | `@types/node` | 26.6.5 | MIT | développement |
| npm | `@types/react` | 19.3.0 | MIT | développement |
| npm | `@types/react-dom` | 19.3.0 | MIT | développement |
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
| npm | `brace-expansion` | 2.1.7 | MIT | développement |
| npm | `brace-expansion` | 5.0.12 | MIT | développement |
| npm | `browserslist` | 4.29.4 | MIT | développement |
| npm | `cacheable` | 2.5.0 | MIT | développement |
| npm | `caniuse-lite` | 1.0.30001819 | CC-BY-4.0 | développement |
| npm | `chai` | 6.3.0 | MIT | développement |
| npm | `change-case` | 5.4.4 | MIT | développement |
| npm | `colorette` | 1.4.0 | MIT | développement |
| npm | `convert-source-map` | 2.0.0 | MIT | développement |
| npm | `cross-spawn` | 7.0.6 | MIT | développement |
| npm | `css-tree` | 3.2.1 | MIT | développement |
| npm | `css.escape` | 1.5.1 | MIT | développement |
| npm | `csstype` | 3.2.3 | MIT | développement |
| npm | `data-urls` | 8.0.0 | MIT | développement |
| npm | `debug` | 4.4.3 | MIT | développement |
| npm | `decimal.js` | 10.6.0 | MIT | développement |
| npm | `deep-is` | 0.1.4 | MIT | développement |
| npm | `dequal` | 2.0.3 | MIT | développement |
| npm | `detect-libc` | 2.1.2 | Apache-2.0 | développement |
| npm | `dom-accessibility-api` | 0.5.16 | MIT | développement |
| npm | `dom-accessibility-api` | 0.6.3 | MIT | développement |
| npm | `electron-to-chromium` | 1.5.463 | ISC | développement |
| npm | `entities` | 8.1.0 | BSD-2-Clause | développement |
| npm | `es-module-lexer` | 2.3.2 | MIT | développement |
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
| npm | `fdir` | 6.5.0 | MIT | développement |
| npm | `file-entry-cache` | 11.1.5 | MIT | développement |
| npm | `find-up` | 5.0.0 | MIT | développement |
| npm | `flat-cache` | 6.1.23 | MIT | développement |
| npm | `flatted` | 3.4.4 | ISC | développement |
| npm | `fsevents` | 2.3.3 | MIT | développement |
| npm | `gensync` | 1.0.0-beta.2 | MIT | développement |
| npm | `glob-parent` | 6.0.2 | ISC | développement |
| npm | `globals` | 17.13.0 | MIT | développement |
| npm | `hashery` | 1.5.1 | MIT | développement |
| npm | `hermes-estree` | 0.25.1 | MIT | développement |
| npm | `hermes-parser` | 0.25.1 | MIT | développement |
| npm | `hookified` | 1.15.1 | MIT | développement |
| npm | `hookified` | 2.2.0 | MIT | développement |
| npm | `html-encoding-sniffer` | 7.0.0 | MIT | développement |
| npm | `https-proxy-agent` | 7.0.6 | MIT | développement |
| npm | `ignore` | 5.3.2 | MIT | développement |
| npm | `ignore` | 7.0.13 | MIT | développement |
| npm | `imurmurhash` | 0.1.4 | MIT | développement |
| npm | `indent-string` | 4.0.0 | MIT | développement |
| npm | `index-to-position` | 1.2.0 | MIT | développement |
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
| npm | `keyv` | 5.6.0 | MIT | développement |
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
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 11.5.3 | BlueOak-1.0.0 | développement |
| npm | `lru-cache` | 5.1.1 | ISC | développement |
| npm | `lz-string` | 1.5.0 | MIT | développement |
| npm | `magic-string` | 1.4.3 | MIT | développement |
| npm | `mdn-data` | 2.27.1 | CC0-1.0 | développement |
| npm | `min-indent` | 1.0.1 | MIT | développement |
| npm | `minimatch` | 10.2.6 | BlueOak-1.0.0 | développement |
| npm | `minimatch` | 5.1.9 | ISC | développement |
| npm | `ms` | 2.1.3 | MIT | développement |
| npm | `nanoid` | 3.3.20 | MIT | développement |
| npm | `natural-compare` | 1.4.0 | MIT | développement |
| npm | `node-releases` | 2.0.60 | MIT | développement |
| npm | `obug` | 3.0.0 | MIT | développement |
| npm | `openapi-typescript` | 7.13.0 | MIT | développement |
| npm | `optionator` | 0.9.4 | MIT | développement |
| npm | `p-limit` | 3.1.0 | MIT | développement |
| npm | `p-locate` | 5.0.0 | MIT | développement |
| npm | `parse-json` | 8.3.0 | MIT | développement |
| npm | `parse5` | 8.0.1 | MIT | développement |
| npm | `path-exists` | 4.0.0 | MIT | développement |
| npm | `path-key` | 3.1.1 | MIT | développement |
| npm | `picocolors` | 1.1.1 | ISC | développement |
| npm | `picomatch` | 4.0.7 | MIT | développement |
| npm | `playwright` | 1.64.1 | Apache-2.0 | développement |
| npm | `playwright-core` | 1.64.1 | Apache-2.0 | développement |
| npm | `pluralize` | 8.0.0 | MIT | développement |
| npm | `postcss` | 8.5.31 | MIT | développement |
| npm | `prelude-ls` | 1.2.1 | MIT | développement |
| npm | `pretty-format` | 27.5.1 | MIT | développement |
| npm | `punycode` | 2.3.1 | MIT | développement |
| npm | `qified` | 0.10.1 | MIT | développement |
| npm | `react-is` | 17.0.2 | MIT | développement |
| npm | `redent` | 3.0.0 | MIT | développement |
| npm | `require-from-string` | 2.0.2 | MIT | développement |
| npm | `rolldown` | 1.2.15 | MIT | développement |
| npm | `saxes` | 6.0.0 | ISC | développement |
| npm | `semver` | 6.3.1 | ISC | développement |
| npm | `semver` | 7.8.5 | ISC | développement |
| npm | `shebang-command` | 2.0.0 | MIT | développement |
| npm | `shebang-regex` | 3.0.0 | MIT | développement |
| npm | `source-map-js` | 1.2.2 | BSD-3-Clause | développement |
| npm | `std-env` | 4.3.0 | MIT | développement |
| npm | `strip-indent` | 3.0.0 | MIT | développement |
| npm | `supports-color` | 10.2.2 | MIT | développement |
| npm | `tinybench` | 6.2.2 | MIT | développement |
| npm | `tinyexec` | 1.3.1 | MIT | développement |
| npm | `tinyglobby` | 0.2.17 | MIT | développement |
| npm | `tldts` | 7.4.19 | MIT | développement |
| npm | `tldts-core` | 7.4.19 | MIT | développement |
| npm | `tough-cookie` | 6.0.2 | BSD-3-Clause | développement |
| npm | `tr46` | 7.0.0 | MIT | développement |
| npm | `ts-api-utils` | 2.5.0 | MIT | développement |
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
| npm | `d3-color` | 3.1.0 | ISC | livré |
| npm | `d3-dispatch` | 3.0.1 | ISC | livré |
| npm | `d3-drag` | 3.0.0 | ISC | livré |
| npm | `d3-ease` | 3.0.1 | BSD-3-Clause | livré |
| npm | `d3-interpolate` | 3.0.1 | ISC | livré |
| npm | `d3-selection` | 3.0.0 | ISC | livré |
| npm | `d3-timer` | 3.0.1 | ISC | livré |
| npm | `d3-transition` | 3.0.1 | ISC | livré |
| npm | `d3-zoom` | 3.0.0 | ISC | livré |
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
| npm | `@types/trusted-types` | 2.0.7 | MIT | non servi |
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
| npm | `elliptic` | dans exceljs 4.4.0 | MIT | livré |
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
| npm | `safer-buffer` | dans exceljs 4.4.0, licence relevée | MIT | livré |
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
