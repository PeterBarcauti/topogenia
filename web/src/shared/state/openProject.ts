// openProject.ts
// The project of the map shown, declared by the editor of the map and read by the header: its code, the breadcrumb, the File menu, the Members and Translations buttons, its languages (B-7-13, B-1.2-12, annex B 2.1, ADR 083).

import { createStore } from 'zustand/vanilla';
import type { ProjectLanguages } from './processLanguage.ts';

/** A map of the breadcrumb: its name as entered, the language it was entered in, and its translations (1.x `ArianeVue`). */
export interface Crumb {
  readonly mapId: string;
  readonly name: string;
  readonly sourceLanguage: string;
  readonly translations: Readonly<Record<string, string>>;
}

export interface OpenProject {
  readonly id: string;
  readonly code: string;
  /** The maps from the root map, named as the project, down to the map shown (B-2.1-01). */
  readonly breadcrumb: readonly Crumb[];
  /** Facilitator of the project or administrator: the Members button (B-2.1-06) and the translations (B-1.2-12). */
  readonly canEdit: boolean;
  /** Opens the File menu of the editor at a point of the window, under its button (B-2.1-05, B-2.4-52). */
  readonly openFileMenu: (at: { readonly x: number; readonly y: number }) => void;
  /** Opens the translations of the project over the map shown; declared with their screen (P6-T08). */
  readonly openTranslations?: () => void;
  /** Its language of origin and how far each language is written: the list of the languages of the process (ADR 083). */
  readonly languages: ProjectLanguages;
}

/** The project open, null on the screens without a map; the editor sets it when a map opens and clears it when it leaves. */
export const openProject = createStore<{ project: OpenProject | null }>()(() => ({ project: null }));
