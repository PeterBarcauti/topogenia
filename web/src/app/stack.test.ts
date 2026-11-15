// stack.test.ts
// The libraries of the state and of the languages: zustand 5 stores made vanilla, i18next given to React by react-i18next (B-0-02, B-0-03).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { getI18n } from 'react-i18next';
import { createPreferencesStore } from '../features/map/state/preferences.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import { openProject } from '../shared/state/openProject.ts';
import { createToastStore } from '../shared/ui/toasts.ts';
import { createProfileStore } from './profile/profileStore.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';

const packageJson = JSON.parse(readFileSync(join(import.meta.dirname, '../../package.json'), 'utf8')) as { dependencies: Record<string, string> };

describe('the state (B-0-02)', () => {
  it('is held by zustand 5', () => {
    expect(packageJson.dependencies.zustand).toMatch(/^5\.\d+\.\d+$/);
  });

  it('in vanilla stores, read, changed and followed without React', async () => {
    const stores = [createToastStore(), openProject, createPreferencesStore(null), createProfileStore(fakeSource([profileOf()]).source, () => undefined, 'fr')];
    const heard = stores.map(() => vi.fn());
    stores.forEach((store, index) => store.subscribe(heard[index] ?? vi.fn()));
    const [toasts, project, preferences, profile] = stores as [ReturnType<typeof createToastStore>, typeof openProject,
      ReturnType<typeof createPreferencesStore>, ReturnType<typeof createProfileStore>];
    toasts.getState().notify('Carte alignée');
    project.setState({ project: null });
    preferences.getState().set('showRaci', true);
    await profile.getState().load();
    expect(heard.map((listener) => listener.mock.calls.length > 0)).toEqual([true, true, true, true]);
    expect(profile.getState().profile?.display_name).toBe('Camille Bernard');
  });
});

describe('the languages (B-0-03)', () => {
  it('go through i18next, given to the components by react-i18next', () => {
    expect(Object.keys(packageJson.dependencies)).toEqual(expect.arrayContaining(['i18next', 'react-i18next']));
    expect(getI18n()).toBe(i18next);
    expect(i18next.isInitialized).toBe(true);
  });
});
