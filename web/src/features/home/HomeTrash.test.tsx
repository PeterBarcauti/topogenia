// HomeTrash.test.tsx
// The trash of the home page and the deletion of a project by typing its code (B-1.3-15 to B-1.3-23).

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { toasts } from '../../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeReply, type FakeRoutes } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';
import type { ProjectItem } from './focus.ts';

const VTE: ProjectItem = {
  id: 'p-vte', code: 'VTE', name: 'Ventes', source_language: 'fr', name_translations: {}, description: null, classification: 'internal', root_map_id: 'm-vte', role: 'facilitator',
  step_count: 2, map_count: 1,
};

const OLD: Schema<'TrashItem'> = {
  id: 't-old', code: 'OLDA', name: 'Ancien processus', deleted_at: '2026-10-20T10:00:00Z', deleted_by: 'Léa Petit', purge_at: '2026-11-19T10:00:00Z', step_count: 4,
};

const trash = (projects: Schema<'TrashItem'>[], days = 30): FakeReply => ({ json: { retention_days: days, projects } });

function start(routes: FakeRoutes, isAdmin = false) {
  const sent = fakeApi({ 'GET /api/projects': { json: [VTE] }, 'GET /api/projects/p-vte/participants': { json: [] }, ...routes });
  render(<App source={fakeSource([profileOf({ is_admin: isAdmin })]).source} />);
  return sent;
}

