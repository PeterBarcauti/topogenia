// quit.test.tsx
// The command « Quit » of the desktop mode: the menu of the account in the header, the confirmation, the host asked to stop, the page left (spec 5.2, S-5.2-16).

import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../api/client.ts';
import { PRODUCT_NAME, i18next } from '../shared/i18n/i18n.ts';
import { trackWrites } from '../shared/state/pendingWrites.ts';
import { toasts } from '../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeReply } from '../test/fakeApi.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';
import { stopped } from './quit.ts';

function start(profile: Partial<Schema<'ProfileView'>>, quit: FakeReply = { json: null, status: 202 }) {
  const sent = fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, 'POST /api/desktop/quit': quit });
  render(<App source={fakeSource([profileOf(profile)]).source} />);
  return sent;
}

async function askToQuit() {
  await userEvent.click(await screen.findByRole('button', { name: 'Camille Bernard' }));
  await userEvent.click(within(screen.getByRole('menu', { name: 'Menu du compte' })).getByRole('menuitem', { name: `Quitter ${PRODUCT_NAME}` }));
  return screen.getByRole('dialog', { name: `Quitter ${PRODUCT_NAME} ?` });
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
    stopped.setState({ stopped: false });
  });
  vi.unstubAllGlobals();
});

describe('the command « Quit » (S-5.2-16)', () => {
  it('in server mode the menu of the account holds the language of the interface only: nothing to stop (ADR 083)', async () => {
    const sent = start({});
    const user = await screen.findByRole('button', { name: 'Camille Bernard' });
    await userEvent.click(user);
    expect(within(screen.getByTestId('account-menu')).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Langue de l’interface']);
    expect(callsTo(sent, 'POST', '/api/desktop/quit')).toEqual([]);
  });

  it('the menu of the account opens under its button, their right sides aligned', async () => {
    start({ desktop: true });
    const user = await screen.findByRole('button', { name: 'Camille Bernard' });
    vi.spyOn(user, 'getBoundingClientRect').mockReturnValue(DOMRect.fromRect({ x: 700, y: 8, width: 80, height: 32 }));
    await userEvent.click(user);
    const menu = screen.getByRole('menu', { name: 'Menu du compte' });
    expect([menu.style.left, menu.style.top]).toEqual([`${String(780 - 240)}px`, '44px']);
  });

  it('in desktop mode the account opens its menu; « Quitter » asks first, and Annuler sends nothing', async () => {
    const sent = start({ desktop: true });
    const user = await screen.findByTestId('header-user');
    expect([user.tagName, user.textContent, user.getAttribute('aria-haspopup'), user.getAttribute('title')])
      .toEqual(['BUTTON', 'CBCamille Bernard', 'menu', 'Commandes de l’application']);
    const confirm = await askToQuit();
    expect(confirm).toHaveTextContent(`${PRODUCT_NAME} s’arrête sur ce poste. Les modifications sont enregistrées au fil de la saisie.`);
    await userEvent.click(within(confirm).getByRole('button', { name: 'Annuler' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(callsTo(sent, 'POST', '/api/desktop/quit')).toEqual([]);
    expect(screen.getByTestId('app-header')).toBeInTheDocument();
  });

  it('confirmed, it asks the host to stop, then the page says that the application stopped and that the tab can be closed', async () => {
    const sent = start({ desktop: true });
    await userEvent.click(within(await askToQuit()).getByRole('button', { name: 'Quitter' }));
    const page = await screen.findByTestId('page-stopped');
    expect(page).toHaveTextContent(`${PRODUCT_NAME} est arrêté. Cet onglet peut être fermé.`);
    expect(page).toHaveAttribute('role', 'status');
    expect(screen.queryByTestId('app-header')).not.toBeInTheDocument();
    expect(screen.queryByTestId('screen-home')).not.toBeInTheDocument();
    const [quit] = callsTo(sent, 'POST', '/api/desktop/quit');
    expect(quit?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('a host that does not answer leaves the application running and says how to stop it otherwise', async () => {
    start({ desktop: true }, { refused: 500, code: 'ERREUR_INTERNE' });
    await userEvent.click(within(await askToQuit()).getByRole('button', { name: 'Quitter' }));
    expect(await screen.findByText(`${PRODUCT_NAME} ne s’est pas arrêté. Réessayer, ou fermer la fenêtre de la console.`)).toBeInTheDocument();
    expect(screen.queryByTestId('page-stopped')).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByTestId('app-header')).toBeInTheDocument();
  });

  it('sends first what is typed and waits, then stops the host (B-4-46)', async () => {
    const sent = start({ desktop: true });
    let waiting = true;
    const order: string[] = [];
    const untrack = trackWrites({ unsaved: () => waiting, flush: () => { order.push('flush'); waiting = false; return Promise.resolve(); } });
    await userEvent.click(within(await askToQuit()).getByRole('button', { name: 'Quitter' }));
    await screen.findByTestId('page-stopped');
    untrack();
    expect([order, callsTo(sent, 'POST', '/api/desktop/quit').length]).toEqual([['flush'], 1]);
  });

  it('a typing that cannot reach the host keeps the application open, the host not asked, and says it', async () => {
    const sent = start({ desktop: true });
    const untrack = trackWrites({ unsaved: () => true, flush: () => Promise.reject(new Error('offline')) });
    await userEvent.click(within(await askToQuit()).getByRole('button', { name: 'Quitter' }));
    expect(await screen.findByText(`Une saisie de la fiche n’est pas encore enregistrée : ${PRODUCT_NAME} reste ouvert. Réessayer une fois la fiche enregistrée.`)).toBeInTheDocument();
    untrack();
    expect(callsTo(sent, 'POST', '/api/desktop/quit')).toEqual([]);
    expect(screen.queryByTestId('page-stopped')).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
