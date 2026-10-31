// preferences.test.ts
// The display preferences in the local storage: neutral keys, defaults, a storage that throws, nothing else stored (annex B 7, AR-28, AR-90).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { DEFAULT_PREFERENCES, PREFERENCE_KEYS, browserStorage, createPreferencesStore, readPreferences, type PreferenceStorage } from './preferences.ts';

/** A storage held in a map, which records what it is given. */
function memory(initial: Record<string, string> = {}): PreferenceStorage & { readonly values: Map<string, string> } {
  const values = new Map(Object.entries(initial));
  return {
    values,
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
      values.set(key, value);
    },
  };
}

const throwing: PreferenceStorage = {
  getItem: () => {
    throw new DOMException('Blocked', 'SecurityError');
  },
  setItem: () => {
    throw new DOMException('Full', 'QuotaExceededError');
  },
};

afterEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
});

describe('the keys and defaults (B-7-01 to B-7-03, AR-28, AR-90, AR-91)', () => {
  it('are neutral app.* keys', () => {
    expect(PREFERENCE_KEYS).toEqual({
      showCodes: 'app.showCodes', alwaysShowPictos: 'app.alwaysShowPictos', showRaci: 'app.showRaci', showValueStream: 'app.showValueStream',
      ribbonCollapsed: 'app.ribbonCollapsed', sheetInputsFolded: 'app.sheetInputsFolded', sheetProcedureFolded: 'app.sheetProcedureFolded',
      sheetOutputsFolded: 'app.sheetOutputsFolded',
    });
  });

  it('are codes shown, pictograms always, RACI and value stream hidden, ribbon unfolded, the sections of the sheet open, when nothing is stored', () => {
    expect(DEFAULT_PREFERENCES).toEqual({
      showCodes: true, alwaysShowPictos: true, showRaci: false, showValueStream: false, ribbonCollapsed: false, sheetInputsFolded: false,
      sheetProcedureFolded: false, sheetOutputsFolded: false,
    });
    expect(readPreferences(memory())).toEqual(DEFAULT_PREFERENCES);
    expect(readPreferences(null)).toEqual(DEFAULT_PREFERENCES);
  });

  it("read '1' and '0', any other value giving the default", () => {
    const stored = memory({ 'app.showCodes': '0', 'app.alwaysShowPictos': 'yes', 'app.showRaci': '1', 'app.ribbonCollapsed': '1', 'app.sheetProcedureFolded': '1', 'app.sheetOutputsFolded': 'x' });
    expect(readPreferences(stored)).toEqual({
      showCodes: false, alwaysShowPictos: true, showRaci: true, showValueStream: false, ribbonCollapsed: true, sheetInputsFolded: false,
      sheetProcedureFolded: true, sheetOutputsFolded: false,
    });
  });

  it('read one layer under the steps at most: both stored on keep the RACI (spec 14.5)', () => {
    expect(readPreferences(memory({ 'app.showValueStream': '1' }))).toMatchObject({ showRaci: false, showValueStream: true });
    expect(readPreferences(memory({ 'app.showRaci': '1', 'app.showValueStream': '1' }))).toMatchObject({ showRaci: true, showValueStream: false });
  });

  it('ignore the keys of the 1.x', () => {
    expect(readPreferences(memory({ 'carto.afficherCodes': '0', 'carto.afficherRaci': '1' }))).toEqual(DEFAULT_PREFERENCES);
  });
});

describe('the store of the preferences', () => {
  it("changes a preference and writes it as '1' or '0' under its key", () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    store.getState().set('showRaci', true);
    store.getState().set('showCodes', false);
    expect([store.getState().showRaci, store.getState().showCodes]).toEqual([true, false]);
    expect(Object.fromEntries(storage.values)).toEqual({ 'app.showRaci': '1', 'app.showCodes': '0' });
    expect(createPreferencesStore(storage).getState()).toMatchObject({ showRaci: true, showCodes: false, alwaysShowPictos: true });
  });

  it('turns a layer off when the other one is turned on, and writes both; turning one off leaves the other (spec 14.5)', () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    store.getState().set('showRaci', true);
    store.getState().set('showValueStream', true);
    expect([store.getState().showRaci, store.getState().showValueStream]).toEqual([false, true]);
    expect(Object.fromEntries(storage.values)).toEqual({ 'app.showRaci': '0', 'app.showValueStream': '1' });
    store.getState().set('showRaci', true);
    expect([store.getState().showRaci, store.getState().showValueStream]).toEqual([true, false]);
    store.getState().set('showRaci', false);
    expect([store.getState().showRaci, store.getState().showValueStream]).toEqual([false, false]);
    store.getState().set('showCodes', false);
    expect(store.getState().showRaci).toBe(false);
  });

  it('works on the local storage of the browser by default', () => {
    createPreferencesStore().getState().set('ribbonCollapsed', true);
    expect(window.localStorage.getItem('app.ribbonCollapsed')).toBe('1');
    expect(createPreferencesStore().getState().ribbonCollapsed).toBe(true);
  });
});

describe('a storage that throws (B-7-04)', () => {
  it('gives the defaults, and a change holds for the session', () => {
    const store = createPreferencesStore(throwing);
    expect(store.getState()).toMatchObject(DEFAULT_PREFERENCES);
    expect(() => {
      store.getState().set('showRaci', true);
    }).not.toThrow();
    expect(store.getState().showRaci).toBe(true);
  });

  it('is no storage at all when reaching it throws', () => {
    vi.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
      throw new DOMException('Blocked', 'SecurityError');
    });
    expect(browserStorage()).toBeNull();
    expect(createPreferencesStore().getState()).toMatchObject(DEFAULT_PREFERENCES);
  });
});

describe('nothing else is stored (B-7-05)', () => {
  it('the preferences write only their eight keys', () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    const names = ['showCodes', 'alwaysShowPictos', 'showRaci', 'showValueStream', 'ribbonCollapsed', 'sheetInputsFolded', 'sheetProcedureFolded', 'sheetOutputsFolded'] as const;
    for (const name of names) store.getState().set(name, !store.getState()[name]);
    expect([...storage.values.keys()].sort()).toEqual(Object.values(PREFERENCE_KEYS).sort());
  });

  it('no other module of the interface uses the storage of the browser; the last project opened lives in the profile (AR-49)', () => {
    const src = join(import.meta.dirname, '../../../');
    const files = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
      const path = join(dir, name);
      return statSync(path).isDirectory() ? files(path) : [path];
    });
    const users = files(src)
      .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file))
      .filter((file) => /\b(localStorage|sessionStorage|indexedDB)\b/.test(readFileSync(file, 'utf8')))
      .map((file) => relative(src, file).split(sep).join('/'));
    expect(users).toEqual(['features/map/state/preferences.ts']);
  });
});
