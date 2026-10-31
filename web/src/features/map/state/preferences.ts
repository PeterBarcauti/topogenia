// preferences.ts
// The display preferences of the map, kept in the local storage of the browser, and nothing else there (annex B 7, AR-28, AR-90).

import { createStore, type StoreApi } from 'zustand/vanilla';

export interface DisplayPreferences {
  /** The codes of the steps shown (B-7-01). */
  readonly showCodes: boolean;
  /** The pictograms of the steps always shown, not only on hover (B-7-02). */
  readonly alwaysShowPictos: boolean;
  /** The RACI layer shown (B-7-03). */
  readonly showRaci: boolean;
  /** The value stream layer shown (spec 14.5): one layer at a time under the steps, it or the RACI. */
  readonly showValueStream: boolean;
  /** The ribbon folded to a thin line (AR-90). */
  readonly ribbonCollapsed: boolean;
  /** The sections of the sheet folded: inputs, procedure, outputs (AR-91). */
  readonly sheetInputsFolded: boolean;
  readonly sheetProcedureFolded: boolean;
  readonly sheetOutputsFolded: boolean;
}

export type PreferenceName = keyof DisplayPreferences;

/** The keys of the local storage, neutral (AR-28: `app.*`, 1.x `carto.*`), and the default of each. */
export const PREFERENCE_KEYS: Readonly<Record<PreferenceName, string>> = {
  showCodes: 'app.showCodes',
  alwaysShowPictos: 'app.alwaysShowPictos',
  showRaci: 'app.showRaci',
  showValueStream: 'app.showValueStream',
  ribbonCollapsed: 'app.ribbonCollapsed',
  sheetInputsFolded: 'app.sheetInputsFolded',
  sheetProcedureFolded: 'app.sheetProcedureFolded',
  sheetOutputsFolded: 'app.sheetOutputsFolded',
};

export const DEFAULT_PREFERENCES: DisplayPreferences = {
  showCodes: true, alwaysShowPictos: true, showRaci: false, showValueStream: false, ribbonCollapsed: false, sheetInputsFolded: false, sheetProcedureFolded: false,
  sheetOutputsFolded: false,
};

/** The layers under the steps, one at a time (spec 14.5): turning one on turns the other off. */
const LAYERS: readonly PreferenceName[] = ['showRaci', 'showValueStream'];

/** The part of the local storage the preferences use. */
export type PreferenceStorage = Pick<Storage, 'getItem' | 'setItem'>;

/** The local storage of the browser, or null where reaching it throws (blocked storage, B-7-04). */
export function browserStorage(): PreferenceStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/** A preference read from the storage: `'1'` or `'0'`, else its default (absent, unreadable, or any other value). */
function read(storage: PreferenceStorage | null, name: PreferenceName): boolean {
  try {
    const value = storage?.getItem(PREFERENCE_KEYS[name]);
    return value === '1' ? true : value === '0' ? false : DEFAULT_PREFERENCES[name];
  } catch {
    return DEFAULT_PREFERENCES[name];
  }
}

/** The preferences stored; two layers stored on keep the RACI, the older one. */
export function readPreferences(storage: PreferenceStorage | null): DisplayPreferences {
  const names = Object.keys(PREFERENCE_KEYS) as PreferenceName[];
  const stored = Object.fromEntries(names.map((name) => [name, read(storage, name)])) as unknown as DisplayPreferences;
  return stored.showRaci && stored.showValueStream ? { ...stored, showValueStream: false } : stored;
}

export interface PreferencesState extends DisplayPreferences {
  /**
   * Changes a preference for the session and writes it; a storage that refuses leaves the change for the session
   * (B-7-04). A layer turned on turns the other one off (spec 14.5).
   */
  readonly set: (name: PreferenceName, value: boolean) => void;
}

/** The preferences of the browser, read once when the store is made (by the editor of a map, P6-T04). */
export function createPreferencesStore(storage: PreferenceStorage | null = browserStorage()): StoreApi<PreferencesState> {
  return createStore<PreferencesState>()((setState, getState) => ({
    ...readPreferences(storage),
    set(name, value) {
      const others = value && LAYERS.includes(name) ? LAYERS.filter((layer) => layer !== name && getState()[layer]) : [];
      const changes: [PreferenceName, boolean][] = [[name, value], ...others.map((layer): [PreferenceName, boolean] => [layer, false])];
      setState(Object.fromEntries(changes));
      try {
        for (const [changed, on] of changes) storage?.setItem(PREFERENCE_KEYS[changed], on ? '1' : '0');
      } catch {
        // The storage is full or blocked: the preference holds for the session only.
      }
    },
  }));
}
