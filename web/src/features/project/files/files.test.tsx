// files.test.tsx
// The .pmap files: open a file chosen, dropped or pasted, its faults and its points to check, a package chosen sent to its import; duplicate; download a copy (annex B 1.6, AR-154).

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next, PACKAGE_EXTENSION } from '../../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../../shared/keyboard/KeyboardContext.tsx';
import { captureDownloads } from '../../../test/downloads.ts';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { DuplicateDialog } from './DuplicateDialog.tsx';
import { OpenPmapDialog } from './OpenPmapDialog.tsx';
import { downloadPmapCopy } from './pmapCopy.ts';

const PMAP = JSON.stringify({ format: 'pmap', version: '1.1', project: { code: 'VTE', name: 'Ventes' }, maps: [] });

const opened = (uncertainties: string[]) => ({
  json: { id: 'p-new', code: 'VTE', root_map_id: 'm-new', maps: 2, lanes: 3, steps: 9, flows: 8, sheets: 0, uncertainties }, status: 201,
});

const openDialog = () => {
  const close = vi.fn();
  render(<KeyboardProvider><OpenPmapDialog onClose={close} /></KeyboardProvider>);
  return { dialog: screen.getByRole('dialog', { name: 'Ouvrir un fichier .pmap' }), close };
};

const field = (name: string) => screen.getByRole('textbox', { name });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('open a .pmap (B-1.6-01 to B-1.6-12)', () => {
  it('is a wide dialog; a pasted content proposes the code and name of its project, its Markdown block and mark dropped', () => {
    const { dialog } = openDialog();
    expect(dialog).toHaveClass('dialog--wide');
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: `${String.fromCharCode(0xfeff)}\`\`\`json\n${PMAP}\n\`\`\`` } });
    expect([field('Code court'), field('Nom du projet')].map((input) => (input as HTMLInputElement).value)).toEqual(['VTE', 'Ventes']);
  });

  it('a chosen file fills the content and shows its name', async () => {
    openDialog();
    const file = new File([PMAP], 'ventes.pmap', { type: 'application/json' });
    await userEvent.upload(screen.getByTestId('open-pmap-file'), file);
    await waitFor(() => {
      expect(field('Code court')).toHaveValue('VTE');
    });
    expect(screen.getByText('ventes.pmap')).toBeInTheDocument();
    expect(screen.getByTestId('open-pmap-file')).toHaveAttribute('accept', '.pmap,application/json');
  });

  it('a file dropped on the area fills it too', async () => {
    openDialog();
    const area = field('Ou coller son contenu ici, par exemple la réponse d’une IA :');
    fireEvent.drop(area, { dataTransfer: { files: [new File([PMAP], 'glisse.pmap')] } });
    await waitFor(() => {
      expect(area).toHaveValue(PMAP);
    });
    expect(screen.getByText('glisse.pmap')).toBeInTheDocument();
  });

  it('a package chosen or dropped instead of a .pmap is not shown as text: its import opens with it, said (AR-154)', async () => {
    const sent = fakeApi({ 'POST /api/projects/import/preview': { refused: 400, code: 'PAQUET_INVALIDE', message: 'Le paquet est abîmé.' } });
    const zip = new File([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0x14, 0x00, 0xff, 0xfe])], `ventes${PACKAGE_EXTENSION}`);
    const { close } = openDialog();
    // The file dialog filters on .pmap, but lets the person show « Tous les fichiers » and take the package all the same.
    await userEvent.setup({ applyAccept: false }).upload(screen.getByTestId('open-pmap-file'), zip);
    const dialog = await screen.findByRole('dialog', { name: `Importer un paquet ${PACKAGE_EXTENSION}` });
    expect(within(dialog).getByRole('status')).toHaveTextContent(`« ventes${PACKAGE_EXTENSION} » est un paquet ${PACKAGE_EXTENSION}, pas un fichier .pmap : il s’importe ici, après vérification.`);
    expect([within(dialog).getByText(`ventes${PACKAGE_EXTENSION}`, { selector: '.file-name' }), screen.queryByRole('dialog', { name: 'Ouvrir un fichier .pmap' })]).toEqual([expect.anything(), null]);
    await userEvent.click(within(dialog).getByTestId('import-package-check'));
    expect(await within(dialog).findByTestId('import-package-error')).toHaveTextContent('Le paquet est abîmé.');
    expect(callsTo(sent, 'POST', '/api/projects/import/preview').map((call) => call.body)).toEqual([zip]);
    await userEvent.click(within(dialog).getByTestId('import-package-cancel'));
    expect(close).toHaveBeenCalledTimes(1);
  });

  it('a package dropped on the area goes to its import too', async () => {
    openDialog();
    fireEvent.drop(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { dataTransfer: { files: [new File(['PK\u0003\u0004'], `glisse${PACKAGE_EXTENSION}`)] } });
    expect(await screen.findByRole('dialog', { name: `Importer un paquet ${PACKAGE_EXTENSION}` })).toHaveTextContent(`« glisse${PACKAGE_EXTENSION} » est un paquet`);
  });

  it('a code or a name typed is no longer replaced by the content (B-1.6-09)', async () => {
    openDialog();
    await userEvent.type(field('Code court'), 'ach');
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: PMAP } });
    expect([field('Code court'), field('Nom du projet')].map((input) => (input as HTMLInputElement).value)).toEqual(['ACH', 'Ventes']);
    await userEvent.clear(field('Nom du projet'));
    await userEvent.type(field('Nom du projet'), 'Achats');
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: PMAP.replace('Ventes', 'Autre') } });
    expect(field('Nom du projet')).toHaveValue('Achats');
  });

  it('Ouvrir waits for a content, a valid code and a name (B-1.6-10 to B-1.6-12)', async () => {
    openDialog();
    const open = screen.getByRole('button', { name: 'Ouvrir' });
    expect(open).toBeDisabled();
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: PMAP } });
    expect(open).toBeEnabled();
    await userEvent.clear(field('Code court'));
    await userEvent.type(field('Code court'), 'VT');
    expect(open).toBeDisabled();
    await userEvent.type(field('Code court'), 'E');
    await userEvent.clear(field('Nom du projet'));
    await userEvent.type(field('Nom du projet'), '   ');
    expect(open).toBeDisabled();
  });
});

