// package.mjs
// Builds the ZIP of a mode, the single entry point of the packages (spec 5.4, 6.1, 6.2, ADR 048, ADR 049, ADR 071 to 074, ADR 076): the interface built, the notices checked, the publication of its profile against the package of the core, signed when a certificate is given, then the ZIP with its readme, licences and SBOM, written and read back by the SDK, its content checked, and its SHA-256. Store: the desktop mode as an MSIX package for the Microsoft Store instead of a ZIP (ADR 088).
// node build/package.mjs <Desktop|Server|Store> [--certificate <file.pfx> [--certificate-password <password>] [--timestamp-url <address>]]

import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, delimiter, join, relative } from 'node:path';
import { parseArgs } from 'node:util';
import { root } from './components.mjs';

const fail = (message) => {
  console.error(message);
  process.exit(1);
};

const USAGE = 'node build/package.mjs <Desktop|Server|Store> [--certificate <file.pfx> [--certificate-password <password>] [--timestamp-url <address>]]';
const OPTIONS = { certificate: { type: 'string' }, 'certificate-password': { type: 'string' }, 'timestamp-url': { type: 'string' } };
const parsed = (() => {
  try {
    return parseArgs({ allowPositionals: true, options: OPTIONS });
  } catch (error) {
    return fail(`${error.message}\n${USAGE}`);
  }
})();
const { positionals: [mode, ...extra], values: signing } = parsed;
if (!['Desktop', 'Server', 'Store'].includes(mode) || extra.length > 0) fail(USAGE);
if (!signing.certificate && (signing['certificate-password'] || signing['timestamp-url'])) fail(`A password or a timestamp server without a certificate\n${USAGE}`);
// ADR 088: the MSIX package of the Store is the publication of the desktop mode.
const profile = mode === 'Store' ? 'Desktop' : mode;

// ADR 076: the tools this script starts, each one directly, never through a shell nor an interpreter of scripts of the
// system: node itself (TypeScript, Vite and the scripts of build/), dotnet (MSBuild, NuGet, the archives of
// build/archive.proj), and signtool of the Windows SDK when a certificate is given; for the Store (ADR 088), makepri and
// makeappx of the Windows SDK. No archiver nor hasher of Windows.
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' };

/** A tool judged on its exit code alone (a warning of Vite on its error output does not stop the build), its output shown as it comes, or kept (capture: the version of MSBuild, the list of files of makeappx). */
function run(what, command, args, options = {}) {
  const stdio = options.capture ? ['ignore', 'pipe', 'inherit'] : 'inherit';
  const result = spawnSync(command, args, { cwd: options.cwd ?? root, env, stdio, encoding: 'utf8', shell: false, windowsHide: true });
  if (result.error) fail(`${what} could not start: ${result.error.message}`);
  if (result.status !== 0) fail(`${what} failed (${result.status})`);
  return result.stdout;
}

const artifacts = join(root, 'artifacts');
const host = join(root, 'src', 'Topogenia.Host', 'Topogenia.Host.csproj');
const archiveProject = join(root, 'build', 'archive.proj');
const CORE_PACKAGE = 'Topogenia.Core';
const coreVersion = new RegExp(`<PackageVersion Include="${CORE_PACKAGE}" Version="([^"]+)"`)
  .exec(readFileSync(join(root, 'Directory.Packages.props'), 'utf8'))?.[1];
const corePackage = join(artifacts, 'core-feed', `${CORE_PACKAGE}.${coreVersion}.nupkg`);
const packCommand = 'node build/core/licence.mjs pack --key <key of the holder> --secret <secret of the marks> --archive <archive of the core> (ADR 087)';
const digest = (file) => createHash('sha256').update(readFileSync(file)).digest('hex');

/** The files under a folder, but the outputs of the builds. */
function filesOf(folder) {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (!entry.isDirectory()) return [path];
    return ['bin', 'obj', 'node_modules', 'artifacts'].includes(entry.name) ? [] : filesOf(path);
  });
}

/** Every file under a folder, by its path inside it written with forward slashes, in order. */
const namesOf = (folder) => readdirSync(folder, { recursive: true, withFileTypes: true }).filter((entry) => entry.isFile())
  .map((entry) => relative(folder, join(entry.parentPath, entry.name)).replaceAll('\\', '/')).sort();

/** Every lock file of the repository with its SHA-256: the publication must leave them as the build wrote them. */
const lockFiles = () => Object.fromEntries(filesOf(root).filter((file) => file.endsWith('packages.lock.json')).map((file) => [file, digest(file)]));

