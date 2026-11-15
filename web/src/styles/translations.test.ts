// translations.test.ts
// The dialogs of the translations as their style sheet draws them: one choice of scope a line, the steps apart, the glossary in the mono font, the report and the text entered on their backgrounds (annex B 5, spec 9.5).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const css = readFileSync(join(import.meta.dirname, 'pages.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

/** A property of the rule whose selector list holds the selector given, the last one winning. */
function rule(selector: string, property: string): string | undefined {
  let found: string | undefined;
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = (match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' '));
    if (!selectors.includes(selector)) continue;
    for (const declaration of (match[2] ?? '').split(';')) {
      const colon = declaration.indexOf(':');
      if (colon > 0 && declaration.slice(0, colon).trim() === property) found = declaration.slice(colon + 1).trim();
    }
  }
  return found;
}

describe('the dialog of the translations (B-5-02 to B-5-05)', () => {
  it('the three scopes one under the other, without the frame of a fieldset; the steps separated by a line', () => {
    expect([rule('.scope-choice', 'display'), rule('.scope-choice', 'flex-direction'), rule('.scope-choice', 'border')]).toEqual(['flex', 'column', '0']);
    expect(rule('.translation-step + .translation-step', 'border-top')).toBe('1px solid var(--lane-separator)');
    expect(rule('.translation-step', 'align-items')).toBe('flex-start');
  });

  it('the glossary on the whole width in the mono font; the report and the text entered on their backgrounds of the charter', () => {
    expect([rule('.glossary', 'align-self'), rule('.glossary-text', 'font-family'), rule('.glossary-text', 'resize')]).toEqual(['stretch', 'var(--font-mono)', 'vertical']);
    expect([rule('.translation-report', 'background'), rule('.manual-original', 'background')]).toEqual(['var(--lane-even)', 'var(--table-header-bg)']);
  });
});
