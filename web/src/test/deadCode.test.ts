// deadCode.test.ts
// Spec 11, B-8.1-41, B-8.1-37: no dead code in the interface; every export of a module serves the application, every class of a style sheet is named by a script.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import ts from 'typescript';

const src = join(import.meta.dirname, '..');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/') && !file.endsWith('.d.ts'));

type Sources = ReadonlyMap<string, string>;

const treeOf = (file: string, text: string) => ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
const exported = (node: ts.Node): boolean => ts.canHaveModifiers(node) && (ts.getModifiers(node) ?? []).some((keyword) => keyword.kind === ts.SyntaxKind.ExportKeyword);

/** The names a module exports by a declaration. */
function exportsOf(tree: ts.SourceFile): string[] {
  return tree.statements.filter(exported).flatMap((statement) => {
    if (ts.isVariableStatement(statement)) return statement.declarationList.declarations.map((declaration) => declaration.name.getText(tree));
    const name = (statement as ts.DeclarationStatement).name;
    return name === undefined ? [] : [name.getText(tree)];
  });
}

/** The names each module takes from another one: `module#name`, `module#*` for all of them. */
function importsOf(file: string, tree: ts.SourceFile): string[] {
  return tree.statements.flatMap((statement) => {
    const specifier = (ts.isImportDeclaration(statement) || ts.isExportDeclaration(statement)) ? statement.moduleSpecifier : undefined;
    if (specifier === undefined || !ts.isStringLiteral(specifier) || !specifier.text.startsWith('.')) return [];
    const target = relative(src, join(src, dirname(file), specifier.text)).split(sep).join('/');
    if (ts.isExportDeclaration(statement)) {
      const clause = statement.exportClause;
      return clause !== undefined && ts.isNamedExports(clause) ? clause.elements.map((element) => `${target}#${(element.propertyName ?? element.name).getText(tree)}`) : [`${target}#*`];
    }
    const clause = ts.isImportDeclaration(statement) ? statement.importClause : undefined;
    const bindings = clause?.namedBindings;
    if (bindings !== undefined && ts.isNamespaceImport(bindings)) return [`${target}#*`];
    const named = bindings !== undefined && ts.isNamedImports(bindings) ? bindings.elements : [];
    return [...named.map((element) => `${target}#${(element.propertyName ?? element.name).getText(tree)}`), ...(clause?.name === undefined ? [] : [`${target}#default`])];
  });
}

/**
 * The exports that no other module takes and that their own module does not use beyond their declaration; the tools of
 * the repository (`users`: the scripts, the journeys) count as takers, the unit tests do not.
 */
function deadExports(sources: Sources, users: Sources = new Map()): string[] {
  const trees = new Map([...sources].filter(([file]) => /\.tsx?$/.test(file)).map(([file, text]) => [file, treeOf(file, text)]));
  const readers = [...users].map(([file, text]) => [file, treeOf(file, text)] as const);
  const taken = new Set([...trees, ...readers].flatMap(([file, tree]) => importsOf(file, tree)));
  return [...trees].flatMap(([file, tree]) => exportsOf(tree).filter((name) => {
    if (taken.has(`${file}#${name}`) || taken.has(`${file}#*`)) return false;
    const code = (sources.get(file) ?? '').replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
    return (code.match(new RegExp(`(?<![\\w$])(?<![^.]\\.)${name.replace(/\$/g, '\\$')}(?![\\w$])`, 'g'))?.length ?? 0) <= 1;
  }).map((name) => `${file} ${name}`));
}

/** The classes of the style sheets that no script names, whole or by the start of a class built from a value (`step--${type}`). */
function deadClasses(sources: Sources): string[] {
  const scripts = [...sources].filter(([file]) => /\.tsx?$/.test(file)).map(([, text]) => text).join('\n');
  const words = new Set(scripts.match(/[A-Za-z_][\w-]*/g) ?? []);
  const starts = [...scripts.matchAll(/([A-Za-z_][\w-]*-)\$\{/g)].map((match) => match[1] ?? '');
  const classes = [...sources].filter(([file]) => file.endsWith('.css'))
    .flatMap(([file, css]) => [...new Set(css.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\{[^}]*\}/g, '{}').match(/\.[A-Za-z_][\w-]*/g) ?? [])].map((name) => [file, name.slice(1)] as const));
  return classes.filter(([, name]) => !name.startsWith('react-flow') && !words.has(name) && !starts.some((start) => name.startsWith(start))).map(([file, name]) => `${file} .${name}`);
}

const all: Sources = new Map(shipped.filter((file) => /\.(tsx?|css)$/.test(file)).map((file) => [file, readFileSync(join(src, file), 'utf8')]));

/** The scripts and the journeys of web/, by their path from web/src (`../scripts/...`), so that their imports resolve as the sources do. */
const tools: Sources = new Map(['scripts', 'e2e'].flatMap((folder) => filesUnder(join(src, '..', folder)))
  .filter((file) => /\.(m?js|ts)$/.test(file) && !/\.test\.ts$/.test(file) && !file.includes(`${sep}node_modules${sep}`))
  .map((file) => [relative(src, file).split(sep).join('/'), readFileSync(file, 'utf8')]));

describe('no dead code (spec 11, B-8.1-41)', () => {
  it('every export of a module of the interface is taken by another module, or used by its own one', () => {
    expect(all.size).toBeGreaterThan(150);
    expect(tools.size).toBeGreaterThan(20);
    expect(deadExports(all, tools)).toEqual([]);
  });

  it('every class of a style sheet is named by a script (B-8.1-37)', () => {
    expect(deadClasses(all)).toEqual([]);
  });

  it('detects an export nobody takes, one only the tests take, and a class nobody names', () => {
    const probe = new Map([
      ['a.ts', 'export const used = 1;\nexport const kept = [2];\nexport function lonely() { return [...kept]; }\nexport type Unused = string;'],
      ['b.ts', "import { used } from './a.ts';\nexport const shown = used;"],
      ['c.tsx', "import { shown } from './b.ts';\nexport const C = () => <p className={`step step--${String(shown)}`}>x</p>;"],
      ['c.css', '.step { color: var(--text); } .step--task { border: 0; } .mono { font: inherit; }'],
    ]);
    expect(deadExports(probe)).toEqual(['a.ts lonely', 'a.ts Unused', 'c.tsx C']);
    expect(deadClasses(probe)).toEqual(['c.css .mono']);
  });
});