/** An archive unpacked by the SDK (the Unzip task of MSBuild) into a folder made empty. */
function unzip(archive, folder) {
  rmSync(folder, { recursive: true, force: true });
  run(`The reading of ${basename(archive)}`, 'dotnet', ['msbuild', archiveProject, '-t:Unzip', '-nologo', '-v:minimal', `-p:Archive=${archive}`, `-p:Folder=${folder}`]);
}

/** ADR 048, ADR 074: what ships carries the core from its package; where its sources are, the package is newer than each of them. */
function prepare() {
  if (!existsSync(corePackage)) fail(`The package of the core is missing (${corePackage}): ${packCommand}`);
  const packed = statSync(corePackage).mtimeMs;
  const newer = ['src/Topogenia.Domain', 'build/core'].map((folder) => join(root, folder)).filter(existsSync)
    .flatMap(filesOf).find((file) => statSync(file).mtimeMs > packed);
  if (newer) fail(`The package of the core is older than ${newer}: ${packCommand}`);
  forgetPublicationLocks();
  return lockFiles();
}

/**
 * The lock files of the last publication, in the obj folder of each project: they hold the content hash of the package
 * of the core restored then, which a package packed again changes (NuGet refuses it, NU1403); the publication writes
 * them again. The lock files of the repository are not touched.
 */
function forgetPublicationLocks() {
  for (const project of readdirSync(join(root, 'src'))) rmSync(join(root, 'src', project, 'obj', 'packages.publish.lock.json'), { force: true });
}

/** The publication carries the package of the core, never the domain built from its sources. */
function checkCore(published, deps, core, locks) {
  if (JSON.stringify(locks) !== JSON.stringify(lockFiles())) fail('The publication changed a lock file of the repository');
  const libraries = JSON.parse(readFileSync(deps, 'utf8')).libraries;
  if (libraries[`${CORE_PACKAGE}/${coreVersion}`]?.type !== 'package') fail(`The publication does not carry ${CORE_PACKAGE} ${coreVersion} (${deps})`);
  if (Object.keys(libraries).some((key) => key.startsWith('Topogenia.Domain/'))) fail(`The publication carries the core built from its sources (${deps})`);
  if (mode === 'Server' && digest(join(published, 'Topogenia.Domain.dll')) !== digest(join(core, 'lib', 'net10.0', 'Topogenia.Domain.dll'))) {
    fail('The assembly of the core in the publication is not the one of its package');
  }
}

/** Spec 0.2: nothing that ships names a folder of the machine of the build (paths of the symbols, of the sources). */
function checkPaths(published) {
  const folder = Buffer.from(root.replace(/[\\/]+$/, ''));
  const named = filesOf(published).filter((file) => readFileSync(file).includes(folder));
  if (named.length > 0) fail(`The publication names the folder of the build in ${named.join(', ')}`);
}

/** A file of build/ that ships, its product name written as {{PRODUCT}}, or {{product}} in lower case (D13). */
function copyTemplate(source, target, product) {
  const text = readFileSync(join(root, source), 'utf8').replaceAll('{{PRODUCT}}', product).replaceAll('{{product}}', product.toLowerCase());
  if (text.includes('{{')) fail(`${source} keeps a token of its own`);
  writeFileSync(target, text);
}

/** The folders of the Windows SDK that hold its tools, the newest version first. */
function kitFolders() {
  const bin = join(process.env['ProgramFiles(x86)'] ?? 'C:\\Program Files (x86)', 'Windows Kits', '10', 'bin');
  if (!existsSync(bin)) return [];
  const parts = (name) => name.split('.').map(Number);
  return readdirSync(bin).filter((name) => /^10\.0\.\d+\.\d+$/.test(name))
    .sort((a, b) => parts(b).reduce((order, part, index) => order || part - parts(a)[index], 0))
    .map((name) => join(bin, name, 'x64'));
}

/** A tool of the Windows SDK, found in the PATH, else in the newest x64 folder of the kit (ADR 088). */
function kitTool(name) {
  const tool = [...(process.env.PATH ?? '').split(delimiter).filter(Boolean), ...kitFolders()].map((folder) => join(folder, name)).find(existsSync);
  if (!tool) fail(`${name} (Windows SDK) is neither in the PATH nor in the folder of the Windows Kits`);
  return tool;
}

/**
 * ADR 088: what the MSIX package adds to the publication of the desktop mode: its manifest (the identity reserved in
 * Partner Center, the product and the version in four parts, the last one kept for the Store), the logos, and the index
 * of their scales and target sizes written by makepri.
 */
