// package.test.tsx
// The package of exchange: export without the people by default (S-5.4-25), import always through its preview (S-7.3-03), new project or replacement (spec 7.3).

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { PACKAGE_EXTENSION, i18next } from '../../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../../shared/keyboard/KeyboardContext.tsx';
import { captureDownloads } from '../../../test/downloads.ts';
import { callsTo, fakeApi, type FakeRoutes } from '../../../test/fakeApi.ts';
import { ExportPackageDialog } from './ExportPackageDialog.tsx';
import { ImportPackageDialog, type ReplacedProject } from './ImportPackageDialog.tsx';

const preview = (overrides: Partial<Schema<'ImportPreview'>> = {}): Schema<'ImportPreview'> => ({
  package_code: 'VTE', project_code: 'VTE', code_state: 'free', with_people: true,
  counts: { maps: 2, lanes: 3, steps: 9, flows: 8, sheets: 4, documents: 5, pain_points: 1, translations: 6 },
  unknown_logins: [], members: 3, settlement: { importer_added: false, released_lanes: 0, released_steps: 0 }, trash_code: null, ...overrides,
});

const imported = { json: { id: 'p-new', code: 'VTE', root_map_id: 'm-new', trash_code: null, created_user_ids: [] }, status: 201 };
const packageFile = () => new File([new Uint8Array([80, 75, 3, 4])], `ventes${PACKAGE_EXTENSION}`);

function openImport(routes: FakeRoutes, replaced?: ReplacedProject) {
  const sent = fakeApi(routes);
  render(<KeyboardProvider><ImportPackageDialog replaced={replaced} onClose={vi.fn()} /></KeyboardProvider>);
  return sent;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the export (S-5.4-24, S-5.4-25)', () => {
  it('« Sans les membres ni les auteurs » is ticked; the package is downloaded under the name of the project', async () => {
    const sent = fakeApi({ 'POST /api/projects/p-vte/export': { blob: new Blob(['zip']) } });
    const capture = captureDownloads();
    try {
      render(<KeyboardProvider><ExportPackageDialog project={{ id: 'p-vte', code: 'VTE', name: 'Ventes' }} onClose={vi.fn()} /></KeyboardProvider>);
      expect(screen.getByRole('checkbox', { name: 'Sans les membres ni les auteurs' })).toBeChecked();
      await userEvent.click(screen.getByRole('button', { name: 'Exporter' }));
      await waitFor(() => {
        expect(capture.files).toHaveLength(1);
      });
      expect(capture.files[0]?.name).toMatch(new RegExp(`^VTE_Ventes_\\d{4}-\\d{2}-\\d{2}\\${PACKAGE_EXTENSION}$`));
      expect(callsTo(sent, 'POST', '/api/projects/p-vte/export')[0]?.body).toEqual({ without_people: true });
    } finally {
      capture.restore();
    }
  });

  it('the option can be unticked: the people go with the package', async () => {
    const sent = fakeApi({ 'POST /api/projects/p-vte/export': { blob: new Blob(['zip']) } });
    const capture = captureDownloads();
    try {
      render(<KeyboardProvider><ExportPackageDialog project={{ id: 'p-vte', code: 'VTE', name: 'Ventes' }} onClose={vi.fn()} /></KeyboardProvider>);
      await userEvent.click(screen.getByRole('checkbox', { name: 'Sans les membres ni les auteurs' }));
      await userEvent.click(screen.getByRole('button', { name: 'Exporter' }));
      await waitFor(() => {
        expect(callsTo(sent, 'POST', '/api/projects/p-vte/export')[0]?.body).toEqual({ without_people: false });
      });
    } finally {
      capture.restore();
    }
  });
});

