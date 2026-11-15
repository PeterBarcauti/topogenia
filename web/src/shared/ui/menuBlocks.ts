// menuBlocks.ts
// The blocks of a menu: its items alone, or gathered in the named groups of choices it declares, and the rows they take for its placement (spec 9.5, ARIA 1.2).

import type { CommandItem } from '../commands/commands.ts';

/**
 * A run of choices drawn as one group, one of them checked: `icon-radio` their icon then a radio on the right (the type of
 * a lane), `radio-icon` a radio then their icon on the right (the type of a step), `swatches` a grid of colours with the
 * name of the choice under it (the colour of a lane), as the validated mock-up draws them.
 */
export interface MenuGroup {
  /** The items of the group by their identifiers; the menu keeps its own order. */
  readonly ids: readonly string[];
  /** The i18n key of its name: shown as its title when `titled`, else read by assistive technologies only. */
  readonly name: string;
  readonly titled?: boolean;
  readonly look: 'icon-radio' | 'radio-icon' | 'swatches';
  /** The i18n keys of the shorter texts shown, by item, under the title; the whole label stays the name of the item. */
  readonly texts?: Readonly<Record<string, string>>;
  /** Swatches: the i18n key of the caption of the choice ({{index}}, {{count}}), and the ink of each swatch by item. */
  readonly caption?: string;
  readonly inks?: Readonly<Record<string, string>>;
}

/** A block of the menu: an item alone, or a group with the items of it that the menu shows. */
export type MenuBlock = { readonly item: CommandItem } | { readonly group: MenuGroup; readonly items: readonly CommandItem[] };

/** The items gathered in their groups, where the first of each group stands; a group none of whose items is shown is left out. */
export function blocksOf(items: readonly CommandItem[], groups: readonly MenuGroup[]): MenuBlock[] {
  const blocks: MenuBlock[] = [];
  const placed = new Set<MenuGroup>();
  for (const item of items) {
    const group = groups.find((candidate) => candidate.ids.includes(item.id));
    if (group === undefined) blocks.push({ item });
    else if (!placed.has(group)) {
      placed.add(group);
      blocks.push({ group, items: items.filter((other) => group.ids.includes(other.id)) });
    }
  }
  return blocks;
}

/** The swatches a row of their grid holds. */
export const SWATCHES_A_ROW = 4;

/** The rows of 34 px a menu takes, for its placement before it is drawn: a title, a row of swatches and their caption count. */
export function rowsOf(blocks: readonly MenuBlock[]): number {
  return blocks.reduce((rows, block) => {
    if (!('group' in block)) return rows + 1;
    const title = block.group.titled === true ? 1 : 0;
    return rows + title + (block.group.look === 'swatches' ? Math.ceil(block.items.length / SWATCHES_A_ROW) + 1 : block.items.length);
  }, 0);
}