function storeFiles(stage, product, version) {
  if (!/^\d+\.\d+\.\d+$/.test(version)) fail(`The version ${version} is not of three numbers: the Store takes four, the last one 0`);
  const manifest = readFileSync(join(root, 'build', 'store', 'AppxManifest.xml'), 'utf8')
    .replaceAll('{{VERSION}}', `${version}.0`).replaceAll('{{PRODUCT}}', product);
  if (manifest.includes('{{')) fail('build/store/AppxManifest.xml keeps a token of its own');
  writeFileSync(join(stage, 'AppxManifest.xml'), manifest);
  cpSync(join(root, 'build', 'store', 'Assets'), join(stage, 'Assets'), { recursive: true });
  // makepri names a resource by its path from the folder it indexes: a folder of the logos alone, laid as in the package,
  // never the publication (its folders of languages would read as qualifiers).
  const logos = `${stage}-resources`;
  rmSync(logos, { recursive: true, force: true });
  cpSync(join(root, 'build', 'store', 'Assets'), join(logos, 'Assets'), { recursive: true });
  const index = join(stage, 'resources.pri');
  run('makepri', kitTool('makepri.exe'), ['new', '/pr', logos, '/cf', join(root, 'build', 'store', 'priconfig.xml'),
    '/mn', join(stage, 'AppxManifest.xml'), '/of', index, '/o'], { capture: true });
  checkLogos(index, manifest, join(logos, 'resources.xml'));
  rmSync(logos, { recursive: true, force: true });
}

/** Each logo the manifest names must be a resource of the index, else Windows shows its default icon in the Start menu and the search. */
function checkLogos(index, manifest, dump) {
  run('makepri dump', kitTool('makepri.exe'), ['dump', '/if', index, '/of', dump, '/dt', 'detailed', '/o'], { capture: true });
  const resources = readFileSync(dump, 'utf8');
  const named = [...new Set(manifest.match(/Assets\\[\w.]+\.png/g) ?? [])];
  if (named.length === 0) fail('The manifest of the MSIX package names no logo');
  const missing = named.filter((logo) => !resources.includes(`/Files/${logo.replaceAll('\\', '/')}"`));
  if (missing.length > 0) fail(`The index of the MSIX package does not name ${missing.join(', ')}`);
}

/** The folder of the package: the publication, its readme and its files, the licences (the bundles of the interface have no banner), the SBOM. */
function stageOf(published, deps, core, product, version) {
  const stage = join(artifacts, `${product}-${version}-${{ Desktop: 'portable', Server: 'server', Store: 'store' }[mode]}`);
  rmSync(stage, { recursive: true, force: true });
  cpSync(published, stage, { recursive: true });
  if (mode === 'Desktop') {
    copyTemplate('build/portable-readme.txt', join(stage, 'README.txt'), product);
  } else if (mode === 'Server') {
    copyTemplate('build/server/server-readme.txt', join(stage, 'README.txt'), product);
    copyTemplate('build/server/appsettings.Production.example.json', join(stage, 'appsettings.Production.example.json'), product);
    cpSync(join(root, 'docs', 'exploitation-serveur.md'), join(stage, 'exploitation-serveur.md'));
  }
  const licenses = join(stage, 'licenses');
  mkdirSync(licenses);
  cpSync(join(root, 'LICENSE'), join(licenses, 'LICENSE.txt'));
  cpSync(join(root, 'NOTICE'), join(licenses, 'NOTICE.txt'));
  cpSync(join(root, 'THIRD-PARTY-NOTICES.md'), join(licenses, 'THIRD-PARTY-NOTICES.md'));
  // The licence of the core: the one its package carries, as the core that ships.
  cpSync(join(core, 'LICENSE.md'), join(licenses, 'CORE-LICENSE.md'));
  cpSync(join(root, 'web', 'node_modules', 'inter-font', 'LICENSE.md'), join(licenses, 'Inter-OFL-1.1.md'));
  cpSync(join(root, 'web', 'node_modules', 'lucide-react', 'LICENSE'), join(licenses, 'Lucide-ISC.txt'));
  const sbom = `${stage}.cdx.json`;
  const runtime = mode === 'Server' ? join(published, `${product}.runtimeconfig.json`) : '-';
  run('The SBOM', process.execPath, [join(root, 'build', 'sbom.mjs'), mode, deps, runtime, sbom]);
  cpSync(sbom, join(stage, 'sbom.cdx.json'));
  if (mode === 'Store') storeFiles(stage, product, version);
  return stage;
}

/** What each package must carry, and never carry (no script, no symbols, no source, no key, nothing of the test bench). */
const REQUIRED = ['sbom.cdx.json', 'appsettings.json', 'wwwroot/index.html', 'licenses/LICENSE.txt', 'licenses/NOTICE.txt',
  'licenses/THIRD-PARTY-NOTICES.md', 'licenses/CORE-LICENSE.md', 'licenses/Inter-OFL-1.1.md', 'licenses/Lucide-ISC.txt'];
