// profileStore.test.ts
// The profile: read at the start, its effective language applied, the language changed and remembered, the last project opened remembered, a conflict with oneself settled (annex B 1.2, AR-49, AR-106).

import { ApiError } from '../../api/errors.ts';
import { callsTo, fakeApi } from '../../test/fakeApi.ts';
import { USER_ID as USER, fakeSource, profileOf } from '../../test/profiles.ts';
import { apiProfileSource, createProfileStore } from './profileStore.ts';

const conflict = (author: string) => new ApiError(409, 'CONFLIT_VERSION', 'Modifié entre-temps.', { object: 'user', updated_by: author, updated_at: '2026-10-27T21:00:00Z' });

describe('the reading of the profile (B-1.2-01 to B-1.2-04)', () => {
  it('starts in the language already shown, then applies the effective language of the profile', async () => {
    const applied: string[] = [];
    const { source: fake } = fakeSource([profileOf({ effective_language: 'en' })]);
    const store = createProfileStore(fake, (language) => { applied.push(language); }, 'fr');
    expect(store.getState()).toMatchObject({ profile: null, error: null, language: 'fr' });
    await store.getState().load();
    expect(store.getState()).toMatchObject({ profile: { display_name: 'Camille Bernard' }, error: null, language: 'en' });
    expect(applied).toEqual(['en']);
  });

  it('keeps the failure without a profile, and a new reading clears it', async () => {
    const failure = new ApiError(0, 'RESEAU', '');
    const { source: fake } = fakeSource([failure, profileOf()]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().load();
    expect(store.getState()).toMatchObject({ profile: null, error: failure });
    await store.getState().load();
    expect(store.getState()).toMatchObject({ profile: { id: USER }, error: null });
  });
});

describe('a change of language (B-1.2-05, B-1.2-06)', () => {
  it('is applied at once, then sent to the profile with the version read', async () => {
    const applied: string[] = [];
    const { source: fake, sent } = fakeSource([profileOf()], [profileOf({ language: 'en', version: 4 })]);
    const store = createProfileStore(fake, (language) => { applied.push(language); }, 'fr');
    await store.getState().load();
    const change = store.getState().changeLanguage('en');
    expect(store.getState().language).toBe('en');
    await change;
    expect(sent).toEqual([[{ language: 'en' }, 3]]);
    expect(applied).toEqual(['fr', 'en']);
    expect(store.getState().profile?.version).toBe(4);
  });

  it('stays applied for the session when the profile refuses it', async () => {
    const { source: fake } = fakeSource([profileOf()], [new ApiError(0, 'RESEAU', '')]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().load();
    await expect(store.getState().changeLanguage('en')).resolves.toBeUndefined();
    expect(store.getState()).toMatchObject({ language: 'en', profile: { version: 3, language: null } });
  });
});

describe('a conflict with oneself (AR-106)', () => {
  it('is settled without the user: the profile read again, the same fields sent with its version', async () => {
    const { source: fake, sent } = fakeSource([profileOf(), profileOf({ version: 5, last_project_id: 'aaaaaaaa-0000-0000-0000-000000000001' })],
      [conflict(USER.toUpperCase()), profileOf({ version: 6, language: 'en' })]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().load();
    await store.getState().change({ language: 'en' });
    expect(sent).toEqual([[{ language: 'en' }, 3], [{ language: 'en' }, 5]]);
    expect(store.getState().profile).toMatchObject({ version: 6, language: 'en' });
  });

  it('a conflict with another author is not settled: the change fails', async () => {
    const other = conflict('0d1f3a5c-0000-0000-0000-000000000002');
    const { source: fake, sent } = fakeSource([profileOf()], [other]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().load();
    await expect(store.getState().change({ display_name: 'Camille B.' })).rejects.toBe(other);
    expect(sent).toHaveLength(1);
    expect(fake.read).toHaveBeenCalledTimes(1);
  });

  it('another refusal is not settled either, and a change without a profile sends nothing', async () => {
    const refused = new ApiError(400, 'LANGUE_INVALIDE', 'Langue inconnue.');
    const { source: fake, sent } = fakeSource([profileOf()], [refused]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().change({ language: 'en' });
    expect(sent).toEqual([]);
    await store.getState().load();
    await expect(store.getState().change({ language: 'xx' })).rejects.toBe(refused);
  });
});

describe('the last project opened (AR-49, B-1.3-02, B-7-05)', () => {
  const VTE = '0190a1b2-0000-7000-8000-00000000b001';

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is sent to the profile, without a version, and the profile of the reply is kept', async () => {
    const sent = fakeApi({ 'PUT /api/me/last-project': { json: profileOf({ version: 4, last_project_id: VTE }) } });
    const store = createProfileStore({ ...fakeSource([profileOf()]).source, remember: apiProfileSource.remember }, () => undefined, 'fr');
    await store.getState().load();
    await store.getState().rememberProject(VTE);
    const call = callsTo(sent, 'PUT', '/api/me/last-project')[0];
    expect([call?.body, call?.headers.get('If-Match')]).toEqual([{ project_id: VTE }, null]);
    expect(store.getState().profile).toMatchObject({ version: 4, last_project_id: VTE });
  });

  it('is not sent again for the last one, whatever the case of its identifier, nor before the profile is read', async () => {
    const { source: fake, remembered } = fakeSource([profileOf({ last_project_id: VTE.toUpperCase() })]);
    const store = createProfileStore(fake, () => undefined, 'fr');
    await store.getState().rememberProject(VTE);
    await store.getState().load();
    await store.getState().rememberProject(VTE);
    expect(remembered).toEqual([]);
    await store.getState().rememberProject('0190a1b2-0000-7000-8000-00000000b002');
    expect(remembered).toEqual(['0190a1b2-0000-7000-8000-00000000b002']);
  });

  it('a failure is silent: the profile stays as it was read', async () => {
    const { source: fake } = fakeSource([profileOf()]);
    const store = createProfileStore({ ...fake, remember: () => Promise.reject(new ApiError(0, 'RESEAU', '')) }, () => undefined, 'fr');
    await store.getState().load();
    await expect(store.getState().rememberProject(VTE)).resolves.toBeUndefined();
    expect(store.getState().profile).toMatchObject({ version: 3, last_project_id: null });
  });
});
