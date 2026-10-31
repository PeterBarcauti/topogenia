// translationsDialog.test.tsx
// The dialog of the translations: scope, workbook downloaded under its name, nothing to translate, the glossary of the project saved with its version, the instruction copied (B-5-02 to B-5-15, B-8.1-45).

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../shared/keyboard/KeyboardContext.tsx';
import { captureDownloads } from '../../test/downloads.ts';
import { callsTo, fakeApi, type FakeRoutes } from '../../test/fakeApi.ts';
import { detailOf, opened, PROJECT, textsOf } from '../../test/translations.ts';
import { TranslationsDialog } from './TranslationsDialog.tsx';

const LIST_PATH = `/api/projects/${PROJECT}/translations`;
const LIST = `GET ${LIST_PATH}`;

// exceljs reads and writes a workbook slowly in jsdom, more under the whole suite: the waits for it are wider.
const SLOW = { timeout: 5_000 };

/** The names of the three choices of the scope, as a screen reader reads them. */
const MISSING = 'Sans traduction · seulement les textes à qui il manque une traduction';
const AUTO = 'Sans traduction manuelle · aussi ceux traduits par un classeur, pour les refaire';
const ALL = 'Tout · tous les textes ; au retour, les traductions saisies à la main sont écrasées';
const GLOSSARY = `PUT /api/projects/${PROJECT}/translation-glossary`;

function openDialog(routes: FakeRoutes, glossary: string | null = null) {
  const sent = fakeApi({ [`GET /api/projects/${PROJECT}`]: detailOf(glossary), ...routes });
  render(<KeyboardProvider><TranslationsDialog project={{ id: PROJECT, code: 'VTE' }} languages={['fr', 'en']} coverage={{ origin: 'fr', total: 3, written: { fr: 3, en: 1 } }} onImported={vi.fn()} onClose={vi.fn()} /></KeyboardProvider>);
  return sent;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the scope and the download (B-5-02 to B-5-06, B-5-13, B-5-14)', () => {
  it('a wide dialog in three steps; « Sans traduction » by default; the workbook of the texts is downloaded under its name', { timeout: 15_000 }, async () => {
    const sent = openDialog({ [LIST]: { json: textsOf() } });
    const capture = captureDownloads();
    try {
      expect(screen.getByRole('dialog', { name: 'Traduire le projet' })).toHaveClass('dialog--wide');
      expect(screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent))
        .toEqual(['Ou par un classeur Excel', '1. Télécharger les textes à traduire', '2. Faire compléter les cellules vides', '3. Rapporter le classeur traduit']);
      expect(screen.getByRole('radio', { name: MISSING })).toBeChecked();
      await userEvent.click(screen.getByRole('button', { name: 'Télécharger le classeur' }));
      await waitFor(() => {
        expect(capture.files).toHaveLength(1);
      }, SLOW);
      expect(capture.files[0]?.name).toMatch(/^VTE_traductions_\d{4}-\d{2}-\d{2}\.xlsx$/);
      expect(callsTo(sent, 'GET', LIST_PATH)[0]?.query.get('scope')).toBe('missing');
      // The columns of the workbook: the language of origin and the language already translated (ADR 083).
      expect(callsTo(sent, 'GET', LIST_PATH)[0]?.query.get('languages')).toBe('fr,en');
      expect((await opened(capture.files[0]?.content as Blob)).getWorksheet('Traductions')?.rowCount).toBe(4);
      expect(screen.getByTestId('translations-status')).toHaveTextContent('3 textes à traduire : le classeur est dans le dossier Téléchargements.');
    } finally {
      capture.restore();
    }
  });

  it('« Sans traduction manuelle » and « Tout » are asked of the host as auto and all; an empty list downloads nothing', { timeout: 15_000 }, async () => {
    const sent = openDialog({ [LIST]: [{ json: textsOf(['fr', 'en'], 'auto') }, { json: { ...textsOf(['fr', 'en'], 'all'), texts: [] } }] });
    const capture = captureDownloads();
    try {
      await userEvent.click(screen.getByRole('radio', { name: AUTO }));
      expect(screen.getByRole('radio', { name: AUTO })).toBeChecked();
      expect(screen.getByRole('radio', { name: MISSING })).not.toBeChecked();
      await userEvent.click(screen.getByTestId('translations-download'));
      await waitFor(() => {
        expect(capture.files).toHaveLength(1);
      }, SLOW);
      await userEvent.click(screen.getByRole('radio', { name: ALL }));
      await userEvent.click(screen.getByTestId('translations-download'));
      await waitFor(() => {
        expect(screen.getByTestId('translations-status')).toHaveTextContent('Rien à traduire pour ce choix.');
      });
      expect(callsTo(sent, 'GET', LIST_PATH).map((call) => call.query.get('scope'))).toEqual(['auto', 'all']);
      expect(capture.files).toHaveLength(1);
    } finally {
      capture.restore();
    }
  });
});

