// contrast.test.ts
// The texts of the box of a pain point in a sheet on its tinted ground: 4.5:1 at least, colours read from the tokens (NF08, WCAG 1.4.3, S-NF-10).

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { contrastRatio, MIN_TEXT_CONTRAST } from '../shared/theme/laneTints.ts';

const read = (name: string) => readFileSync(join(import.meta.dirname, name), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
const css = readdirSync(import.meta.dirname).filter((name) => name.endsWith('.css') && name !== 'tokens.css').map(read).join('\n');
const tokens = new Map([...read('tokens.css').matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map((match) => [match[1] ?? '', (match[2] ?? '').trim()]));

/** A property of the rules whose selector list holds the selector given, the last one winning. */
function rule(selector: string, property: string): string | undefined {
  let found: string | undefined;
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    if (!(match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' ')).includes(selector)) continue;
    for (const declaration of (match[2] ?? '').split(';')) {
      const colon = declaration.indexOf(':');
      if (colon > 0 && declaration.slice(0, colon).trim() === property) found = declaration.slice(colon + 1).trim();
    }
  }
  return found;
}

/** The colour of a value of the sheets: a token, followed through its aliases, down to its hexadecimal value. */
function colourOf(value: string | undefined): string {
  const name = /^var\((--[\w-]+)\)$/.exec(value ?? '')?.[1];
  const token = name === undefined ? undefined : tokens.get(name);
  if (token === undefined) throw new Error(`No colour token in ${String(value)}.`);
  return token.startsWith('#') ? token : colourOf(token);
}

describe('the box of a pain point in a sheet (S-NF-10, WCAG 1.4.3)', () => {
  it('its title, its help and its line of time lost reach 4.5:1 on its ground, the help in its own rule or else in that of every help', () => {
    const ground = colourOf(rule('.irritant', 'background'));
    const texts: Record<string, string | undefined> = {
      title: rule('.irritant .field-label', 'color'),
      help: rule('.irritant .field-hint', 'color') ?? rule('.field-hint', 'color'),
      lost: rule('.lost', 'color'),
    };
    const ratios = Object.entries(texts).map(([name, value]) => [name, Math.round(contrastRatio(colourOf(value), ground) * 100) / 100] as const);
    expect(ratios.filter(([, ratio]) => ratio < MIN_TEXT_CONTRAST)).toEqual([]);
  });

  it('its help keeps the muted look of a help, a shade darker than --text-muted', () => {
    expect(rule('.irritant .field-hint', 'color')).toBe('var(--text-muted-on-danger-soft)');
    expect(contrastRatio(colourOf('var(--text-muted-on-danger-soft)'), colourOf('var(--text-muted)'))).toBeLessThan(1.1);
  });
});
