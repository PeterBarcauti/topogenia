// ProjectScreens.test.tsx
// The creation of a project and the members of a project: fields, names by language, the classification, the editor of members, what is sent (annex B 1.4, 1.5, spec 4.1).

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { callsTo, fakeApi, type FakeRoutes } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';

const member = (login: string, role: Schema<'MemberRole'>, sponsor = false, key = false): Schema<'MemberView'> =>
  ({ user_id: login, login, display_name: login, role, is_sponsor: sponsor, is_key_contributor: key, display_order: 0 });

const detail = (members: Schema<'MemberView'>[], version = 4) => ({
  id: '1b2c3d4e-0000-4000-8000-000000000001', code: 'VTE', name: 'Ventes', source_language: 'fr', name_translations: {}, description: null, shared_folder: null, classification: 'internal',
  sharing_group_label: null, root_map_id: 'm-vte', role: 'facilitator', members, version,
});

function start(path: string, routes: FakeRoutes, profile: Partial<Schema<'ProfileView'>> = {}) {
  window.history.replaceState(null, '', path);
  const sent = fakeApi(routes);
  render(<App source={fakeSource([profileOf(profile)]).source} />);
  return sent;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the creation of a project (B-1.4-01 to B-1.4-14)', () => {
  it('is for the administrator only, else « Droits insuffisants »', async () => {
    start('/projects/new', {});
    expect(await screen.findByTestId('new-project-refused')).toHaveTextContent('Droits insuffisants pour cette page.');
    expect(screen.queryByRole('button', { name: 'Créer le projet' })).not.toBeInTheDocument();
  });

  it('the language of the process, the one of the interface first, its name 200 at most, a translation of the name added; description 4000, folder 400; the code filtered', async () => {
    start('/projects/new', {}, { is_admin: true, languages: ['fr', 'en', 'es'] });
    const language = await screen.findByRole('combobox', { name: 'Langue du processus' });
    expect([(language as HTMLSelectElement).value, within(language).getAllByRole('option').map((option) => option.textContent)])
      .toEqual(['fr', ['Français · français', 'English · anglais', 'Español · espagnol']]);
    // One name, in the language of the process (ADR 083): the others come by « Traduire le nom en… ».
    expect(screen.getAllByRole('textbox', { name: /^Nom en / }).map((field) => field.getAttribute('maxlength'))).toEqual(['200']);
    expect(screen.getByRole('textbox', { name: 'Nom en français' })).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Traduire le nom en…' }), 'es');
    expect(screen.getByRole('textbox', { name: 'Nom en espagnol' })).toHaveAttribute('maxlength', '200');
    expect(within(screen.getByRole('combobox', { name: 'Traduire le nom en…' })).getAllByRole('option').map((option) => option.textContent))
      .toEqual(['Choisir une langue…', 'English · anglais']);
    expect(screen.getByRole('textbox', { name: 'Description' })).toHaveAttribute('maxlength', '4000');
    expect(screen.getByRole('textbox', { name: 'Dossier partagé (OneDrive)' })).toHaveAttribute('maxlength', '400');
    const code = screen.getByRole('textbox', { name: 'Code court' });
    await userEvent.type(code, 've-t2achat');
    expect(code).toHaveValue('VETACH');
  });

  it('« Créer le projet » waits for a valid code and the name in the language of the process', async () => {
    start('/projects/new', {}, { is_admin: true });
    const create = await screen.findByRole('button', { name: 'Créer le projet' });
    await userEvent.type(screen.getByRole('textbox', { name: 'Code court' }), 'vt');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en français' }), 'Ventes');
    expect(create).toBeDisabled();
    await userEvent.type(screen.getByRole('textbox', { name: 'Code court' }), 'e');
    expect(create).toBeEnabled();
    await userEvent.clear(screen.getByRole('textbox', { name: 'Nom en français' }));
    expect(create).toBeDisabled();
    // A translation of the name alone does not make a project: the language of the process wants its name.
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Traduire le nom en…' }), 'en');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en anglais' }), 'Sales');
    expect(create).toBeDisabled();
  });

  it('sends the name in the language of the process chosen and its translations, the members with a login, then opens the root map', async () => {
    const sent = start('/projects/new', { 'POST /api/projects': { json: { id: 'p-new', code: 'VTE', root_map_id: 'm-new', invited_user_ids: [] }, status: 201 } },
      { is_admin: true, languages: ['fr', 'en'] });
    await userEvent.type(await screen.findByRole('textbox', { name: 'Code court' }), 'VTE');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Langue du processus' }), 'en');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en anglais' }), 'Sales');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Traduire le nom en…' }), 'fr');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en français' }), 'Ventes');
    await userEvent.type(screen.getByRole('textbox', { name: 'Description' }), 'Du devis à la facture');
    await userEvent.click(screen.getByRole('button', { name: 'Ajouter une personne' }));
    await userEvent.click(screen.getByRole('button', { name: 'Ajouter une personne' }));
    expect(screen.getAllByRole('combobox', { name: 'Rôle' }).map((select) => (select as HTMLSelectElement).value)).toEqual(['contributor', 'contributor']);
    await userEvent.type(screen.getAllByRole('textbox', { name: 'Identifiant Windows (DOMAINE\\login)' })[1] ?? document.body, ' FICTIF\\lea ');
    await userEvent.selectOptions(screen.getAllByRole('combobox', { name: 'Rôle' })[1] ?? document.body, 'facilitator');
    await userEvent.click(screen.getAllByRole('checkbox', { name: 'Sponsor' })[1] ?? document.body);
    await userEvent.click(screen.getByRole('button', { name: 'Créer le projet' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-new');
    });
    expect(callsTo(sent, 'POST', '/api/projects')[0]?.body).toEqual({
      code: 'VTE', name: 'Sales', source_language: 'en', translations: { fr: 'Ventes' }, description: 'Du devis à la facture', shared_folder: null,
      classification: 'internal', sharing_group_label: null, members: [{ login: 'FICTIF\\lea', role: 'facilitator', is_sponsor: true, is_key_contributor: false }],
    });
  });

});