describe('the glossary of the project (B-5-11, B-8.1-45)', () => {
  it('read with the project; changed, it is saved with the version of the project before the download and goes into the workbook', { timeout: 15_000 }, async () => {
    const saved = 'clôture = closing\nécart = variance';
    const sent = openDialog({ [GLOSSARY]: detailOf(saved, 5), [LIST]: { json: textsOf() } }, 'clôture = closing');
    expect(screen.getByTestId('translations-glossary')).toBeDisabled();
    const capture = captureDownloads();
    try {
      const field = await screen.findByDisplayValue('clôture = closing');
      expect(screen.getByTestId('translations-glossary-save')).toBeDisabled();
      await userEvent.type(field, '{Enter}écart = variance  ');
      await userEvent.click(screen.getByTestId('translations-download'));
      await waitFor(() => {
        expect(capture.files).toHaveLength(1);
      }, SLOW);
      const put = callsTo(sent, 'PUT', `/api/projects/${PROJECT}/translation-glossary`)[0];
      expect([put?.body, put?.headers.get('If-Match')]).toEqual([{ glossary: saved }, '"4"']);
      expect(sent.map((call) => call.method)).toEqual(['GET', 'PUT', 'GET']);
      const cells: string[] = [];
      (await opened(capture.files[0]?.content as Blob)).getWorksheet('Consigne')?.eachRow((row) => cells.push(row.getCell(1).text));
      expect(cells.slice(-3)).toEqual(['Glossaire du projet', 'clôture = closing', 'écart = variance']);
      expect(screen.getByTestId('translations-glossary-save')).toBeDisabled();
    } finally {
      capture.restore();
    }
  });

  it('« Enregistrer le glossaire » saves it alone; a field emptied clears it; a conflict reads it again and stops the download', async () => {
    const sent = openDialog({
      [GLOSSARY]: [detailOf(null, 5), { refused: 409, code: 'CONFLIT_VERSION' }],
      [`GET /api/projects/${PROJECT}`]: [detailOf('achat = purchase'), detailOf('vente = sale', 9)],
    }, 'achat = purchase');
    await userEvent.clear(await screen.findByDisplayValue('achat = purchase'));
    await userEvent.click(screen.getByRole('button', { name: 'Enregistrer le glossaire' }));
    await waitFor(() => {
      expect(screen.getByTestId('translations-status')).toHaveTextContent('Glossaire enregistré.');
    });
    expect(callsTo(sent, 'PUT', `/api/projects/${PROJECT}/translation-glossary`)[0]?.body).toEqual({ glossary: null });
    await userEvent.type(screen.getByTestId('translations-glossary'), 'remise = discount');
    await userEvent.click(screen.getByTestId('translations-download'));
    expect(await screen.findByDisplayValue('vente = sale')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Le glossaire a été modifié entre-temps : voici celui qui est enregistré.');
    expect(callsTo(sent, 'PUT', `/api/projects/${PROJECT}/translation-glossary`)[1]?.headers.get('If-Match')).toBe('"5"');
    expect(callsTo(sent, 'GET', LIST_PATH)).toEqual([]);
  });
});

describe('while the host answers', () => {
  it('the choice, the glossary and the two buttons that call the host wait for its answer', async () => {
    openDialog({ [LIST]: { pending: true } });
    await waitFor(() => {
      expect(screen.getByTestId('translations-glossary')).toBeEnabled();
    });
    await userEvent.click(screen.getByTestId('translations-download'));
    expect(screen.getByTestId('translations-scope')).toBeDisabled();
    expect([screen.getByTestId('translations-glossary'), screen.getByTestId('translations-download'), screen.getByTestId('translations-import')])
      .toEqual([expect.toBeDisabled(), expect.toBeDisabled(), expect.toBeDisabled()]);
    expect(screen.getByTestId('translations-copy')).toBeEnabled();
  });
});

describe('the instruction for Copilot (B-5-15)', () => {
  it('« Copier la consigne pour Copilot » puts the instruction and the glossary of the field in the clipboard, and says it', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    openDialog({}, 'clôture = closing');
    await screen.findByDisplayValue('clôture = closing');
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    await userEvent.click(screen.getByRole('button', { name: 'Copier la consigne pour Copilot' }));
    await waitFor(() => {
      expect(screen.getByTestId('translations-status')).toHaveTextContent('Consigne copiée : la coller dans Copilot.');
    });
    const text = (writeText.mock.calls[0] as unknown as [string])[0];
    expect(text).toContain('colonnes de langue (FR, EN)');
    expect(text.endsWith('Glossaire du projet\nclôture = closing')).toBe(true);
  });
});

