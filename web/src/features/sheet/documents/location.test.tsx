// location.test.tsx
// The two commands of the location of a document on its line: what the copy button copies and says, the folder the host shows on the desktop mode only, for every reader of the sheet (ADR 104).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo } from '../../../test/fakeApi.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { documentOf, inputOf, sheetViewOf } from '../../../test/sheets.ts';

const SHARE = '\\\\serveur\\partage\\visites\\programme.xlsx';
const outside = { producer: null, near: false, used: true };
const onShare = inputOf(documentOf('e1', { direction: 'input', step_id: 's2', name: 'Programme des visites', location: SHARE, location_type: 'path' }), outside);
const online = inputOf(documentOf('e2', { direction: 'input', step_id: 's2', name: 'Plan des salles', location: 'https://localhost/plan', location_type: 'url' }), outside);
const produced = inputOf(documentOf('d1', { step_id: 's1', name: 'Demande de visite', location: 'C:\\Dossiers\\demandes\\demande.docx', location_type: 'path' }));
const unplaced = { document: documentOf('o1', { step_id: 's2', name: 'Billet' }), used_by: [] };
const reading = { fill: false, validate: false, request_clarification: false, assign: false };

/** The clipboard of the browser replaced: what was written to it, or a browser that refuses. */
function clipboard(refuses = false): string[] {
  const written: string[] = [];
  const writeText = (text: string) => (refuses ? Promise.reject(new Error('refused')) : Promise.resolve(void written.push(text)));
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
  return written;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the copy button of a document (ADR 104)', () => {
  it('copies the path as it was entered, the link, or the name of a document without a location, and says which', async () => {
    const written = clipboard();
    const { record } = await renderSheet(sheetViewOf({ inputs: [onShare, online], outputs: [unplaced] }));
    fireEvent.click(screen.getByRole('button', { name: 'Copier le chemin de « Programme des visites »' }));
    fireEvent.click(screen.getByRole('button', { name: 'Copier le lien de « Plan des salles »' }));
    fireEvent.click(screen.getByRole('button', { name: 'Copier le nom « Billet »' }));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Chemin copié.', 'success'], ['Lien copié.', 'success'], ['Nom copié.', 'success']]);
    });
    expect(written).toEqual([SHARE, 'https://localhost/plan', 'Billet']);
    // Named on hover as it is named for a screen reader, and found by the document for the journeys.
    const copy = screen.getByTestId('document-copy-e1');
    expect([copy.getAttribute('title'), copy.getAttribute('type')]).toEqual(['Copier le chemin de « Programme des visites »', 'button']);
  });

  it('says that the browser refuses the clipboard, nothing copied', async () => {
    const written = clipboard(true);
    const { record } = await renderSheet(sheetViewOf({ inputs: [onShare] }));
    fireEvent.click(screen.getByTestId('document-copy-e1'));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Le navigateur refuse l’accès au presse-papiers.', 'error']]);
    });
    expect(written).toEqual([]);
  });

  it('is offered to a reader who may not fill the sheet, without the buttons that change the document', async () => {
    clipboard();
    await renderSheet(sheetViewOf({ inputs: [onShare], permissions: reading }));
    const line = within(screen.getByTestId('input-e1'));
    expect(line.getByRole('button', { name: 'Copier le chemin de « Programme des visites »' })).toBeEnabled();
    expect(line.queryByRole('button', { name: 'Modifier « Programme des visites »' })).toBeNull();
  });

  it('is offered on an input to tick, and a click on it ticks nothing', async () => {
    const written = clipboard();
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [produced] }));
    fireEvent.click(screen.getByRole('button', { name: 'Copier le chemin de « Demande de visite »' }));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Chemin copié.', 'success']]);
    });
    expect([written, callsTo(sent, 'PUT', '/api/steps/s2/uses/d1').length]).toEqual([['C:\\Dossiers\\demandes\\demande.docx'], 0]);
  });
});

