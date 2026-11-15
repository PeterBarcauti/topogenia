// tools.ts
// The tools of a sheet as pills (essai J10, asked by the project owner): the field stays the text it was, a comma between two tools; what a typing ends and what it still writes, a tool never written twice, and the tint of a tool, the one of a known office software or one drawn from its name, always the same for the same tool.

import { citedTools } from './draft.ts';

/** The length of the field of the tools, as the host keeps it. */
export const TOOLS_MAX = 4000;

/** A tint of the charter, by the number of its lane colour: 1 pink, 2 violet, 3 blue, 4 green, 5 orange, 6 yellow, 7 mauve, 8 teal. */
export type ToolTint = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;

/** The office software a room knows by its colour, read as a word of the name of the tool. */
const KNOWN: readonly (readonly [RegExp, ToolTint])[] = [
  [/\bexcel\b/, 4], [/\bword\b/, 3], [/\bpower\s?point\b/, 5], [/\boutlook\b/, 8], [/\bteams\b/, 2], [/\b(pdf|acrobat)\b/, 1],
  [/\bshare\s?point\b/, 8], [/\bone\s?note\b/, 7], [/\bpower\s?bi\b/, 6], [/\baccess\b/, 1], [/\bvisio\b/, 7],
];

/** The tint of a tool: the one of the software its name cites, else one drawn from its name, whatever its case. */
export function toolTint(tool: string): ToolTint {
  const name = tool.trim().toLocaleLowerCase();
  const known = KNOWN.find(([pattern]) => pattern.test(name));
  if (known !== undefined) return known[1];
  let sum = 0;
  for (const character of name) sum = (sum * 31 + (character.codePointAt(0) ?? 0)) % 9973;
  return ((sum % 8) + 1) as ToolTint;
}

/** The tools as the field keeps them: one after the other, a comma between two. */
export const joinTools = (tools: readonly string[]): string => tools.join(', ');

/** The tools without one written twice, whatever its case: the first writing is kept. */
export function distinctTools(tools: readonly string[]): string[] {
  const seen = new Set<string>();
  return tools.filter((tool) => {
    const key = tool.toLocaleLowerCase();
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

/** What a typing gives: the tools it ends, before its last comma, semicolon or line break, and what is still being typed after it. */
export function typedTools(text: string): { readonly ended: readonly string[]; readonly typing: string } {
  const parts = text.split(/[,;\n]/);
  return { ended: parts.slice(0, -1).map((part) => part.trim()).filter((part) => part !== ''), typing: parts.at(-1) ?? '' };
}

/** The tools of the field but the one being typed, which the field already holds as its last one. */
export function settledTools(field: string, typing: string): string[] {
  const tools = citedTools(field);
  const tail = typing.trim();
  return tail !== '' && tools.at(-1) === tail ? tools.slice(0, -1) : tools;
}
