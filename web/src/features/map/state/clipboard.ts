// clipboard.ts
// The clipboard of the editor: a store of the module, not the one of the system, shared by the maps and lost at a reload (B-2.7-01 to B-2.7-03, B-7-12).

import { createStore } from 'zustand/vanilla';
import type { ClipboardContent } from '../model/clipboard.ts';

/** What is copied or cut: the block, its mode, its project and its map; null when empty. */
export const clipboard = createStore<{ content: ClipboardContent | null }>()(() => ({ content: null }));

export function setClipboard(content: ClipboardContent | null): void {
  clipboard.setState({ content });
}

/** The steps of a map shown cut, greyed until the paste (B-2.7-09). */
export function cutSteps(content: ClipboardContent | null, mapId: string | undefined): ReadonlySet<string> {
  return new Set(content?.mode === 'cut' && content.mapId === mapId ? content.steps.map((step) => step.key) : []);
}