describe('the folder of a document shown by the host (ADR 104)', () => {
  it('is asked on the desktop mode for a path, by the identifiers of the document alone, and said once shown', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [onShare] }), { 'POST /api/steps/s2/documents/e1/folder': { json: null, status: 204 } }, { desktop: true });
    fireEvent.click(screen.getByRole('button', { name: 'Ouvrir le dossier de « Programme des visites »' }));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Dossier ouvert dans l’Explorateur de fichiers.', 'success']]);
    });
    const calls = callsTo(sent, 'POST', '/api/steps/s2/documents/e1/folder');
    // Never a path in the request: the host reads the one it keeps.
    expect([calls.length, calls[0]?.body ?? null]).toEqual([1, null]);
  });

  it('names the step that declared the document, not the step of the sheet, for a document produced elsewhere', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [produced] }), { 'POST /api/steps/s1/documents/d1/folder': { json: null, status: 204 } }, { desktop: true });
    fireEvent.click(screen.getByTestId('document-folder-d1'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s1/documents/d1/folder')).toHaveLength(1);
    });
  });

  it('says the refusal of the host as the host wrote it, the button given back', async () => {
    const refusal = 'Le dossier est introuvable. Le chemin reste à copier.';
    const { record } = await renderSheet(sheetViewOf({ inputs: [onShare] }),
      { 'POST /api/steps/s2/documents/e1/folder': { refused: 404, code: 'DOSSIER_INTROUVABLE', message: refusal } }, { desktop: true });
    const button = screen.getByTestId('document-folder-e1');
    fireEvent.click(button);
    await vi.waitFor(() => {
      expect(record.notes).toEqual([[refusal, 'error']]);
    });
    expect(button).toBeEnabled();
  });

  it('waits for the answer of the host: a second click while the folder is looked for asks nothing more', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [onShare] }), { 'POST /api/steps/s2/documents/e1/folder': { pending: true } }, { desktop: true });
    const button = screen.getByTestId('document-folder-e1');
    fireEvent.click(button);
    await vi.waitFor(() => {
      expect(button).toBeDisabled();
    });
    fireEvent.click(button);
    expect(callsTo(sent, 'POST', '/api/steps/s2/documents/e1/folder')).toHaveLength(1);
  });

  it('is offered to a reader on the desktop mode, and only for a path: not for a link, not for a document without a location', async () => {
    await renderSheet(sheetViewOf({ inputs: [onShare, online], outputs: [unplaced], permissions: reading }), {}, { desktop: true });
    expect(screen.getByTestId('document-folder-e1')).toBeEnabled();
    expect([screen.queryByTestId('document-folder-e2'), screen.queryByTestId('document-folder-o1')]).toEqual([null, null]);
  });

  it('is not offered on the server mode, where the copy button stays', async () => {
    await renderSheet(sheetViewOf({ inputs: [onShare] }));
    expect(screen.queryByTestId('document-folder-e1')).toBeNull();
    expect(screen.getByTestId('document-copy-e1')).toBeEnabled();
  });
});

describe('the line of a document keeps its name readable (ADR 104, asked by the project owner)', () => {
  it('holds its commands in one group after the name: copy, folder, then edit and remove', async () => {
    await renderSheet(sheetViewOf({ inputs: [onShare] }), {}, { desktop: true });
    const row = screen.getByTestId('input-e1').querySelector('.entry-row');
    const group = row?.querySelector('.link-btns');
    expect([...(group?.querySelectorAll('button') ?? [])].map((button) => button.getAttribute('aria-label'))).toEqual([
      'Copier le chemin de « Programme des visites »', 'Ouvrir le dossier de « Programme des visites »', 'Modifier « Programme des visites »',
      'Retirer « Programme des visites »',
    ]);
    // The group comes after the name in the line, and the name is not cut: it wraps.
    expect([row?.lastElementChild === group, row?.querySelector('.document-name')?.textContent]).toEqual([true, 'Programme des visites']);
  });
});
