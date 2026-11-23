// HomeUpdate.test.tsx
// The reminder of the home page (ADR 111): a discreet line that says the day of the version and leads to the page of the versions, closed for 90 days, kept by the browser, for the administrator alone.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { formatDay } from '../../shared/i18n/formatDate.ts';
import { i18next } from '../../shared/i18n/i18n.ts';
import { fakeApi } from '../../test/fakeApi.ts';
import { TEST_LINKS, fakeSource, profileOf } from '../../test/profiles.ts';
import { UPDATE_REMINDER_CLOSED_KEY } from '../map/state/preferences.ts';

/** Ninety-one days after the release (2026-11-21), a morning, in the local calendar. */
const LATE = new Date(2026, 10, 21 + 91, 9, 30);

function start(profile: Partial<Schema<'ProfileView'>> = {}) {
  fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
  return render(<App source={fakeSource([profileOf({ is_admin: true, desktop: true, ...profile })]).source} />);
}

const line = () => screen.queryByTestId('update-reminder');

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.stubGlobal('__APP_RELEASE_DATE__', '2026-11-21');
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(LATE);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  window.localStorage.clear();
  window.history.replaceState(null, '', '/');
});

describe('the reminder of the home page (ADR 111)', () => {
  it('the line says the day of the version in the language of the interface and opens the page of the versions', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    start();
    const found = await screen.findByTestId('update-reminder');
    expect(found).toHaveTextContent(`Cette version date du ${formatDay(__APP_RELEASE_DATE__, 'fr')}.`);
    expect(found).not.toHaveAttribute('role', 'alert');
    await userEvent.click(screen.getByRole('button', { name: 'Voir s’il en existe une plus récente' }));
    expect(open.mock.calls).toEqual([[`${TEST_LINKS.site}/versions/#v=${__APP_VERSION__}&d=portable`, '_blank', 'noopener,noreferrer']]);
  });

  it('speaks English for an interface in English', async () => {
    await i18next.changeLanguage('en');
    start({ language: 'en', effective_language: 'en' });
    expect(await screen.findByTestId('update-reminder')).toHaveTextContent(`This version dates from ${formatDay(__APP_RELEASE_DATE__, 'en')}.`);
    expect(screen.getByRole('button', { name: 'See whether a newer one exists' })).toBeInTheDocument();
  });

  it('closed, it stays closed when the page is read again', async () => {
    const first = start();
    await userEvent.click(await screen.findByRole('button', { name: 'Fermer le rappel' }));
    expect(line()).toBeNull();
    expect(window.localStorage.getItem(UPDATE_REMINDER_CLOSED_KEY)).toBe('2027-02-20');
    first.unmount();
    start();
    expect(await screen.findByTestId('home-empty')).toBeInTheDocument();
    expect(line()).toBeNull();
  });

  it('comes back 90 days after it was closed', async () => {
    window.localStorage.setItem(UPDATE_REMINDER_CLOSED_KEY, '2026-11-22');
    start();
    expect(await screen.findByTestId('update-reminder')).toBeInTheDocument();
  });

  it('a storage refused closes it for the visit', async () => {
    vi.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
      throw new DOMException('Blocked', 'SecurityError');
    });
    start();
    await userEvent.click(await screen.findByRole('button', { name: 'Fermer le rappel' }));
    expect(line()).toBeNull();
  });

  it.each([
    ['before the 90th day', {}, new Date(2026, 10, 21 + 89, 9)],
    ['a user who is not administrator', { is_admin: false, desktop: false }, LATE],
    ['the Store version', { store: true }, LATE],
    ['no links given by the host', { links: null }, LATE],
  ] as [string, Partial<Schema<'ProfileView'>>, Date][])('is not there: %s', async (_name, profile, now) => {
    vi.setSystemTime(now);
    start(profile);
    await screen.findByTestId('home-empty');
    expect(line()).toBeNull();
  });
});
