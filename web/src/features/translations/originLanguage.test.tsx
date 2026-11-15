// originLanguage.test.tsx
// The language of origin of a project in the dialog of the translations (ADR 100): offered for a language wholly translated, the missing texts said for the others (the comments of presentation among them, unknown when they could not be read), confirmed with what it forgets before it is sent with the version of the project, a refusal said.

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../../shared/keyboard/KeyboardContext.tsx';
import { callsTo, fakeApi, type FakeReply, type FakeRoutes } from '../../test/fakeApi.ts';
import { commentOf, detailOf, flowCommentOf, PROJECT, STEP, textsOf } from '../../test/translations.ts';
import { missingIn } from './OriginLanguage.tsx';
import { TranslationsDialog } from './TranslationsDialog.tsx';

const PATH = `/api/projects/${PROJECT}/origin-language`;
const LIST_PATH = `/api/projects/${PROJECT}/translations`;
const LIST = `GET ${LIST_PATH}`;

/** What the host lists as lacking a translation into a language: here no text of the map, and the comments of presentation given. */
const lacking = (comments: number): FakeReply => ({
  json: {
    ...textsOf(['fr', 'en'], 'missing', Array.from({ length: comments }, () => (
      { step_id: STEP, code: 'VIS-E001', context: 'Visites / Accueil', language: 'fr', original: 'Le visiteur confirme sa venue.', translations: {}, sources: {} }))),
    texts: [],
  },
});

/** The dialog of a French project of three texts, written so far in the languages given; what follows a change is kept. */
function openDialog(written: Record<string, number>, routes: FakeRoutes = {}, follows = true) {
  const changed = vi.fn();
  const imported = vi.fn();
  const sent = fakeApi({ [`GET /api/projects/${PROJECT}`]: detailOf(null, 7), [LIST]: lacking(0), ...routes });
  const view = render(
    <KeyboardProvider>
      <TranslationsDialog project={{ id: PROJECT, code: 'VIS' }} languages={['fr', 'en', 'de', 'es']} coverage={{ origin: 'fr', total: 3, written: { fr: 3, ...written } }}
        onImported={imported} onClose={vi.fn()} {...(follows ? { onOriginChanged: changed } : {})} />
    </KeyboardProvider>,
  );
  return { sent, changed, imported, unmount: view.unmount };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the language of origin in the dialog of the translations (ADR 100)', () => {
  it('a language wholly translated offers to become the language of origin; one that lacks texts says how many; one never translated is not listed', async () => {
    const { sent } = openDialog({ en: 3, de: 1 });
    expect(screen.getByRole('heading', { name: 'Langue d’origine : Français' })).toBeInTheDocument();
    expect(await within(screen.getByTestId('translations-origin-en')).findByRole('button', { name: 'Faire de cette langue la langue d’origine' })).toBeEnabled();
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-de').textContent).toBe('DeutschTextes manquants : 2');
    });
    // The comments of presentation that lack are asked of the host, language by language, as the workbook lists them.
    expect(callsTo(sent, 'GET', LIST_PATH).map((call) => [call.query.get('scope'), call.query.get('languages')])).toEqual([['missing', 'en'], ['missing', 'de']]);
    expect(within(screen.getByTestId('translations-origin-de')).queryByRole('button')).toBeNull();
    expect(screen.queryByTestId('translations-origin-es')).toBeNull();
    // The language of origin is the title of the list, never one of its rows.
    expect(screen.queryByTestId('translations-origin-fr')).toBeNull();
    expect([missingIn({ origin: 'fr', total: 3, written: { fr: 3, en: 5 } }, 'en'), missingIn({ origin: 'fr', total: 3, written: { fr: 3 } }, 'pt')]).toEqual([0, 3]);
  });

  it('a comment of presentation without its line in a language is a text that lacks: the language is not offered, and the count says how many are comments (audit 3.2 UX-MAJ-04)', async () => {
    openDialog({ en: 3, de: 1 }, { [LIST]: [lacking(1), lacking(2)] });
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-en').textContent).toBe('EnglishTextes manquants : 1, dont 1 commentaire de présentation');
    });
    expect(within(screen.getByTestId('translations-origin-en')).queryByRole('button')).toBeNull();
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-de').textContent).toBe('DeutschTextes manquants : 4, dont 2 commentaires de présentation');
    });
  });

  it('the comment of a flow without its line in a language lacks as the one of a step does, counted with them (ADR 099, lot J10)', async () => {
    const onFlows = (steps: number, flows: number): FakeReply => ({
      json: { ...textsOf(['fr', 'en'], 'missing', Array.from({ length: steps }, commentOf), Array.from({ length: flows }, flowCommentOf)), texts: [] },
    });
    openDialog({ en: 3, de: 1 }, { [LIST]: [onFlows(0, 1), onFlows(1, 2)] });
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-en').textContent).toBe('EnglishTextes manquants : 1, dont 1 commentaire de présentation');
    });
    expect(within(screen.getByTestId('translations-origin-en')).queryByRole('button')).toBeNull();
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-de').textContent).toBe('DeutschTextes manquants : 5, dont 3 commentaires de présentation');
    });
  });

  it('while the comments are read, a language whose texts are all translated is not offered', () => {
    openDialog({ en: 3 }, { [LIST]: { pending: true } });
    expect(within(screen.getByTestId('translations-origin-en')).queryByRole('button')).toBeNull();
    expect(screen.getByTestId('translations-origin-en')).toHaveTextContent('Chargement');
  });

  it('when the comments cannot be read, the language is not offered either, and its count is said unknown, not loading (audit 3.2 UX-MAJ-04)', async () => {
    const { sent } = openDialog({ en: 3, de: 1 }, { [LIST]: { refused: 500, code: 'ERREUR_INTERNE' } });
    await waitFor(() => {
      expect(callsTo(sent, 'GET', LIST_PATH)).toHaveLength(2);
    });
    await waitFor(() => {
      expect(['en', 'de'].map((code) => screen.getByTestId(`translations-origin-${code}`).textContent)).toEqual(['EnglishTextes manquants : nombre inconnu', 'DeutschTextes manquants : nombre inconnu']);
    });
    expect(within(screen.getByTestId('translations-origin-en')).queryByRole('button')).toBeNull();
  });

  it('is not shown while no language has a translation', () => {
    const first = openDialog({});
    expect(screen.queryByTestId('translations-origin')).toBeNull();
    expect(first.changed).not.toHaveBeenCalled();
  });

  it('is not shown where nothing follows the change', () => {
    openDialog({ en: 3 }, {}, false);
    expect(screen.queryByTestId('translations-origin')).toBeNull();
  });

});

