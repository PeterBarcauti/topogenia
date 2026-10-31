// translatedView.test.tsx
// A map shown in another language than its own (ADR 083): one language on the map, the texts not translated shown to translate, the version of origin locked, a name typed written as its translation, the way back to the original.

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../shared/i18n/i18n.ts';
import { chooseProcessLanguage, processLanguage } from '../../shared/state/processLanguage.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../test/editor.tsx';
import { callsTo, fakeApi } from '../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, replyOf, sampleMap } from '../../test/maps.ts';
import { createMapEditor } from './state/editor.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  processLanguage.setState({ chosen: {} });
});

afterEach(() => {
  vi.unstubAllGlobals();
  processLanguage.setState({ chosen: {} });
});

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;

/** The sample map, whose names are in French and three of them in English, in an editor with three languages. */
async function shownIn(language: string | null, map = sampleMap()) {
  if (language !== null) chooseProcessLanguage(PROJECT_ID, language);
  const editor = createMapEditor({ source: sourceOf(map), storage: memoryStorage(), languages: ['fr', 'en', 'pt'] });
  const { container } = renderMapScreen({ mapId: MAP_ID, editor });
  const canvas = await within(container).findByTestId('map-canvas');
  return { editor, canvas };
}

describe('a map shown in its language of origin (ADR 083)', () => {
  it('is not locked: no banner, every text as entered, the structure as the right of the viewer', async () => {
    const { editor, canvas } = await shownIn(null);
    expect(editor.store.getState().shown).toEqual({ language: 'fr', origin: 'fr', locked: false });
    expect(editor.store.getState().map?.rights.structure).toBe(true);
    expect(screen.queryByTestId('map-translation')).toBeNull();
    expect(within(node(canvas, 's2')).getByTestId('step-label')).toHaveTextContent('Vérifier la demande');
    expect(canvas.querySelectorAll('.untranslated')).toHaveLength(0);
  });
});

describe('a map shown in another language (ADR 083)', () => {
  it('shows every text in it, those not translated in their original to translate; its structure locked, said by a banner', async () => {
    const { editor, canvas } = await shownIn('en');
    expect(editor.store.getState().shown).toEqual({ language: 'en', origin: 'fr', locked: true });
    expect([editor.store.getState().map?.rights.structure, editor.store.getState().translating]).toEqual([false, true]);
    expect(within(node(canvas, 's2')).getByTestId('step-label')).toHaveTextContent('Check the request');
    expect(within(node(canvas, 's2')).getByTestId('step-label')).not.toHaveClass('untranslated');
    const original = within(node(canvas, 's1')).getByTestId('step-label');
    expect([original.textContent, original.className, original.getAttribute('title')]).toEqual(['Demande reçue', 'step-label untranslated', 'À traduire en English']);
    const banner = screen.getByTestId('map-translation');
    expect(banner).toHaveTextContent('Affiché en English : la version d’origine (Français) est verrouillée.');
    // The lane Comptabilité, the steps s1, s3 and s4 and the label « non »: five texts of the map to translate.
    expect(screen.getByTestId('map-translation-missing')).toHaveTextContent('5 textes de cette carte restent à traduire.');
    expect(screen.queryByTestId('map-read-only')).toBeNull();
  });

  it('a name typed on a step is its translation into the language shown, never its label', async () => {
    const sent = fakeApi({ 'PUT /api/steps/s1/translations': { json: replyOf(sampleMap()) } });
    const { canvas } = await shownIn('en');
    fireEvent.doubleClick(node(canvas, 's1'));
    const field = within(canvas).getByTestId('step-label-input');
    expect([(field as HTMLTextAreaElement).value, field.getAttribute('placeholder')]).toEqual(['', 'Demande reçue']);
    await userEvent.type(field, 'Request received{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s1/translations')[0]?.body).toEqual({ translations: { en: 'Request received' } });
    });
    expect(callsTo(sent, 'PATCH', '/api/steps/s1')).toEqual([]);
  });

  it('a name typed on a lane is its translation too, the one already written to correct', async () => {
    const sent = fakeApi({ 'PATCH /api/lanes/l1': { json: replyOf(sampleMap()) } });
    await shownIn('en');
    fireEvent.doubleClick(screen.getByTestId('lane-head-VTE-C01'));
    const field = screen.getByTestId('lane-name-input');
    expect((field as HTMLTextAreaElement).value).toBe('Sales');
    await userEvent.clear(field);
    await userEvent.type(field, 'Sales team{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/lanes/l1')[0]?.body).toEqual({ translations: { en: 'Sales team' } });
    });
  });

  it('a reader reads the translation, the banner without the way to type; « Afficher en Français » goes back to the original', async () => {
    const { editor } = await shownIn('en', sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(editor.store.getState().translating).toBe(false);
    expect(screen.getByTestId('map-translation')).toHaveTextContent('Affiché en English, traduction de la version d’origine (Français).');
    expect(screen.queryByTestId('map-translation-assistant')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Afficher en Français' }));
    expect(processLanguage.getState().chosen[PROJECT_ID]).toBe('fr');
    await vi.waitFor(() => {
      expect(screen.queryByTestId('map-translation')).toBeNull();
    });
  });

  it('the language of the interface shows the process when half of it is translated into it at least, else its language of origin', async () => {
    await i18next.changeLanguage('en');
    const half = await shownIn(null, sampleMap({ languages: { origin: 'fr', total: 8, written: { fr: 8, en: 4 } } }));
    expect(half.editor.store.getState().shown?.language).toBe('en');
    act(() => {
      processLanguage.setState({ chosen: {} });
    });
    // Three texts of nine in English: the map of its author is not locked for so little.
    const little = await shownIn(null, sampleMap({ languages: { origin: 'fr', total: 9, written: { fr: 9, en: 3 } } }));
    expect(little.editor.store.getState().shown?.language).toBe('fr');
  });

  it('« Traduire avec l’assistant » of the banner opens the translations on the language shown', async () => {
    const { editor } = await shownIn('pt');
    await userEvent.click(screen.getByTestId('map-translation-assistant'));
    expect(editor.floating.getState().floating).toEqual({ kind: 'translations', assistant: 'pt' });
  });
});