const shown = () => within(screen.getByRole('region', { name: 'Notifications' })).queryAllByRole('status').map((toast) => toast.textContent);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
  });
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the trash (B-1.3-15 to B-1.3-20)', () => {
  it('is shown under the projects only when it is not empty', async () => {
    start({ 'GET /api/trash': trash([]) });
    await screen.findByTestId('project-VTE');
    expect(screen.queryByTestId('trash')).not.toBeInTheDocument();
  });

  it('its failure is ignored: the projects stay', async () => {
    start({ 'GET /api/trash': { refused: 500, code: 'ERREUR_INTERNE', message: 'Erreur interne.' } });
    expect(await screen.findByTestId('project-VTE')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByTestId('trash')).not.toBeInTheDocument();
  });

  it('each line: name, code, steps, deleted on by, erased for good on', async () => {
    start({ 'GET /api/trash': trash([OLD, { ...OLD, id: 't-x', code: 'XYZA', deleted_by: null }]) });
    const line = await screen.findByTestId('trash-OLDA');
    expect(within(screen.getByTestId('trash')).getByRole('heading')).toHaveTextContent('Corbeille (2)');
    expect(line).toHaveTextContent('Ancien processus');
    expect(within(line).getByText('OLDA')).toHaveClass('chip-code');
    expect(line).toHaveTextContent('4 étapes · Supprimé le 20/10/2026 par Léa Petit · effacé définitivement le 19/11/2026');
    expect(screen.getByTestId('trash-XYZA')).toHaveTextContent('par quelqu’un');
    expect(within(line).getAllByRole('button').map((button) => button.textContent)).toEqual(['Restaurer']);
  });

  it('Restore for every one who sees the line, then the home page is read again', async () => {
    const sent = start({ 'GET /api/trash': [trash([OLD]), trash([])], 'POST /api/trash/t-old/restore': { json: null, status: 204 } });
    await userEvent.click(await screen.findByRole('button', { name: 'Restaurer' }));
    await waitFor(() => {
      expect(shown()).toEqual(['Projet OLDA restauré.']);
    });
    expect(callsTo(sent, 'POST', '/api/trash/t-old/restore')).toHaveLength(1);
    expect(screen.queryByTestId('erase-project')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Supprimer définitivement' })).not.toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByTestId('trash')).not.toBeInTheDocument();
    });
  });

  it('a failure of the trash read again after an action is ignored as well', async () => {
    start({
      'GET /api/projects': [{ json: [VTE] }, { pending: true }],
      'GET /api/trash': [trash([OLD]), { refused: 500, code: 'ERREUR_INTERNE', message: 'Erreur interne.' }],
      'POST /api/trash/t-old/restore': { json: null, status: 204 },
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Restaurer' }));
    await waitFor(() => {
      expect(shown()).toEqual(['Projet OLDA restauré.']);
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('a refused restoration is said as an error', async () => {
    start({ 'GET /api/trash': trash([OLD]), 'POST /api/trash/t-old/restore': { refused: 409, code: 'PROJET_CODE_EXISTANT', message: 'Le code OLDA est déjà pris.' } });
    await userEvent.click(await screen.findByRole('button', { name: 'Restaurer' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Le code OLDA est déjà pris.');
  });

  it('« Supprimer définitivement » for the administrator, after a confirmation', async () => {
    const sent = start({ 'GET /api/trash': trash([OLD]), 'DELETE /api/trash/t-old': { json: null, status: 204 } }, true);
    await userEvent.click(await screen.findByRole('button', { name: 'Supprimer définitivement' }));
    const confirm = screen.getByRole('dialog', { name: 'Supprimer définitivement OLDA' });
    expect(confirm).toHaveTextContent('« Ancien processus » et tout son contenu seront effacés de la base, sans retour possible. Le code OLDA redeviendra libre.');
    expect(callsTo(sent, 'DELETE', '/api/trash/t-old')).toEqual([]);
    await userEvent.click(within(confirm).getByRole('button', { name: 'Supprimer définitivement' }));
    await waitFor(() => {
      expect(shown()).toEqual(['Projet OLDA supprimé définitivement.']);
    });
    expect(callsTo(sent, 'DELETE', '/api/trash/t-old')).toHaveLength(1);
  });
});

describe('the deletion of a project (B-1.3-21 to B-1.3-23)', () => {
  it('reminds the retention, takes the code in capitals and six letters, and waits for the exact code', async () => {
    start({ 'GET /api/trash': trash([], 45) });
    await userEvent.click(await screen.findByRole('button', { name: 'Supprimer…' }));
    const dialog = screen.getByRole('dialog', { name: 'Supprimer le projet VTE' });
    expect(dialog).toHaveTextContent('« Ventes » part à la corbeille : plus personne ne le voit. Il reste restaurable pendant 45 jours');
    const field = within(dialog).getByRole('textbox', { name: 'Pour confirmer, saisir le code du projet : VTE' });
    const go = within(dialog).getByRole('button', { name: 'Mettre à la corbeille' });
    await userEvent.type(field, 'vt');
    expect([field, go.hasAttribute('disabled')]).toEqual([field, true]);
    expect(field).toHaveValue('VT');
    await userEvent.type(field, 'eabcdef');
    expect(field).toHaveValue('VTEABC');
    expect(go).toBeDisabled();
    await userEvent.clear(field);
    await userEvent.type(field, 'vte');
    expect(go).toBeEnabled();
  });

  it('sends the code typed with the version read, then says it and reads the home page again', async () => {
    const sent = start({
      'GET /api/trash': [trash([]), trash([{ ...OLD, code: 'VTEA', name: 'Ventes' }])],
      'GET /api/projects/p-vte': { json: { version: 7 } },
      'DELETE /api/projects/p-vte': { json: null, status: 204 },
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Supprimer…' }));
    await userEvent.type(screen.getByRole('textbox'), 'VTE');
    await userEvent.click(screen.getByRole('button', { name: 'Mettre à la corbeille' }));
    await waitFor(() => {
      expect(shown()).toEqual(['Projet VTE mis à la corbeille.']);
    });
    const [deletion] = callsTo(sent, 'DELETE', '/api/projects/p-vte');
    expect([deletion?.query.get('confirmation'), deletion?.headers.get('If-Match')]).toEqual(['VTE', '"7"']);
    expect(await screen.findByTestId('trash-VTEA')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('a refusal stays in the dialog', async () => {
    start({ 'GET /api/projects/p-vte': { json: { version: 7 } }, 'DELETE /api/projects/p-vte': { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifié par Léa Petit.' } });
    await userEvent.click(await screen.findByRole('button', { name: 'Supprimer…' }));
    await userEvent.type(screen.getByRole('textbox'), 'VTE');
    await userEvent.click(screen.getByRole('button', { name: 'Mettre à la corbeille' }));
    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent('Modifié par Léa Petit.');
    expect(screen.getByRole('button', { name: 'Mettre à la corbeille' })).toBeEnabled();
  });
});
