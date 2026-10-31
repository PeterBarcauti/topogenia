// hardcodedTexts.test.ts
// D12, S-D12-02: every text the interface shows comes from the files of the languages; no text written in the JSX of a component, nor in an attribute a person reads.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';

const src = join(import.meta.dirname, '..');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

/** The components of the interface, the tests and their helpers aside. */
const COMPONENTS = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => file.endsWith('.tsx') && !file.endsWith('.test.tsx') && !file.startsWith('test/'));

/** The attributes whose value a person reads or hears; `label` only on an element of the page (a component takes a key there). */
const READ_ATTRIBUTES = new Set(['aria-label', 'aria-description', 'aria-roledescription', 'aria-valuetext', 'aria-placeholder', 'title', 'placeholder', 'alt']);

const isRead = (attribute: ts.JsxAttribute, tree: ts.SourceFile): boolean => {
  const name = attribute.name.getText(tree);
  return READ_ATTRIBUTES.has(name) || (name === 'label' && /^[a-z]/.test(attribute.parent.parent.tagName.getText(tree)));
};

const hasLetter = (text: string): boolean => /\p{L}/u.test(text);

/** The literal texts an expression may give: a string, a template, either branch of a condition, either side of && || ??. */
function literalTexts(node: ts.Expression): string[] {
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) return [node.text];
  if (ts.isTemplateExpression(node)) return [node.head.text, ...node.templateSpans.map((span) => span.literal.text)];
  if (ts.isParenthesizedExpression(node)) return literalTexts(node.expression);
  if (ts.isConditionalExpression(node)) return [...literalTexts(node.whenTrue), ...literalTexts(node.whenFalse)];
  if (ts.isBinaryExpression(node) && [ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken].includes(node.operatorToken.kind)) {
    return [...literalTexts(node.left), ...literalTexts(node.right)];
  }
  return [];
}

/** The texts written in the JSX of a source, each with its line: text nodes, literal children, literal values of the attributes read. */
function hardcodedTexts(source: string, file = 'probe.tsx'): string[] {
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const found: string[] = [];
  const report = (node: ts.Node, text: string) => {
    if (hasLetter(text)) found.push(`${file}:${String(tree.getLineAndCharacterOfPosition(node.getStart(tree)).line + 1)} ${text.trim()}`);
  };
  const visit = (node: ts.Node): void => {
    if (ts.isJsxText(node)) report(node, node.text);
    else if (ts.isJsxExpression(node) && node.expression !== undefined && !ts.isJsxAttribute(node.parent)) for (const text of literalTexts(node.expression)) report(node, text);
    else if (ts.isJsxAttribute(node) && isRead(node, tree) && node.initializer !== undefined) {
      const value = node.initializer;
      if (ts.isStringLiteral(value)) report(node, value.text);
      else if (ts.isJsxExpression(value) && value.expression !== undefined) for (const text of literalTexts(value.expression)) report(node, text);
    }
    ts.forEachChild(node, visit);
  };
  visit(tree);
  return found;
}

describe('no text written in the components (D12, S-D12-02)', () => {
  it('reads every component of the interface', () => {
    expect(COMPONENTS.length).toBeGreaterThan(70);
    expect(COMPONENTS).toContain('features/map/canvas/Canvas.tsx');
  });

  it('no component writes a text in its JSX nor in an attribute read by a person: each one comes from the files of the languages', () => {
    expect(COMPONENTS.flatMap((file) => hardcodedTexts(readFileSync(join(src, file), 'utf8'), file))).toEqual([]);
  });

  it('detects the texts it must refuse, and lets the keys, the values and the signs without a letter pass', () => {
    const faulty = [
      '<p>Bonjour</p>', '<b>{\'Oui\'}</b>', '<b>{`Non`}</b>', '<i>{done ? \'Fait\' : t(\'k\')}</i>', '<i>{ok && \'Prêt\'}</i>',
      '<input placeholder="Nom" />', '<img alt={\'Logo\'} />', '<span title={`Aide ${n}`} />', '<button aria-label="Fermer" />', '<option label="Choix" />',
    ];
    for (const jsx of faulty) expect([jsx, hardcodedTexts(`const a = ${jsx};`).length]).toEqual([jsx, 1]);
    const fine = [
      '<p>{t(\'home.title\')}</p>', '<span>·</span>', '<span>{\' · \'}</span>', '<i aria-label={t(\'map.zoom_in\')} />', '<p>{count}</p>',
      '<div className="map-canvas" data-testid="map-canvas" role="region" />', '<p>{`${code} ${String(n)}`}</p>', '<span title={label} />',
      '<Toolbar label="ribbon.name" />',
    ];
    for (const jsx of fine) expect([jsx, hardcodedTexts(`const a = ${jsx};`)]).toEqual([jsx, []]);
  });
});
