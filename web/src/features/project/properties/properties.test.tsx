// properties.test.tsx
// The properties of the open project: its classification next to its code in the header, the dialog that every member opens and only a facilitator changes, what a Save sends (spec 4.1, 10, Q5).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { App } from '../../../app/App.tsx';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { openProject } from '../../../shared/state/openProject.ts';
import { toasts } from '../../../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeReply, type FakeRoutes } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID } from '../../../test/maps.ts';
import { fakeSource, profileOf } from '../../../test/profiles.ts';
import { CLASSIFICATIONS } from '../classification.ts';
import { openProjectProperties, projectProperties } from './projectProperties.ts';

const DETAIL = `/api/projects/${PROJECT_ID}`;

const detail = (overrides: Partial<Schema<'ProjectDetail'>> = {}): FakeReply => ({
  json: {
    id: PROJECT_ID, code: 'VTE', name: 'Ventes', source_language: 'fr', name_translations: {}, description: null, shared_folder: null, classification: 'internal',
    sharing_group_label: null, translation_glossary: null, root_map_id: MAP_ID, role: 'facilitator', members: [], version: 4, workday_hours: 7,
    fte_hours_per_month: 151.67, ...overrides,
  },
});

/** The application on the home page, a map of the project VTE declared open to the header, the project read from the routes given. */
async function start(routes: FakeRoutes) {
  const sent = fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, ...routes });
  render(<App source={fakeSource([profileOf()]).source} />);
  await screen.findByTestId('app-header');
  act(() => {
    openProject.setState({ project: { id: PROJECT_ID, code: 'VTE', canEdit: true, openFileMenu: vi.fn(), languages: { origin: 'fr', total: 0, written: {} }, breadcrumb: [{ mapId: MAP_ID, name: 'Ventes', sourceLanguage: 'fr', translations: {} }] } });
  });
  return sent;
}

const dialog = () => screen.getByRole('dialog', { name: 'Propriétés du projet VTE' });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
    openProject.setState({ project: null });
  });
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the levels of the classification (Q5)', () => {
  it('are those of the contract, in its order: Interne, Confidentiel, Strictement confidentiel', () => {
    const contract = JSON.parse(readFileSync(join(import.meta.dirname, '../../../api/generated/openapi.json'), 'utf8')) as
      { components: { schemas: { Classification: { enum: string[] } } } };
    expect(CLASSIFICATIONS).toEqual(contract.components.schemas.Classification.enum);
    expect(CLASSIFICATIONS.map((level) => i18next.t(`classification.${level}`))).toEqual(['Interne', 'Confidentiel', 'Strictement confidentiel']);
  });
});

describe('the classification in the header (S-4.1-07)', () => {
  it('follows the code of the project as a button named by its level, right after the breadcrumb; in the language shown', async () => {
    await start({ [`GET ${DETAIL}`]: detail({ classification: 'confidential', role: 'reader' }) });
    const level = await screen.findByTestId('header-classification');
    expect([level.textContent, level.getAttribute('aria-label'), level.getAttribute('title')])
      .toEqual(['Confidentiel', 'Classification : Confidentiel. Propriétés du projet', 'Classification : Confidentiel. Propriétés du projet']);
    expect(screen.getByTestId('breadcrumb').nextElementSibling?.firstElementChild).toBe(level);
    expect(level.querySelector('svg.i')).not.toBeNull();
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(screen.getByTestId('header-classification')).toHaveTextContent('Confidential');
  });

  it('is not shown while the project is read, nor when it could not be; the header stays as it was', async () => {
    const sent = await start({ [`GET ${DETAIL}`]: { refused: 500, code: 'ERREUR_INTERNE' } });
    await waitFor(() => {
      expect(callsTo(sent, 'GET', DETAIL)).toHaveLength(1);
    });
    expect(screen.queryByTestId('header-classification')).not.toBeInTheDocument();
    expect(screen.getByTestId('header-file')).toBeInTheDocument();
  });
});