describe('the members and the refusals of a creation (B-1.4-01 to B-1.4-14)', () => {
  it('the editor lists the three roles and removes a line', async () => {
    start('/projects/new', {}, { is_admin: true });
    await userEvent.click(await screen.findByRole('button', { name: 'Ajouter une personne' }));
    const role = screen.getByRole('combobox', { name: 'Rôle' });
    expect(within(role).getAllByRole('option').map((option) => option.textContent)).toEqual(['Animateur', 'Contributeur', 'Lecteur']);
    await userEvent.click(screen.getByRole('button', { name: 'Retirer' }));
    expect(screen.queryByRole('combobox', { name: 'Rôle' })).not.toBeInTheDocument();
  });

  it('a refusal of the API is said in the form', async () => {
    start('/projects/new', { 'POST /api/projects': { refused: 409, code: 'PROJET_CODE_EXISTANT', message: 'Le code VTE est déjà pris.' } }, { is_admin: true });
    await userEvent.type(await screen.findByRole('textbox', { name: 'Code court' }), 'VTE');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en français' }), 'Ventes');
    await userEvent.click(screen.getByRole('button', { name: 'Créer le projet' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Le code VTE est déjà pris.');
    expect(window.location.pathname).toBe('/projects/new');
  });
});

describe('the classification chosen at the creation (S-4.1-07, Q5)', () => {
  it('Interne by default, the three levels of the closed list, the one chosen sent', async () => {
    const sent = start('/projects/new', { 'POST /api/projects': { json: { id: 'p-new', code: 'PAI', root_map_id: 'm-new', invited_user_ids: [] }, status: 201 } }, { is_admin: true });
    const level = await screen.findByRole('combobox', { name: 'Classification' });
    expect([(level as HTMLSelectElement).value, within(level).getAllByRole('option').map((option) => option.textContent)])
      .toEqual(['internal', ['Interne', 'Confidentiel', 'Strictement confidentiel']]);
    expect(level).toHaveAccessibleDescription('Niveau de confidentialité, affiché dans l’application et imprimé sur les exports.');
    await userEvent.type(screen.getByRole('textbox', { name: 'Code court' }), 'PAI');
    await userEvent.type(screen.getByRole('textbox', { name: 'Nom en français' }), 'Paie');
    await userEvent.selectOptions(level, 'Strictement confidentiel');
    await userEvent.click(screen.getByRole('button', { name: 'Créer le projet' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-new');
    });
    expect((callsTo(sent, 'POST', '/api/projects')[0]?.body as { classification: string }).classification).toBe('strictly_confidential');
  });
});

describe('the members of a project (B-1.5-01 to B-1.5-04)', () => {
  it('the editor is preloaded; Save sends the whole list with the version, keeps the key contribution, says it in line', async () => {
    const sent = start('/projects/1b2c3d4e-0000-4000-8000-000000000001/members', {
      'GET /api/projects/1b2c3d4e-0000-4000-8000-000000000001': [{ json: detail([member('FICTIF\\lea', 'facilitator', true, true), member('FICTIF\\paul', 'reader')]) },
        { json: detail([member('FICTIF\\lea', 'facilitator', true, true)], 5) }],
      'PUT /api/projects/1b2c3d4e-0000-4000-8000-000000000001/members': { json: { members: [], invited_user_ids: [], removed_user_ids: [] } },
    });
    expect(await screen.findByRole('heading', { name: 'Membres du projet VTE' })).toBeInTheDocument();
    expect(screen.getAllByRole('textbox').map((field) => (field as HTMLInputElement).value)).toEqual(['FICTIF\\lea', 'FICTIF\\paul']);
    await userEvent.click(screen.getAllByRole('button', { name: 'Retirer' })[1] ?? document.body);
    await userEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Membres enregistrés.');
    const [put] = callsTo(sent, 'PUT', '/api/projects/1b2c3d4e-0000-4000-8000-000000000001/members');
    expect([put?.body, put?.headers.get('If-Match')]).toEqual([[{ login: 'FICTIF\\lea', role: 'facilitator', is_sponsor: true, is_key_contributor: true }], '"4"']);
  });

  it('the next Save carries the version read after the previous one', async () => {
    const sent = start('/projects/1b2c3d4e-0000-4000-8000-000000000001/members', {
      'GET /api/projects/1b2c3d4e-0000-4000-8000-000000000001': [{ json: detail([], 4) }, { json: detail([], 5) }],
      'PUT /api/projects/1b2c3d4e-0000-4000-8000-000000000001/members': { json: { members: [], invited_user_ids: [], removed_user_ids: [] } },
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Enregistrer' }));
    await screen.findByRole('status');
    await userEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));
    await waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/projects/1b2c3d4e-0000-4000-8000-000000000001/members').map((call) => call.headers.get('If-Match'))).toEqual(['"4"', '"5"']);
    });
  });

  it('a refusal is said in line; « Ouvrir la carte » goes back to the root map', async () => {
    start('/projects/1b2c3d4e-0000-4000-8000-000000000001/members', {
      'GET /api/projects/1b2c3d4e-0000-4000-8000-000000000001': { json: detail([member('FICTIF\\lea', 'facilitator')]) },
      'PUT /api/projects/1b2c3d4e-0000-4000-8000-000000000001/members': { refused: 400, code: 'ANIMATEUR_REQUIS', message: 'Il faut au moins un animateur.' },
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Enregistrer' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Il faut au moins un animateur.');
    await act(async () => {
      await userEvent.click(screen.getByRole('button', { name: 'Ouvrir la carte' }));
    });
    expect(window.location.pathname).toBe('/maps/m-vte');
  });
});