const REQUIRED_BY_MODE = {
  Desktop: (name) => ['README.txt', `${name}.exe`, 'e_sqlite3.dll', 'Microsoft.Data.SqlClient.SNI.dll'],
  Server: (name) => ['README.txt', `${name}.dll`, `${name}.exe`, 'Topogenia.Domain.dll', 'web.config', 'appsettings.Production.example.json', 'exploitation-serveur.md'],
  // ADR 088: no readme in the package of the Store, whose listing presents it; its manifest, its index and its logos.
  Store: (name) => [`${name}.exe`, 'e_sqlite3.dll', 'Microsoft.Data.SqlClient.SNI.dll', 'AppxManifest.xml', 'resources.pri', 'Assets/StoreLogo.scale-100.png',
    'Assets/Square44x44Logo.scale-100.png', 'Assets/Square44x44Logo.targetsize-256.png', 'Assets/Square150x150Logo.scale-100.png', 'Assets/Wide310x150Logo.scale-100.png'],
};
const FORBIDDEN = /\.(pdb|ps1|psm1|bat|cmd|vbs|wsf|cs|csproj|snk)$|EndToEnd|appsettings\.Development/i;

/** The ZIP written by the SDK (the ZipDirectory task of MSBuild), then read back by it: it must give the staged files, byte for byte. */
function zip(stage, product) {
  const file = `${stage}.zip`;
  rmSync(file, { force: true });
  run('The ZIP', 'dotnet', ['msbuild', archiveProject, '-t:Zip', '-nologo', '-v:minimal', `-p:Folder=${stage}`, `-p:Archive=${file}`]);
  const back = `${stage}-read`;
  unzip(file, back);
  const names = namesOf(back);
  if (JSON.stringify(names) !== JSON.stringify(namesOf(stage))) fail('The ZIP does not give back the files of its folder');
  const hashes = names.map((name) => [name, digest(join(back, name))]);
  const changed = hashes.filter(([name, hash]) => hash !== digest(join(stage, name))).map(([name]) => name);
  if (changed.length > 0) fail(`The ZIP gives back other bytes for ${changed.join(', ')}`);
  rmSync(back, { recursive: true, force: true });
  const missing = [...REQUIRED, ...REQUIRED_BY_MODE[mode](product)].filter((name) => !names.includes(name));
  if (missing.length > 0) fail(`The ZIP lacks ${missing.join(', ')}`);
  const forbidden = names.filter((name) => FORBIDDEN.test(name));
  if (forbidden.length > 0) fail(`The ZIP carries ${forbidden.join(', ')}`);
  // ADR 076: the SHA-256 of each file of the ZIP beside it, for the allow-list of an antivirus or an EDR (the executable first of all).
  writeFileSync(`${stage}.files.sha256`, hashes.map(([name, hash]) => `${hash}  ${name}\n`).join(''));
  return file;
}

/** The files makeappx adds to a package of its own: its map of blocks, the types of its content; and signtool, when signed: the signature and its catalogue. */
const PACKAGE_FILES = ['AppxBlockMap.xml', '[Content_Types].xml', 'AppxSignature.p7x', 'AppxMetadata/CodeIntegrity.cat'];

/**
 * ADR 088: the MSIX package written by makeappx, signed when a certificate is given (for a trial on a machine: the Store
 * signs the package it receives), then unpacked by makeappx: it must give the staged files, byte for byte, and its
 * manifest must name the executable it carries.
 */
function msix(stage, product, version) {
  const file = join(artifacts, `${product}-${version}.msix`);
  rmSync(file, { force: true });
  const makeappx = kitTool('makeappx.exe');
  run('makeappx pack', makeappx, ['pack', '/d', stage, '/p', file, '/o'], { capture: true });
  if (signing.certificate) sign(file);
  const back = `${stage}-read`;
  rmSync(back, { recursive: true, force: true });
  run('makeappx unpack', makeappx, ['unpack', '/p', file, '/d', back, '/o'], { capture: true });
  const names = namesOf(back).filter((name) => !PACKAGE_FILES.includes(name));
  if (JSON.stringify(names) !== JSON.stringify(namesOf(stage))) fail('The MSIX package does not give back the files of its folder');
  const hashes = names.map((name) => [name, digest(join(back, name))]);
  const changed = hashes.filter(([name, hash]) => hash !== digest(join(stage, name))).map(([name]) => name);
  if (changed.length > 0) fail(`The MSIX package gives back other bytes for ${changed.join(', ')}`);
  if (!readFileSync(join(back, 'AppxManifest.xml'), 'utf8').includes(`Executable="${product}.exe"`)) fail('The manifest of the MSIX package does not name its executable');
  rmSync(back, { recursive: true, force: true });
  const missing = [...REQUIRED, ...REQUIRED_BY_MODE[mode](product)].filter((name) => !names.includes(name));
  if (missing.length > 0) fail(`The MSIX package lacks ${missing.join(', ')}`);
  const forbidden = names.filter((name) => FORBIDDEN.test(name));
  if (forbidden.length > 0) fail(`The MSIX package carries ${forbidden.join(', ')}`);
  writeFileSync(`${stage}.files.sha256`, hashes.map(([name, hash]) => `${hash}  ${name}\n`).join(''));
  return file;
}

