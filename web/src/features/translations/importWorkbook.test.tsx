// importWorkbook.test.tsx
// The workbook brought back through the dialog: read, sent in one pass with its scope, the report by reason, the map read again; the workbooks refused and the rows the host refuses (B-5-16 to B-5-24).

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type ExcelJS from 'exceljs';
import { i18next } from '../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../shared/keyboard/KeyboardContext.tsx';
import { callsTo, fakeApi, type FakeReply } from '../../test/fakeApi.ts';
import { bytesOf, detailOf, french, opened, PROJECT, STEP, textsOf } from '../../test/translations.ts';
import { buildWorkbook } from './model/workbook.ts';
import { TranslationsDialog } from './TranslationsDialog.tsx';

const LIST_PATH = `/api/projects/${PROJECT}/translations`;

// exceljs reads and writes a workbook slowly in jsdom, more under the whole suite: the waits for it are wider.
const SLOW = { timeout: 5_000 };

function openDialog(reply: FakeReply) {
  const onImported = vi.fn();
  const sent = fakeApi({ [`GET /api/projects/${PROJECT}`]: detailOf(null), [`PUT ${LIST_PATH}`]: reply });
  render(<KeyboardProvider><TranslationsDialog project={{ id: PROJECT, code: 'VTE' }} languages={['fr', 'en']} coverage={{ origin: 'fr', total: 3, written: { fr: 3, en: 1 } }} onImported={onImported} onClose={vi.fn()} /></KeyboardProvider>);
  return { sent, onImported };
}

/** The workbook of the three texts as downloaded, brought back as it is. */
async function downloaded(): Promise<ExcelJS.Workbook> {
  return opened(await buildWorkbook(textsOf(), null, french));
}

const bringBack = async (data: ArrayBuffer | string) =>
  userEvent.upload(screen.getByTestId('translations-file'), new File([data], 'VTE_traductions.xlsx'));

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the workbook brought back and its report (B-5-20, B-5-22 to B-5-24)', () => {
  it('sent in one pass with its scope and its rows; « N traductions enregistrées », the rows aside by reason with their lines; the map read again', { timeout: 15_000 }, async () => {
    const book = await opened(await buildWorkbook(textsOf(['fr', 'en'], 'auto'), null, french));
    const sheet = book.getWorksheet('Traductions') as ExcelJS.Worksheet;
    sheet.getRow(2).getCell(6).value = 'Sales';
    sheet.getRow(3).getCell(6).value = 'Enter the order';
    sheet.getRow(4).getCell(1).value = 'flow:12';
    const { sent, onImported } = openDialog({ json: { written: 1, ignored: [{ line: 3, reason: 'TEXTE_MODIFIE' }] } });
    await bringBack(await bytesOf(book));
    expect(await screen.findByTestId('translations-report', undefined, SLOW)).toHaveTextContent('1 traduction enregistrée.');
    expect(callsTo(sent, 'PUT', LIST_PATH)[0]?.body).toEqual({ scope: 'auto', rows: [
      { line: 2, type: 'project', id: PROJECT, original: null, translations: { fr: 'Ventes', en: 'Sales' } },
      { line: 3, type: 'step', id: STEP, original: null, translations: { fr: 'Saisir la commande', en: 'Enter the order' } },
    ] });
    expect(screen.getByTestId('translations-ignored-OBJET_INTROUVABLE')).toHaveTextContent('1 ligne ignorée, élément supprimé depuis ou clé modifiée : lignes 4');
    expect(screen.getByTestId('translations-ignored-TEXTE_MODIFIE')).toHaveTextContent('1 ligne ignorée, texte modifié depuis le téléchargement : lignes 3');
    expect(screen.queryByTestId('translations-ignored-SANS_TRADUCTION')).not.toBeInTheDocument();
    expect(onImported).toHaveBeenCalledTimes(1);
  });

  it('the plural of the report: several translations and several lines of one reason', { timeout: 15_000 }, async () => {
    openDialog({ json: { written: 12, ignored: [{ line: 7, reason: 'TRADUCTION_MANUELLE' }, { line: 5, reason: 'TRADUCTION_MANUELLE' }, { line: 9, reason: 'SANS_TRADUCTION' }] } });
    await bringBack(await bytesOf(await downloaded()));
    expect(await screen.findByTestId('translations-report', undefined, SLOW)).toHaveTextContent('12 traductions enregistrées.');
    expect(screen.getByTestId('translations-ignored-TRADUCTION_MANUELLE')).toHaveTextContent('2 traductions manuelles conservées : lignes 5, 7');
    expect(screen.getByTestId('translations-ignored-SANS_TRADUCTION')).toHaveTextContent('1 ligne sans traduction : lignes 9');
  });

  it('a workbook brought back again clears the report of the one before, even when it is refused', { timeout: 15_000 }, async () => {
    openDialog({ json: { written: 3, ignored: [] } });
    await bringBack(await bytesOf(await downloaded()));
    expect(await screen.findByTestId('translations-report', undefined, SLOW)).toHaveTextContent('3 traductions enregistrées.');
    await bringBack('pas un classeur');
    expect(await screen.findByRole('alert', undefined, SLOW)).toHaveTextContent('Ce fichier n’est pas un classeur Excel lisible (.xlsx).');
    expect(screen.queryByTestId('translations-report')).not.toBeInTheDocument();
    // The field is emptied after each choice: the same file chosen again, corrected, is read again.
    expect(screen.getByTestId<HTMLInputElement>('translations-file').value).toBe('');
  });
});

