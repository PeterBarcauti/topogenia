// profileStore.ts
// The profile of the user: read at the start, its language applied and remembered, the last project opened remembered, a conflict with oneself settled (annex B 1.2, AR-49, AR-106).

import { createStore, type StoreApi } from 'zustand/vanilla';
import { api, type Schema } from '../../api/client.ts';
import { isApiError } from '../../api/errors.ts';

export type Profile = Schema<'ProfileView'>;

/** The fields of the profile a change sends (only those present change, A-1.2-03). */
export type ProfileFields = Schema<'ChangeProfileBody'>;

/** Where the profile is read and changed: the API, or a fake one in the tests. */
export interface ProfileSource {
  readonly read: () => Promise<Profile>;
  readonly change: (fields: ProfileFields, version: number) => Promise<Profile>;
  /** Remembers the project just opened (`PUT /api/me/last-project`, without a version: AR-49); gives the profile. */
  readonly remember: (projectId: string) => Promise<Profile>;
}

export const apiProfileSource: ProfileSource = {
  read: () => api.get('/api/me'),
  change: (fields, version) => api.patch('/api/me', { body: fields, version }),
  remember: (projectId) => api.put('/api/me/last-project', { body: { project_id: projectId } }),
};

export interface ProfileState {
  /** The profile read, null before the first answer. */
  readonly profile: Profile | null;
  /** The failure of the last reading, null after a success. */
  readonly error: unknown;
  /** The language of the interface. */
  readonly language: string;
  /** Reads the profile (at the start, and by « Try again »), then applies its effective language (B-1.2-01, 04). */
  readonly load: () => Promise<void>;
  /** Applies a language at once, then sends it to the profile; a failure leaves it applied for the session (B-1.2-05, 06). */
  readonly changeLanguage: (language: string) => Promise<void>;
  /** Sends fields of the profile with the version read; a conflict with oneself is settled without the user (AR-106). */
  readonly change: (fields: ProfileFields) => Promise<void>;
  /**
   * Remembers the project of the map opened, put forward by the home page (AR-49, B-1.3-02); nothing when it is the last
   * one already, and a failure is silent: the home page then puts forward the one it knew.
   */
  readonly rememberProject: (projectId: string) => Promise<void>;
}

/**
 * A version conflict whose author is the user himself (two tabs: the language changed in one while a project opened in
 * the other moved the version on, AR-106).
 */
function isOwnConflict(error: unknown, userId: string): boolean {
  if (!isApiError(error, 'CONFLIT_VERSION')) return false;
  const author = error.details.updated_by;
  return typeof author === 'string' && author.toLowerCase() === userId.toLowerCase();
}

/**
 * The store of the profile. `applyLanguage` shows a language in the page and the requests; the language before the
 * first reading is the one already shown (the browser's, B-1.2-04), and the first reading goes out with the language
 * of the browser, from which the API derives the effective language (AR-51).
 */
export function createProfileStore(source: ProfileSource, applyLanguage: (language: string) => Promise<void> | void, initialLanguage: string): StoreApi<ProfileState> {
  return createStore<ProfileState>()((set, get) => {
    const apply = (language: string) => {
      // The texts of a language are read when it is first shown (ADR 083): the page follows once they are.
      void applyLanguage(language);
      set({ language });
    };

    const send = async (fields: ProfileFields, profile: Profile): Promise<void> => {
      try {
        set({ profile: await source.change(fields, profile.version) });
      } catch (error) {
        if (!isOwnConflict(error, profile.id)) throw error;
        const fresh = await source.read();
        set({ profile: await source.change(fields, fresh.version) });
      }
    };

    return {
      profile: null,
      error: null,
      language: initialLanguage,
      async load() {
        try {
          const profile = await source.read();
          set({ profile, error: null });
          apply(profile.effective_language);
        } catch (error) {
          set({ error });
        }
      },
      async changeLanguage(language) {
        apply(language);
        await get().change({ language }).catch(() => undefined);
      },
      async change(fields) {
        const profile = get().profile;
        if (profile === null) return;
        await send(fields, profile);
      },
      async rememberProject(projectId) {
        const profile = get().profile;
        if (profile === null || profile.last_project_id?.toLowerCase() === projectId.toLowerCase()) return;
        try {
          set({ profile: await source.remember(projectId) });
        } catch {
          // Not remembered: the home page keeps the project it put forward.
        }
      },
    };
  });
}