describe('the properties of the project for a member who is not a facilitator (B-4-69, B-8.1-44)', () => {
  it('shows the values read again at the opening, without the means to change them; Fermer closes', async () => {
    const sent = await start({ [`GET ${DETAIL}`]: [detail({ role: 'reader' }), detail({ role: 'contributor', classification: 'strictly_confidential', sharing_group_label: 'Équipe RH' })] });
    await userEvent.click(await screen.findByTestId('header-classification'));
    const level = await within(dialog()).findByRole('combobox', { name: 'Classification' });
    expect([(level as HTMLSelectElement).value, level.hasAttribute('disabled')]).toEqual(['strictly_confidential', true]);
    expect(document.activeElement).toBe(within(dialog()).getByTestId('project-properties-done'));
    const group = within(dialog()).getByRole('textbox', { name: 'Groupe de partage des documents' });
    expect([(group as HTMLInputElement).value, group.hasAttribute('readonly'), group.getAttribute('maxlength')]).toEqual(['Équipe RH', true, '100']);
    expect(within(dialog()).getByTestId('project-properties-read-only')).toHaveTextContent('Seul un animateur du projet modifie ces propriétés.');
    expect(within(dialog()).queryByRole('button', { name: 'Enregistrer' })).not.toBeInTheDocument();
    expect(callsTo(sent, 'GET', DETAIL)).toHaveLength(2);
    expect(screen.getByTestId('header-classification')).toHaveTextContent('Strictement confidentiel');
    await userEvent.click(within(dialog()).getByTestId('project-properties-done'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(sent.filter((call) => call.method !== 'GET')).toEqual([]);
  });
});

describe('the properties of the project for its facilitator (B-4-69, B-8.1-44, S-4.1-07)', () => {
  const routes = (gets: FakeReply[], classification: FakeReply, label: FakeReply): FakeRoutes => ({
    [`GET ${DETAIL}`]: gets, [`PUT ${DETAIL}/classification`]: classification, [`PUT ${DETAIL}/sharing-group-label`]: label,
  });

  it('Save waits for a change, then sends each setting by its route with the version read; the header shows the new level', async () => {
    const sent = await start(routes([detail(), detail()], detail({ classification: 'strictly_confidential', version: 5 }),
      detail({ classification: 'strictly_confidential', sharing_group_label: 'Équipe achats', version: 6 })));
    await userEvent.click(await screen.findByTestId('header-classification'));
    const save = await within(dialog()).findByRole('button', { name: 'Enregistrer' });
    expect(save).toBeDisabled();
    // The project read, its first field takes the focus, not the button of the footer drawn before the reading.
    expect(document.activeElement).toBe(within(dialog()).getByRole('combobox', { name: 'Classification' }));
    await userEvent.selectOptions(within(dialog()).getByRole('combobox', { name: 'Classification' }), 'Strictement confidentiel');
    await userEvent.type(within(dialog()).getByRole('textbox', { name: 'Groupe de partage des documents' }), '  Équipe achats ');
    await userEvent.click(save);
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(sent.filter((call) => call.method === 'PUT').map((call) => [call.path, call.body, call.headers.get('If-Match')])).toEqual([
      [`${DETAIL}/classification`, { classification: 'strictly_confidential' }, '"4"'],
      [`${DETAIL}/sharing-group-label`, { label: 'Équipe achats' }, '"5"'],
    ]);
    expect(screen.getByTestId('header-classification')).toHaveTextContent('Strictement confidentiel');
    expect(toasts.getState().toasts.map((toast) => [toast.kind, toast.text])).toEqual([['success', 'Propriétés du projet enregistrées.']]);
  });

  it('only what changed is sent: an emptied label removes it, and the same level goes nowhere', async () => {
    const sent = await start(routes([detail({ sharing_group_label: 'Équipe RH' })], detail(), detail({ version: 5 })));
    await userEvent.click(await screen.findByTestId('header-classification'));
    await userEvent.clear(await within(dialog()).findByRole('textbox', { name: 'Groupe de partage des documents' }));
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Enregistrer' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(sent.filter((call) => call.method === 'PUT').map((call) => [call.path, call.body])).toEqual([[`${DETAIL}/sharing-group-label`, { label: null }]]);
  });

  it('a refusal stays in the dialog with the version of the change already made; Annuler leaves without sending', async () => {
    const sent = await start(routes([detail()], detail({ classification: 'confidential', version: 5 }),
      { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifié entre-temps par lea.' }));
    await userEvent.click(await screen.findByTestId('header-classification'));
    await userEvent.selectOptions(await within(dialog()).findByRole('combobox', { name: 'Classification' }), 'Confidentiel');
    await userEvent.type(within(dialog()).getByRole('textbox', { name: 'Groupe de partage des documents' }), 'Achats');
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Enregistrer' }));
    expect(await within(dialog()).findByRole('alert')).toHaveTextContent('Modifié entre-temps par lea.');
    expect(screen.getByTestId('header-classification')).toHaveTextContent('Confidentiel');
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Enregistrer' }));
    await waitFor(() => {
      expect(callsTo(sent, 'PUT', `${DETAIL}/sharing-group-label`).map((call) => call.headers.get('If-Match'))).toEqual(['"5"', '"5"']);
    });
    expect(callsTo(sent, 'PUT', `${DETAIL}/classification`)).toHaveLength(1);
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Annuler' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('opens from elsewhere than the header (the File menu of the map), and closes when the header leaves the project', async () => {
    await start({ [`GET ${DETAIL}`]: detail() });
    await screen.findByTestId('header-classification');
    act(() => {
      openProjectProperties();
    });
    expect(await screen.findByRole('dialog', { name: 'Propriétés du projet VTE' })).toBeInTheDocument();
    act(() => {
      openProject.setState({ project: null });
    });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(projectProperties.getState()).toEqual({ detail: null, open: false });
  });
});

describe('the work time of the value stream in the properties (spec 14.2, S6)', () => {
  it('the work time of the value stream: the values read, only the one changed sent by its route with the version (spec 14.2, S6)', async () => {
    const sent = await start({ [`GET ${DETAIL}`]: [detail()], [`PUT ${DETAIL}/work-time`]: detail({ workday_hours: 7.5, version: 5 }) });
    await userEvent.click(await screen.findByTestId('header-classification'));
    const workday = await within(dialog()).findByRole('spinbutton', { name: 'Journée de travail (heures)' });
    expect([workday, within(dialog()).getByRole('spinbutton', { name: 'Heures par ETP et par mois' })].map((field) => (field as HTMLInputElement).value)).toEqual(['7', '151.67']);
    await userEvent.clear(workday);
    await userEvent.type(workday, '7.5');
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Enregistrer' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(sent.filter((call) => call.method === 'PUT').map((call) => [call.path, call.body, call.headers.get('If-Match')]))
      .toEqual([[`${DETAIL}/work-time`, { workday_hours: 7.5, fte_hours_per_month: null }, '"4"']]);
  });
});
