// profiles.ts
// Fictitious profiles and a fake source of the profile, shared by the unit tests of the shell.

import type { Profile, ProfileFields, ProfileSource } from '../app/profile/profileStore.ts';

export const USER_ID = '6f9619ff-8b86-d011-b42d-00c04fc964ff';

export const profileOf = (overrides: Partial<Profile> = {}): Profile => ({
  id: USER_ID, login: 'FICTIF\\camille', display_name: 'Camille Bernard', email: null, language: null, effective_language: 'fr',
  is_admin: false, first_visit_done: true, last_project_id: null, version: 3, roles: [], languages: ['fr', 'en'], ...overrides,
});

/** A source that answers each reading and each change with the next reply given, and records the changes sent; the projects remembered are given back in the profile read last. */
export function fakeSource(reads: (Profile | Error)[], changes: (Profile | Error)[] = []) {
  const sent: [ProfileFields, number][] = [];
  const remembered: string[] = [];
  let last = profileOf();
  const next = (replies: (Profile | Error)[]) => {
    const reply = replies.shift();
    if (reply === undefined) return Promise.reject(new Error('No reply left.'));
    return reply instanceof Error ? Promise.reject(reply) : Promise.resolve(reply);
  };
  const source: ProfileSource = {
    read: vi.fn(() => next(reads).then((profile) => {
      last = profile;
      return profile;
    })),
    change: vi.fn((fields: ProfileFields, version: number) => {
      sent.push([fields, version]);
      return next(changes);
    }),
    remember: vi.fn((projectId: string) => {
      remembered.push(projectId);
      return Promise.resolve({ ...last, last_project_id: projectId, version: last.version + 1 });
    }),
  };
  return { source, sent, remembered };
}