describe('the change of the language of origin asked from the dialog (ADR 100)', () => {
  it('confirmed, is sent with the version of the project read just before, then what follows it is called', async () => {
    const { sent, changed } = openDialog({ en: 3 }, { [`PUT ${PATH}`]: detailOf(null, 8) });
    await userEvent.click(await screen.findByTestId('translations-origin-make-en'));
    const confirm = screen.getByRole('dialog', { name: 'Changer la langue d’origine' });
    expect(confirm).toHaveTextContent('Nouvelle langue d’origine : Anglais. Ses textes deviennent les originaux ; ceux de l’ancienne (Français) deviennent leurs traductions.');
    // What the change forgets is said before it is asked (audit 3.2 UX-MAJ-05, ADR 100 point 7).
    expect(confirm).toHaveTextContent('L’historique des annulations du projet est vidé, sur toutes ses cartes et pour tous ses membres.');
    expect(confirm).toHaveTextContent('Ctrl+Z ne défait pas ce changement');
    // Nothing is sent before the confirmation, and the dialog of the translations gives its place to the question.
    expect([callsTo(sent, 'PUT', PATH).length, screen.queryByRole('dialog', { name: 'Traduire le projet' })]).toEqual([0, null]);
    await userEvent.click(within(confirm).getByRole('button', { name: 'Changer' }));
    await waitFor(() => {
      expect(changed).toHaveBeenCalledWith('en');
    });
    const call = callsTo(sent, 'PUT', PATH)[0];
    expect([call?.body, call?.headers.get('If-Match')]).toEqual([{ language: 'en' }, '"7"']);
  });

  it('cancelled, sends nothing and gives the dialog of the translations back', async () => {
    const { sent, changed } = openDialog({ en: 3 });
    await userEvent.click(await screen.findByTestId('translations-origin-make-en'));
    await userEvent.click(screen.getByTestId('translations-origin-confirm-cancel'));
    expect(screen.getByRole('dialog', { name: 'Traduire le projet' })).toBeInTheDocument();
    expect([callsTo(sent, 'PUT', PATH).length, changed.mock.calls.length]).toEqual([0, 0]);
  });

  it('refused, says the refusal in the dialog and nothing follows', async () => {
    const { sent, changed, imported } = openDialog({ en: 3 }, {
      [`PUT ${PATH}`]: { refused: 409, code: 'TRADUCTION_INCOMPLETE', message: 'Des textes du projet ne sont pas traduits dans cette langue.' }, [LIST]: [lacking(0), lacking(1)],
    });
    await userEvent.click(await screen.findByTestId('translations-origin-make-en'));
    await userEvent.click(screen.getByTestId('translations-origin-confirm-confirm'));
    expect(await screen.findByTestId('translations-status')).toHaveTextContent('Des textes du projet ne sont pas traduits dans cette langue.');
    expect(screen.getByRole('dialog', { name: 'Traduire le projet' })).toBeInTheDocument();
    expect(changed).not.toHaveBeenCalled();
    // What lacks is read again, the map too: the language refused is no longer offered.
    await waitFor(() => {
      expect(screen.getByTestId('translations-origin-en').textContent).toBe('EnglishTextes manquants : 1, dont 1 commentaire de présentation');
    });
    expect([callsTo(sent, 'GET', LIST_PATH).length, imported.mock.calls.length]).toEqual([2, 1]);
  });
});