describe('the workbooks refused (B-5-16 to B-5-19, A-2.14-22)', () => {
  it('a workbook of another project, of the 1.x or a file that is no workbook: its message, nothing sent, the map not read again', { timeout: 15_000 }, async () => {
    const { sent, onImported } = openDialog({ json: { written: 0, ignored: [] } });
    const other = await opened(await buildWorkbook({ ...textsOf(), project_id: STEP }, null, french));
    await bringBack(await bytesOf(other));
    expect(await screen.findByRole('alert', undefined, SLOW)).toHaveTextContent('Ce classeur a été téléchargé depuis un autre projet.');
    await bringBack('pas un classeur');
    expect(await screen.findByText('Ce fichier n’est pas un classeur Excel lisible (.xlsx).', undefined, SLOW)).toBeInTheDocument();
    const older = await downloaded();
    older.removeWorksheet(older.worksheets[2]?.id ?? 0);
    await bringBack(await bytesOf(older));
    expect(await screen.findByText('Ce classeur ne vient pas de l’outil Traductions : télécharger le classeur depuis l’étape 1.', undefined, SLOW)).toBeInTheDocument();
    expect(callsTo(sent, 'PUT', LIST_PATH)).toEqual([]);
    expect(onImported).not.toHaveBeenCalled();
  });

  it('TRADUCTIONS_INVALIDES: the message of the host and one line a refused row; nothing is saved, the map is not read again', { timeout: 15_000 }, async () => {
    const { onImported } = openDialog({
      refused: 400, code: 'TRADUCTIONS_INVALIDES', message: 'Le classeur contient des traductions invalides.',
      details: { errors: [{ line: 2, message: 'L2 : Le champ EN dépasse 200 caractères.' }, { line: 3, message: 'L3 : Le champ EN ne peut pas contenir de balises HTML.' }] },
    });
    await bringBack(await bytesOf(await downloaded()));
    expect(await screen.findByRole('alert', undefined, SLOW)).toHaveTextContent('Le classeur contient des traductions invalides.');
    expect(screen.getByTestId('translations-errors').querySelectorAll('li')).toHaveLength(2);
    expect(screen.getByTestId('translations-errors')).toHaveTextContent('L3 : Le champ EN ne peut pas contenir de balises HTML.');
    expect(screen.queryByTestId('translations-report')).not.toBeInTheDocument();
    expect(onImported).not.toHaveBeenCalled();
  });
});