/** ADR 049: the executable (the MSIX package for the Store, ADR 088) signed when a certificate is given, by signtool of the Windows SDK. */
function sign(file) {
  const signtool = kitTool('signtool.exe');
  const args = ['sign', '/fd', 'SHA256', '/f', signing.certificate];
  if (signing['certificate-password']) args.push('/p', signing['certificate-password']);
  if (signing['timestamp-url']) args.push('/tr', signing['timestamp-url'], '/td', 'SHA256');
  run('signtool', signtool, [...args, file]);
}

function assemble(published, locks, product, version) {
  // What the publication carries: the deps.json beside the server, the one kept by the build of the single file of the desktop.
  const deps = mode === 'Server' ? join(published, `${product}.deps.json`)
    : join(root, 'src', 'Topogenia.Host', 'obj', 'Release', 'net10.0', 'win-x64', `${product}.deps.json`);
  const core = join(artifacts, 'core-package');
  unzip(corePackage, core);
  checkCore(published, deps, core, locks);
  checkPaths(published);
  const stage = stageOf(published, deps, core, product, version);
  const file = mode === 'Store' ? msix(stage, product, version) : zip(stage, product);
  const hash = digest(file);
  // Its SHA-256, to check a copy taken from the shared folder (ADR 049).
  writeFileSync(`${file}.sha256`, `${hash}  ${basename(file)}\n`);
  rmSync(stage, { recursive: true, force: true });
  rmSync(core, { recursive: true, force: true });
  console.log(file);
  console.log(`SHA-256 ${hash}`);
}

// The version and the name of the product, as MSBuild evaluates them (Directory.Build.props, ProductIdentity.cs).
const { Version: version, ProductName: product } = JSON.parse(run('The reading of the version and of the name of the product', 'dotnet',
  ['msbuild', host, '-getProperty:Version', '-getProperty:ProductName'], { capture: true })).Properties;
const solution = readdirSync(root).find((name) => name.endsWith('.sln'));
if (!solution) fail(`No solution in ${root}`);

// The interface built from its sources as `npm run build` does (web/package.json: tsc -b, then Vite, which writes
// web/served-packages.json), each tool started by node itself; then the notices of the components up to date with it,
// read in the restore graphs of the build: the whole solution restored first, since a publication leaves graphs of its
// own (the package of the core, and for the desktop the trimming tasks of the SDK, which never ship).
const web = join(root, 'web');
run('The check of the types of the interface', process.execPath, [join(web, 'node_modules', 'typescript', 'bin', 'tsc'), '-b'], { cwd: web });
run('The build of the interface', process.execPath, [join(web, 'node_modules', 'vite', 'bin', 'vite.js'), 'build'], { cwd: web });
run('dotnet restore', 'dotnet', ['restore', join(root, solution)]);
run('The check of THIRD-PARTY-NOTICES.md (to rewrite: node build/third-party-notices.mjs)', process.execPath, [join(root, 'build', 'third-party-notices.mjs'), '--check']);

// ADR 048, ADR 074: the publication of the profile Properties/PublishProfiles/<mode>.pubxml on the package of the core
// (signed by strong name, obfuscated), never on the sources of the domain; its lock files are written in obj, the ones
// of the repository stay as the build wrote them; no symbols, so that no path of the machine of the build ships.
const locks = prepare();
const published = join(artifacts, profile.toLowerCase());
rmSync(published, { recursive: true, force: true });
run('dotnet publish', 'dotnet', ['publish', host, `-p:PublishProfile=${profile}`, '-p:UseCorePackage=true', '-p:NuGetLockFilePath=obj/packages.publish.lock.json', '-p:DebugType=none']);
if (signing.certificate && mode !== 'Store') sign(join(published, `${product}.exe`));
assemble(published, locks, product, version);
