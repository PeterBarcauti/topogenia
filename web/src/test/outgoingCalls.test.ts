// outgoingCalls.test.ts
// ADR 014, ADR 111: the interface reaches the network only by the client of the API and by the files of the application it reads itself (the fonts of the PDF and of the image of a map), at addresses of its own; a new tab is opened only by the help, the links to the site and the links of the documents a project names, which the browser opens on a click; the page asks the site nothing.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';
import { urlOf } from '../api/request.ts';

const src = join(import.meta.dirname, '..');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

/** The modules of the interface, the tests and their helpers aside. */
const SHIPPED = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.endsWith('.d.ts'))
  .map((file) => [file, readFileSync(join(src, file), 'utf8')] as const);

/** What the page reaches the network by: `fetch`, the requests, the sockets, the streams of events, the beacons. */
const NETWORK = new Set(['fetch', 'XMLHttpRequest', 'WebSocket', 'EventSource', 'sendBeacon']);

/** The name a declaration gives (the key of an object, a member), which calls nothing. */
const isDeclaredName = (node: ts.Identifier): boolean => {
  const { parent } = node;
  return (ts.isPropertyAssignment(parent) || ts.isPropertySignature(parent) || ts.isPropertyDeclaration(parent) || ts.isMethodDeclaration(parent)
    || ts.isMethodSignature(parent)) && parent.name === node;
};

/** A text written in the code, a template included, outside the comments. */
const literalOf = (node: ts.Node): string | null =>
  ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node) || ts.isTemplateHead(node) || ts.isTemplateMiddle(node) || ts.isTemplateTail(node) ? node.text : null;

interface Found {
  /** The page reaches the network: a name of {@link NETWORK} used, by itself, as a member or by its text between brackets. */
  readonly network: boolean;
  /** The page opens a new tab: `window.open`, or a target `_blank` (a link, an argument). */
  readonly tab: boolean;
  /** The addresses written whole (a scheme, or `//` and a host): none where the network is reached. */
  readonly absolute: readonly string[];
}

/** What a source reaches and opens, read from its tree: a word in a comment or in a text that names nothing is not taken. */
function scan(source: string, file = 'probe.tsx'): Found {
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  let network = false;
  let tab = false;
  const absolute: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isIdentifier(node) && NETWORK.has(node.text) && !isDeclaredName(node)) network = true;
    if (ts.isElementAccessExpression(node) && ts.isStringLiteralLike(node.argumentExpression) && NETWORK.has(node.argumentExpression.text)) network = true;
    if (ts.isPropertyAccessExpression(node) && node.name.text === 'open' && ['window', 'globalThis', 'self'].includes(node.expression.getText(tree))) tab = true;
    const text = literalOf(node);
    if (text === '_blank') tab = true;
    if (text !== null && /\b[a-z][\w+.-]*:\/\/|^\/\/[\w-]/i.test(text)) absolute.push(text);
    ts.forEachChild(node, visit);
  };
  visit(tree);
  return { network, tab, absolute };
}

const SCANNED = SHIPPED.map(([file, source]) => [file, scan(source, file)] as const);

describe('the page asks only its own host (ADR 014, ADR 111)', () => {
  it('the network is reached only by the client of the API and the fonts of the PDF, at a relative address ; a new tab only by the help and the links to the site', () => {
    const reaching = SCANNED.filter(([, found]) => found.network);
    expect(reaching.map(([file]) => file)).toEqual(['api/request.ts', 'features/map/export/capture.ts', 'features/map/export/pdfFonts.ts']);
    expect(reaching.flatMap(([, found]) => found.absolute)).toEqual([]);
    // The client of the API sends to the routes of the contract, each under /api/ of the host, written from the root of the page.
    const contract = JSON.parse(readFileSync(join(src, 'api', 'generated', 'openapi.json'), 'utf8')) as { paths: Record<string, unknown> };
    expect(Object.keys(contract.paths).filter((route) => !route.startsWith('/api/'))).toEqual([]);
    expect(urlOf('/api/projects/{id}', { path: { id: 'a/b' }, query: { q: 'x' } })).toBe('/api/projects/a%2Fb?q=x');
    // A tab is opened by a script for the help and the site; a link of a document opens in one on a click (an address its project writes).
    expect(SCANNED.filter(([, found]) => found.tab).map(([file]) => file)).toEqual(['app/help.ts', 'app/siteLinks.ts', 'features/sheet/documents/DocumentParts.tsx']);
  });

  it('detects each way to reach the network or open a tab, and takes no word of a comment nor a text that names nothing', () => {
    const probes = [
      'void fetch(address, { mode: \'no-cors\' });',
      'navigator.sendBeacon(\'/x\', body);',
      'const request = new XMLHttpRequest();',
      'globalThis[\'WebSocket\'];',
      'export async function read(read: typeof fetch = fetch) { return read(\'/a\'); }',
      'window.open(address, \'_blank\', \'noopener\');',
      'export const A = () => <a href={address} target="_blank" rel="noopener noreferrer">x</a>;',
      'const address = `http://localhost/${path}`;',
      '// fetch the file, never by an XMLHttpRequest, nor window.open\nexport const ANTI_CSRF_HEADER = [\'X-Requested-With\', \'XMLHttpRequest\'];\nconst api = { fetch: 1, open: 2 };\nwindow.addEventListener(\'x\', go);\nconst one = \'/api/projects\';',
    ];
    expect(probes.map((probe) => scan(probe))).toEqual([
      { network: true, tab: false, absolute: [] },
      { network: true, tab: false, absolute: [] },
      { network: true, tab: false, absolute: [] },
      { network: true, tab: false, absolute: [] },
      { network: true, tab: false, absolute: [] },
      { network: false, tab: true, absolute: [] },
      { network: false, tab: true, absolute: [] },
      { network: false, tab: false, absolute: ['http://localhost/'] },
      { network: false, tab: false, absolute: [] },
    ]);
  });
});
