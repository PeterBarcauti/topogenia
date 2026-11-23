// startItems.test.ts
// The lines of the menu of the starts of the home page (ADR 111): which are offered to whom and in which order.

import { profileOf } from '../../test/profiles.ts';
import { startItems } from './startItems.ts';

const ids = (profile: Parameters<typeof startItems>[0], blankFirst = false) => startItems(profile, { blankFirst }).map((item) => item.id);

describe('the starts of the home page', () => {
  it('desktop: sample, gallery, blank map, describe, open a file, in this order', () => {
    expect(ids(profileOf({ is_admin: true, desktop: true }))).toEqual(['sample', 'gallery', 'blank', 'describe', 'open']);
    expect(startItems(profileOf({ is_admin: true, desktop: true }), { blankFirst: false }).map((item) => item.label))
      .toEqual(['home.start_sample', 'home.start_gallery', 'home.start_blank', 'assistant.panel.describe_button', 'files.open']);
  });

  it('server administrator: no sample', () => {
    expect(ids(profileOf({ is_admin: true, desktop: false }))).toEqual(['gallery', 'blank', 'describe', 'open']);
  });

  it('no gallery without links', () => {
    expect(ids(profileOf({ is_admin: true, desktop: true, links: null }))).toEqual(['sample', 'blank', 'describe', 'open']);
    expect(ids(profileOf({ is_admin: true, links: undefined }))).toEqual(['blank', 'describe', 'open']);
  });

  it('the gallery says it opens in the browser', () => {
    expect(startItems(profileOf({ is_admin: true }), { blankFirst: false }).find((item) => item.id === 'gallery')?.title).toBe('home.start_gallery_help');
  });

  it('the menu of « New project » puts the blank map first, the others in the same order', () => {
    expect(ids(profileOf({ is_admin: true, desktop: true }), true)).toEqual(['blank', 'sample', 'gallery', 'describe', 'open']);
  });

  it('nothing for a user who is not administrator, nor before the profile is read', () => {
    expect([ids(profileOf({ is_admin: false, desktop: true })), ids(null)]).toEqual([[], []]);
  });
});
