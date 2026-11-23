// preferences.ts
// The display preferences of the map, kept in the local storage of the browser, and nothing else there (annex B 7, AR-28, AR-90): switches (the details of the view in BPMN 2.0 among them, ADR 106), the name of the voice of a presentation chosen (audit 3.2, UX-MOY-01), and the day the reminder of an old version was closed (ADR 111).

import { createStore, type StoreApi } from 'zustand/vanilla';

export interface DisplayPreferences {
  /** The codes of the steps shown (B-7-01). */
  readonly showCodes: boolean;
  /** The pictograms of the steps always shown, not only on hover (B-7-02). */
  readonly alwaysShowPictos: boolean;
  /** The RACI layer shown (B-7-03). */
  readonly showRaci: boolean;
  /** The value stream layer shown (spec 14.5): one layer at a time under the steps, it, the RACI or the analysis. */
  readonly showValueStream: boolean;
  /** The layer « Analysis » shown (Makigami, plan 3.1): the flows where the case changes hands, off by default. */
  readonly showAnalysis: boolean;
  /** The ribbon folded to a thin line (AR-90). */
  readonly ribbonCollapsed: boolean;
  /** The summary of a map shown in a bubble under its title when it opens (plan 3.1, lot R). */
  readonly showSummaryOnOpen: boolean;
  /** The subtitles of a presentation shown, on by default (ADR 097). */
  readonly presenterSubtitles: boolean;
  /** The subtitles read aloud, off by default (ADR 097, F-119). */
  readonly presenterVoice: boolean;
  /** The online voices allowed (the text leaves for the maker of the browser), off by default. */
  readonly presenterOnlineVoices: boolean;
  /** The question of the voice answered once: the voice of the computer or a natural online one (essai J2 of 3.2). */
  readonly presenterVoiceAsked: boolean;
  /** What a presentation says of its steps, shown and read, on by default; off, the presenter speaks in its place (lot J10). */
  readonly presenterStepMessages: boolean;
  /** What a presentation says on the way from a step to the next (the handoffs), on by default (lot J10). */
  readonly presenterFlowMessages: boolean;
  /** The documents and the notes of the pain points drawn under the steps of the view in BPMN 2.0, on by default (ADR 106). */
  readonly showBpmnDetails: boolean;
}

export type PreferenceName = keyof DisplayPreferences;

/** The keys of the local storage, neutral (AR-28: `app.*`, 1.x `carto.*`), and the default of each. */
export const PREFERENCE_KEYS: Readonly<Record<PreferenceName, string>> = {
  showCodes: 'app.showCodes',
  alwaysShowPictos: 'app.alwaysShowPictos',
  showRaci: 'app.showRaci',
  showValueStream: 'app.showValueStream',
  showAnalysis: 'app.showAnalysis',
  ribbonCollapsed: 'app.ribbonCollapsed',
  showSummaryOnOpen: 'app.showSummaryOnOpen',
  presenterSubtitles: 'app.presenterSubtitles',
  presenterVoice: 'app.presenterVoice',
  presenterOnlineVoices: 'app.presenterOnlineVoices',
  presenterVoiceAsked: 'app.presenterVoiceAsked',
  presenterStepMessages: 'app.presenterStepMessages',
  presenterFlowMessages: 'app.presenterFlowMessages',
  showBpmnDetails: 'app.bpmnShowDetails',
};

export const DEFAULT_PREFERENCES: DisplayPreferences = {
  showCodes: true, alwaysShowPictos: true, showRaci: false, showValueStream: false, showAnalysis: false, ribbonCollapsed: false,
  showSummaryOnOpen: true, presenterSubtitles: true, presenterVoice: false,
  presenterOnlineVoices: false, presenterVoiceAsked: false, presenterStepMessages: true, presenterFlowMessages: true, showBpmnDetails: true,
};

/** The layers under the steps, one at a time (spec 14.5), the older first: turning one on turns the others off. */
const LAYERS: readonly PreferenceName[] = ['showRaci', 'showValueStream', 'showAnalysis'];

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

/** The key of the voice of a presentation chosen, by its name (audit 3.2, UX-MOY-01): the one preference that is a text, not a switch. */
export const VOICE_NAME_KEY = 'app.presenterVoiceName';

/** The longest name of a voice kept: the names of the browsers are short, a longer text stored is no name. */
const VOICE_NAME_MAX = 200;

/** The voice stored, null when none is (absent, empty, too long, or unreadable). */
function readVoiceName(storage: PreferenceStorage | null): string | null {
  try {
    const name = storage?.getItem(VOICE_NAME_KEY) ?? '';
    return name === '' || name.length > VOICE_NAME_MAX ? null : name;
  } catch {
    return null;
  }
}

/** The key of the day the reminder of an old version was closed (ADR 111): a day `yyyy-mm-dd`, kept by the browser, never by the database. */
export const UPDATE_REMINDER_CLOSED_KEY = 'app.updateReminderClosed';

/** The day stored, null when none is (absent, too long to be a day, or unreadable); whether it is a real day is the rule of the reminder. */
export function readReminderClosed(storage: PreferenceStorage | null = browserStorage()): string | null {
  try {
    const day = storage?.getItem(UPDATE_REMINDER_CLOSED_KEY) ?? '';
    return day === '' || day.length > 10 ? null : day;
  } catch {
    return null;
  }
}

/** Keeps the day the reminder was closed; a storage that refuses leaves it to the visit. */
export function writeReminderClosed(day: string, storage: PreferenceStorage | null = browserStorage()): void {
  try {
    storage?.setItem(UPDATE_REMINDER_CLOSED_KEY, day);
  } catch {
    // The storage is full or blocked: the reminder stays closed for the visit only.
  }
}

/** The preferences stored; several layers stored on keep the older one only (the RACI, then the value stream). */
export function readPreferences(storage: PreferenceStorage | null): DisplayPreferences {
  const names = Object.keys(PREFERENCE_KEYS) as PreferenceName[];
  const stored = Object.fromEntries(names.map((name) => [name, read(storage, name)])) as unknown as DisplayPreferences;
  const kept = LAYERS.find((layer) => stored[layer]);
  return { ...stored, ...Object.fromEntries(LAYERS.filter((layer) => layer !== kept).map((layer) => [layer, false])) };
}

export interface PreferencesState extends DisplayPreferences {
  /**
   * Changes a preference for the session and writes it; a storage that refuses leaves the change for the session
   * (B-7-04). A layer turned on turns the others off (spec 14.5).
   */
  readonly set: (name: PreferenceName, value: boolean) => void;
  /** The voice of a presentation chosen, by its name; null for the first one of the language. Read again after a reload, taken if it is still offered (UX-MOY-01). */
  readonly presenterVoiceName: string | null;
  /** Keeps the voice chosen, or forgets it (null, written empty); a storage that refuses leaves it for the session. */
  readonly setVoiceName: (name: string | null) => void;
}

/** The preferences of the browser, read once when the store is made (by the editor of a map, P6-T04). */
export function createPreferencesStore(storage: PreferenceStorage | null = browserStorage()): StoreApi<PreferencesState> {
  return createStore<PreferencesState>()((setState, getState) => ({
    ...readPreferences(storage),
    presenterVoiceName: readVoiceName(storage),
    setVoiceName(name) {
      setState({ presenterVoiceName: name });
      try {
        storage?.setItem(VOICE_NAME_KEY, name ?? '');
      } catch {
        // The storage is full or blocked: the voice holds for the session only.
      }
    },
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