describe('the answer of the host (B-1.6-13 to B-1.6-17)', () => {
  const fill = () => {
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: PMAP } });
  };

  it('a refused file: the message, then each fault with its place in the file', async () => {
    fakeApi({ 'POST /api/projects/import-pmap': { refused: 400, code: 'FICHIER_INVALIDE', message: 'Le fichier n’est pas valide.',
      details: { errors: [{ path: 'map.steps[2]', code: 'PMAP_OBLIGATOIRE', message: 'Le couloir manque.' }, { path: '', code: 'PMAP_VIDE', message: 'Fichier vide.' }] } } });
    openDialog();
    fill();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Le fichier n’est pas valide.');
    expect(within(alert).getAllByRole('listitem').map((line) => line.textContent)).toEqual(['map.steps[2] Le couloir manque.', ' Fichier vide.']);
    expect(within(alert).getByText('map.steps[2]').tagName).toBe('CODE');
  });

  it('after a refused file, the page stays where it was; a change of the content takes the faults away', async () => {
    fakeApi({ 'POST /api/projects/import-pmap': { refused: 400, code: 'FICHIER_INVALIDE', message: 'Le fichier n’est pas valide.',
      details: { errors: [{ path: 'map.steps[2]', code: 'PMAP_OBLIGATOIRE', message: 'Le couloir manque.' }] } } });
    const { dialog } = openDialog();
    fill();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    expect(await screen.findByTestId('open-pmap-error')).toHaveTextContent('Le couloir manque.');
    expect([window.location.pathname, dialog.isConnected]).toEqual(['/', true]);
    fireEvent.change(field('Ou coller son contenu ici, par exemple la réponse d’une IA :'), { target: { value: `${PMAP} ` } });
    expect([screen.queryByTestId('open-pmap-error'), screen.queryByRole('alert')]).toEqual([null, null]);
  });

  it('sends the content, the code and the name; without a point to check, the map opens at once', async () => {
    const sent = fakeApi({ 'POST /api/projects/import-pmap': opened([]) });
    openDialog();
    fill();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-new');
    });
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')[0]?.body).toEqual({ content: PMAP, code: 'VTE', name: 'Ventes' });
  });

  it('with a point to check: « Projet créé », what it holds, the point, and « Ouvrir la carte »', async () => {
    fakeApi({ 'POST /api/projects/import-pmap': opened(['Couloir « Achats » deviné.']) });
    openDialog();
    fill();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir' }));
    const created = await screen.findByRole('dialog', { name: 'Projet créé' });
    expect(within(created).getByRole('status')).toHaveTextContent('Étapes : 9 · Couloirs : 3 · Liens : 8 · Cartes : 2');
    expect(within(created).getAllByRole('listitem').map((point) => point.textContent)).toEqual(['Couloir « Achats » deviné.']);
    expect(window.location.pathname).toBe('/');
    await userEvent.click(within(created).getByRole('button', { name: 'Ouvrir la carte' }));
    expect(window.location.pathname).toBe('/maps/m-new');
  });
});

