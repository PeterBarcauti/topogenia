// generate-tokens.mjs
// Writes the lane tint block of src/styles/tokens.css from laneTints.ts, darkening any ink below 4.5:1 (spec 9.2).

import { readFileSync, writeFileSync } from 'node:fs';
import { laneTokensBlock, TOKENS_BEGIN, TOKENS_END } from '../src/shared/theme/laneTints.ts';

const file = new URL('../src/styles/tokens.css', import.meta.url);
const css = readFileSync(file, 'utf8');
const start = css.indexOf(TOKENS_BEGIN);
const end = css.indexOf(TOKENS_END);
if (start < 0 || end < start) throw new Error('Lane tint markers not found in tokens.css');
const updated = css.slice(0, start + TOKENS_BEGIN.length) + '\n' + laneTokensBlock() + '\n' + css.slice(end);
if (updated !== css) writeFileSync(file, updated);
console.log(updated === css ? 'tokens.css already up to date' : 'tokens.css updated');