describe('the assistant and the languages of the workbook (ADR 083)', () => {
  const coverage = { origin: 'fr', total: 4, written: { fr: 4, en: 2 } };

  function openWith(props: { preselected?: string; onAssistant?: (language: string) => void }) {
    const sent = fakeApi({ [`GET /api/projects/${PROJECT}`]: detailOf(null), [LIST]: { json: textsOf() } });
    render(<KeyboardProvider><TranslationsDialog project={{ id: PROJECT, code: 'VTE' }} languages={['fr', 'en', 'pt']} coverage={coverage} onImported={vi.fn()} onClose={vi.fn()} {...props} /></KeyboardProvider>);
    return sent;
  }

  it('the assistant comes first: each language but the one of origin, how far it is translated; one chosen is asked', async () => {
    const onAssistant = vi.fn();
    openWith({ onAssistant });
    const section = within(screen.getByTestId('translations-assistant'));
    expect(section.getAllByRole('radio').map((card) => card.getAttribute('data-testid'))).toEqual(['translations-assistant-en', 'translations-assistant-pt']);
    expect(screen.getByTestId('translations-assistant-en')).toHaveTextContent(/English.*Anglais.*50\s?%/u);
    expect(screen.getByTestId('translations-assistant-pt')).toHaveTextContent(/Português.*Portugais.*0\s?%/u);
    expect(screen.getByTestId('translations-assistant-run')).toBeDisabled();
    await userEvent.click(screen.getByTestId('translations-assistant-pt'));
    expect(screen.getByTestId('translations-assistant-pt')).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('button', { name: 'Traduire en Português' }));
    expect(onAssistant).toHaveBeenCalledWith('pt');
  });

  it('without the assistant, the dialog keeps the workbook alone', () => {
    openWith({});
    expect(screen.queryByTestId('translations-assistant')).not.toBeInTheDocument();
  });

  it('the workbook gives the languages ticked, those translated and the one shown first; none ticked, nothing to download', { timeout: 15_000 }, async () => {
    const sent = openWith({ preselected: 'pt' });
    expect(screen.getByTestId('translations-language-en')).toBeChecked();
    expect(screen.getByTestId('translations-language-pt')).toBeChecked();
    expect(screen.queryByTestId('translations-language-fr')).not.toBeInTheDocument();
    const capture = captureDownloads();
    try {
      await userEvent.click(screen.getByRole('button', { name: 'Télécharger le classeur' }));
      await waitFor(() => {
        expect(capture.files).toHaveLength(1);
      }, SLOW);
      expect(callsTo(sent, 'GET', LIST_PATH)[0]?.query.get('languages')).toBe('fr,en,pt');
    } finally {
      capture.restore();
    }
    await userEvent.click(screen.getByTestId('translations-language-en'));
    await userEvent.click(screen.getByTestId('translations-language-pt'));
    expect(screen.getByRole('button', { name: 'Télécharger le classeur' })).toBeDisabled();
  });
});