describe('duplicate a project (B-1.6-18 to B-1.6-22)', () => {
  it('proposes « {nom} (copie) », says what is copied and what is not, and waits for a code', () => {
    render(<KeyboardProvider><DuplicateDialog projectId="p-vte" sourceName="Ventes" onClose={vi.fn()} /></KeyboardProvider>);
    const dialog = screen.getByRole('dialog', { name: 'Dupliquer le projet' });
    expect(field('Nom du projet')).toHaveValue('Ventes (copie)');
    expect(dialog).toHaveTextContent('La copie reprend toutes les cartes : couloirs, étapes, flèches et RACI.');
    expect(dialog).toHaveTextContent('Les fiches, documents et irritants restent dans le projet d’origine.');
    expect(dialog).toHaveTextContent('les membres ne sont pas copiés');
    expect(within(dialog).getByRole('button', { name: 'Dupliquer' })).toBeDisabled();
  });

  it('sends the code and the name, then opens the root map of the copy', async () => {
    const sent = fakeApi({ 'POST /api/projects/p-vte/duplicate': opened([]) });
    render(<KeyboardProvider><DuplicateDialog projectId="p-vte" sourceName="Ventes" onClose={vi.fn()} /></KeyboardProvider>);
    await userEvent.type(field('Code court'), 'vtec');
    await userEvent.click(screen.getByRole('button', { name: 'Dupliquer' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-new');
    });
    expect(callsTo(sent, 'POST', '/api/projects/p-vte/duplicate')[0]?.body).toEqual({ code: 'VTEC', name: 'Ventes (copie)' });
  });
});

describe('download a copy (B-1.6-23, B-1.6-24)', () => {
  it('reads the copy as text, without the sheets by default, and saves it as CODE_Short-name_day.pmap', async () => {
    const sent = fakeApi({ 'GET /api/projects/p-vte/pmap': { text: PMAP } });
    const capture = captureDownloads();
    try {
      const name = await downloadPmapCopy({ id: 'p-vte', code: 'VTE', name: 'Ventes à l’export' });
      expect(name).toMatch(/^VTE_Ventes-a-lexport_\d{4}-\d{2}-\d{2}\.pmap$/);
      expect(capture.files.map((file) => file.name)).toEqual([name]);
      expect(await capture.files[0]?.content.text()).toBe(PMAP);
      expect(callsTo(sent, 'GET', '/api/projects/p-vte/pmap')[0]?.query.get('with_sheets')).toBe('false');
    } finally {
      capture.restore();
    }
  });
});
