// globals.d.ts
// The values that the build writes into the bundle (vite.config.ts `define`).

/** The version of the application, from package.json (B-0-10). */
declare const __APP_VERSION__: string;

/** The day of the release of the version, `yyyy-mm-dd` without hour, from package.json (ADR 111). */
declare const __APP_RELEASE_DATE__: string;
