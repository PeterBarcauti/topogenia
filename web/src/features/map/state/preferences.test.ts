// preferences.test.ts
// The display preferences in the local storage: neutral keys, defaults, the name of the voice of a presentation (UX-MOY-01), a storage that throws, nothing else stored (annex B 7, AR-28, AR-90).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { DEFAULT_PREFERENCES, PREFERENCE_KEYS, UPDATE_REMINDER_CLOSED_KEY, VOICE_NAME_KEY, browserStorage, createPreferencesStore, readPreferences, readReminderClosed, writeReminderClosed, type PreferenceStorage } from './preferences.ts';

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
      showAnalysis: 'app.showAnalysis', ribbonCollapsed: 'app.ribbonCollapsed',
      showSummaryOnOpen: 'app.showSummaryOnOpen', presenterSubtitles: 'app.presenterSubtitles', presenterVoice: 'app.presenterVoice', presenterOnlineVoices: 'app.presenterOnlineVoices', presenterVoiceAsked: 'app.presenterVoiceAsked',
      presenterStepMessages: 'app.presenterStepMessages', presenterFlowMessages: 'app.presenterFlowMessages', showBpmnDetails: 'app.bpmnShowDetails',
    });
  });

  it('are codes shown, pictograms always, RACI, value stream and analysis hidden, ribbon unfolded, the summary shown at the opening, when nothing is stored', () => {
    expect(DEFAULT_PREFERENCES).toEqual({
      showCodes: true, alwaysShowPictos: true, showRaci: false, showValueStream: false, showAnalysis: false, ribbonCollapsed: false,
      showSummaryOnOpen: true, presenterSubtitles: true, presenterVoice: false, presenterOnlineVoices: false, presenterVoiceAsked: false,
      presenterStepMessages: true, presenterFlowMessages: true, showBpmnDetails: true,
    });
    expect(readPreferences(memory())).toEqual(DEFAULT_PREFERENCES);
    expect(readPreferences(null)).toEqual(DEFAULT_PREFERENCES);
  });

  it("read '1' and '0', any other value giving the default", () => {
    const stored = memory({ 'app.showCodes': '0', 'app.alwaysShowPictos': 'yes', 'app.showRaci': '1', 'app.ribbonCollapsed': '1', 'app.showAnalysis': 'x' });
    expect(readPreferences(stored)).toEqual({
      showCodes: false, alwaysShowPictos: true, showRaci: true, showValueStream: false, showAnalysis: false, ribbonCollapsed: true,
      showSummaryOnOpen: true, presenterSubtitles: true, presenterVoice: false, presenterOnlineVoices: false, presenterVoiceAsked: false,
      presenterStepMessages: true, presenterFlowMessages: true, showBpmnDetails: true,
    });
  });

  it('read one layer under the steps at most: several stored on keep the older, the RACI, then the value stream (spec 14.5)', () => {
    expect(readPreferences(memory({ 'app.showValueStream': '1' }))).toMatchObject({ showRaci: false, showValueStream: true, showAnalysis: false });
    expect(readPreferences(memory({ 'app.showAnalysis': '1' }))).toMatchObject({ showRaci: false, showValueStream: false, showAnalysis: true });
    expect(readPreferences(memory({ 'app.showRaci': '1', 'app.showValueStream': '1' }))).toMatchObject({ showRaci: true, showValueStream: false });
    expect(readPreferences(memory({ 'app.showValueStream': '1', 'app.showAnalysis': '1' }))).toMatchObject({ showValueStream: true, showAnalysis: false });
    expect(readPreferences(memory({ 'app.showRaci': '1', 'app.showValueStream': '1', 'app.showAnalysis': '1' })))
      .toMatchObject({ showRaci: true, showValueStream: false, showAnalysis: false });
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

  it('turns the analysis layer on alone, and the others turn it off (plan 3.1, Makigami)', () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    store.getState().set('showValueStream', true);
    store.getState().set('showAnalysis', true);
    expect([store.getState().showRaci, store.getState().showValueStream, store.getState().showAnalysis]).toEqual([false, false, true]);
    expect(Object.fromEntries(storage.values)).toEqual({ 'app.showValueStream': '0', 'app.showAnalysis': '1' });
    store.getState().set('showRaci', true);
    expect([store.getState().showRaci, store.getState().showValueStream, store.getState().showAnalysis]).toEqual([true, false, false]);
  });

  it('keeps the voice of a presentation by its name, read again; forgotten, written empty; empty or too long, none (audit 3.2, UX-MOY-01)', () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    expect([VOICE_NAME_KEY, store.getState().presenterVoiceName]).toEqual(['app.presenterVoiceName', null]);
    store.getState().setVoiceName('Denise Online');
    expect([store.getState().presenterVoiceName, storage.values.get(VOICE_NAME_KEY), createPreferencesStore(storage).getState().presenterVoiceName])
      .toEqual(['Denise Online', 'Denise Online', 'Denise Online']);
    store.getState().setVoiceName(null);
    expect([store.getState().presenterVoiceName, storage.values.get(VOICE_NAME_KEY), createPreferencesStore(storage).getState().presenterVoiceName]).toEqual([null, '', null]);
    expect(createPreferencesStore(memory({ [VOICE_NAME_KEY]: 'x'.repeat(201) })).getState().presenterVoiceName).toBeNull();
    expect(createPreferencesStore(memory({ [VOICE_NAME_KEY]: 'x'.repeat(200) })).getState().presenterVoiceName).toHaveLength(200);
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
    expect(store.getState()).toMatchObject({ ...DEFAULT_PREFERENCES, presenterVoiceName: null });
    expect(() => {
      store.getState().set('showRaci', true);
      store.getState().setVoiceName('Hortense');
    }).not.toThrow();
    expect([store.getState().showRaci, store.getState().presenterVoiceName]).toEqual([true, 'Hortense']);
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
  it('the preferences write only their switches, the name of the voice and the day a reminder was closed', () => {
    const storage = memory();
    const store = createPreferencesStore(storage);
    const names = ['showCodes', 'alwaysShowPictos', 'showRaci', 'showValueStream', 'showAnalysis', 'ribbonCollapsed',
      'showSummaryOnOpen', 'presenterSubtitles', 'presenterVoice', 'presenterOnlineVoices', 'presenterVoiceAsked', 'presenterStepMessages', 'presenterFlowMessages', 'showBpmnDetails'] as const;
    for (const name of names) store.getState().set(name, !store.getState()[name]);
    store.getState().setVoiceName('Hortense');
    writeReminderClosed('2027-02-20', storage);
    expect([...storage.values.keys()].sort()).toEqual([...Object.values(PREFERENCE_KEYS), VOICE_NAME_KEY, UPDATE_REMINDER_CLOSED_KEY].sort());
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

describe('the day a reminder was closed (ADR 111)', () => {
  it('is written under a neutral key and read back', () => {
    const storage = memory();
    expect(UPDATE_REMINDER_CLOSED_KEY).toBe('app.updateReminderClosed');
    expect(readReminderClosed(storage)).toBeNull();
    writeReminderClosed('2027-02-20', storage);
    expect(storage.values.get('app.updateReminderClosed')).toBe('2027-02-20');
    expect(readReminderClosed(storage)).toBe('2027-02-20');
  });

  it('is none when absent, too long, or when the storage throws; writing to a storage that throws does not throw', () => {
    expect(readReminderClosed(memory({ 'app.updateReminderClosed': 'x'.repeat(50) }))).toBeNull();
    expect(readReminderClosed(throwing)).toBeNull();
    expect(readReminderClosed(null)).toBeNull();
    expect(() => {
      writeReminderClosed('2027-02-20', throwing);
      writeReminderClosed('2027-02-20', null);
    }).not.toThrow();
  });
});