describe('the import into a new project (S-7.3-03)', () => {
  it('goes through the preview: nothing to import before it, then what the package holds, then the import', async () => {
    const sent = openImport({ 'POST /api/projects/import/preview': { json: preview({ unknown_logins: ['FICTIF\\lea'], settlement: { importer_added: true, released_lanes: 1, released_steps: 2 } }) },
      'POST /api/projects/import': imported });
    const importButton = screen.getByRole('button', { name: 'Importer' });
    expect([screen.getByRole('button', { name: 'Vérifier le paquet' }), importButton].map((button) => button.hasAttribute('disabled'))).toEqual([true, true]);
    await userEvent.upload(screen.getByTestId('import-package-file'), packageFile());
    expect(importButton).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    const shown = await screen.findByTestId('package-preview');
    expect(shown).toHaveTextContent('Cartes : 2 · Couloirs : 3 · Étapes : 9 · Liens : 8 · Fiches : 4 · Documents : 5 · Irritants : 1 · Traductions : 6');
    expect(shown).toHaveTextContent('Comptes inconnus, créés à l’import : FICTIF\\lea');
    expect(shown).toHaveTextContent('La personne qui importe en devient l’animateur.');
    expect(shown).toHaveTextContent('Couloirs dont l’acteur n’est pas membre : 1 · étapes dont la personne assignée n’est pas membre : 2');
    expect(within(shown).getByTestId('package-preview-code')).toHaveTextContent('Le projet prendra le code VTE.');
    await userEvent.click(importButton);
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-new');
    });
    const [check, done] = [callsTo(sent, 'POST', '/api/projects/import/preview')[0], callsTo(sent, 'POST', '/api/projects/import')[0]];
    expect([check?.query.get('with_members'), done?.query.get('with_members'), done?.query.has('code'), done?.headers.get('Content-Type')]).toEqual(['false', 'false', false, 'application/octet-stream']);
  });

  it('a choice changed after the preview asks for a new one before the import', async () => {
    const sent = openImport({ 'POST /api/projects/import/preview': { json: preview() } });
    await userEvent.upload(screen.getByTestId('import-package-file'), packageFile());
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    await screen.findByTestId('package-preview');
    await userEvent.click(screen.getByRole('checkbox', { name: 'Reprendre les membres du paquet' }));
    expect(screen.queryByTestId('package-preview')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Importer' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    await screen.findByTestId('package-preview');
    expect(callsTo(sent, 'POST', '/api/projects/import/preview').map((call) => call.query.get('with_members'))).toEqual(['false', 'true']);
  });

  it('a code taken asks for another code, checked again before the import', async () => {
    const sent = openImport({
      'POST /api/projects/import/preview': [{ json: preview({ code_state: 'taken' }) }, { json: preview({ project_code: 'VTEB', code_state: 'free' }) }],
      'POST /api/projects/import': imported,
    });
    await userEvent.upload(screen.getByTestId('import-package-file'), packageFile());
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    expect(await screen.findByTestId('package-preview-code')).toHaveTextContent('Le code VTE est déjà celui d’un projet : en choisir un autre.');
    expect(screen.getByRole('button', { name: 'Importer' })).toBeDisabled();
    await userEvent.type(screen.getByRole('textbox', { name: 'Code du nouveau projet' }), 'vteb');
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    expect(await screen.findByTestId('package-preview-code')).toHaveTextContent('Le projet prendra le code VTEB.');
    await userEvent.click(screen.getByRole('button', { name: 'Importer' }));
    await waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/projects/import')[0]?.query.get('code')).toBe('VTEB');
    });
  });

  it('a refused package is said in the dialog', async () => {
    openImport({ 'POST /api/projects/import/preview': { refused: 400, code: 'PAQUET_INVALIDE', message: 'Le paquet est abîmé.' } });
    await userEvent.upload(screen.getByTestId('import-package-file'), packageFile());
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Le paquet est abîmé.');
  });
});

describe('the replacement of a project (spec 7.3)', () => {
  it('says the code the current project takes in the trash, and replaces with the version of the project', async () => {
    const sent = openImport({ 'POST /api/projects/p-vte/import/preview': { json: preview({ trash_code: 'VTEA' }) }, 'POST /api/projects/p-vte/import': imported },
      { id: 'p-vte', code: 'VTE', version: 9, retentionDays: 30 });
    expect(screen.getByRole('dialog', { name: 'Remplacer le projet VTE' })).toHaveTextContent('restaurable pendant 30 jours');
    await userEvent.upload(screen.getByTestId('import-package-file'), packageFile());
    await userEvent.click(screen.getByRole('button', { name: 'Vérifier le paquet' }));
    expect(await screen.findByTestId('package-preview-trash')).toHaveTextContent('Le projet actuel part à la corbeille sous le code VTEA.');
    await userEvent.click(screen.getByRole('button', { name: 'Remplacer le projet' }));
    await waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/projects/p-vte/import')[0]?.headers.get('If-Match')).toBe('"9"');
    });
    expect(window.location.pathname).toBe('/maps/m-new');
  });
});
