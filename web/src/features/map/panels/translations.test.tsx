// translations.test.tsx
// The translations over the map: the button of the header with its dialog (B-5-01), the map read again after an import (B-5-24), « Traduire… » in the menus of a step, a flow and a lane (B-5-28).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { openProject } from '../../../shared/state/openProject.ts';
import { memoryStorage, renderMapScreen } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, replyOf, sampleMap } from '../../../test/maps.ts';
import { bytesOf, detailOf, french, opened, textsOf } from '../../../test/translations.ts';
import { buildWorkbook } from '../../translations/model/workbook.ts';
import { createMapEditor } from '../state/editor.ts';
import type { MapSource } from '../state/mapStore.ts';

/** The map shown in an editor with the languages of the configuration given; its readings counted. */
async function showWith(languages: readonly string[], map: Schema<'MapView'> = sampleMap()) {
  const notes: string[] = [];
  const reads: string[] = [];
  const source: MapSource = (mapId) => {
    reads.push(mapId);
    return Promise.resolve({ map, history: { undo: [], redo: [] } });
  };
  const editor = createMapEditor({ source, storage: memoryStorage(), notify: (text) => notes.push(text), languages });
  const { container } = renderMapScreen({ mapId: MAP_ID, editor });
  const canvas = await within(container).findByTestId('map-canvas');
  return { editor, canvas, notes, reads };
}

const rightClick = (element: Element) => fireEvent.contextMenu(element, { clientX: 300, clientY: 200 });

// exceljs reads and writes a workbook slowly in jsdom, more under the whole suite: the waits for it are wider.
const SLOW = { timeout: 5_000 };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the button Translations of the header and the map read again (B-5-01, B-5-24)', () => {
  it('declared with its screen for who may change the structure; it opens the dialog of the project; an import reads the map again', { timeout: 15_000 }, async () => {
    const sent = fakeApi({
      [`GET /api/projects/${PROJECT_ID}`]: detailOf(null), [`PUT /api/projects/${PROJECT_ID}/translations`]: { json: { written: 1, ignored: [] } },
    });
    const { reads } = await showWith(['fr', 'en']);
    const project = openProject.getState().project;
    expect([project?.canEdit, typeof project?.openTranslations]).toEqual([true, 'function']);
    act(() => project?.openTranslations?.());
    expect(screen.getByRole('dialog', { name: 'Traduire le projet' })).toBeInTheDocument();
    const book = await opened(await buildWorkbook({ ...textsOf(), project_id: PROJECT_ID }, null, french));
    await userEvent.upload(screen.getByTestId('translations-file'), new File([await bytesOf(book)], 'VTE_traductions.xlsx'));
    expect(await screen.findByTestId('translations-report', undefined, SLOW)).toHaveTextContent('1 traduction enregistrée.');
    await vi.waitFor(() => {
      expect(reads).toEqual([MAP_ID, MAP_ID]);
    });
    expect(callsTo(sent, 'PUT', `/api/projects/${PROJECT_ID}/translations`)).toHaveLength(1);
  });

  it('nothing to translate with one language configured: no button; a reader may not change the structure', async () => {
    await showWith(['fr']);
    expect(openProject.getState().project?.openTranslations).toBeUndefined();
    await showWith(['fr', 'en'], sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(openProject.getState().project?.canEdit).toBe(false);
  });
});

describe('« Traduire… » in the menus (B-5-28, spec 10)', () => {
  it('a step: its text entered and one field per other language; only the languages changed are sent with its version', async () => {
    const sent = fakeApi({ 'PUT /api/steps/s2/translations': { json: replyOf(sampleMap()) } });
    const { canvas } = await showWith(['fr', 'en', 'es']);
    rightClick(within(canvas).getByTestId('rf__node-s2'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Traduire…' }));
    const dialog = screen.getByRole('dialog', { name: 'Traductions de « Vérifier la demande »' });
    expect(within(dialog).getByTestId('manual-translations-original')).toHaveTextContent('Vérifier la demande');
    expect(within(dialog).getByText('Texte saisi (Français)')).toBeInTheDocument();
    // A field for each language the name is translated into, the language shown and those of the project; the others by « Ajouter une langue » (ADR 083).
    expect(within(dialog).queryByLabelText('Traduction (Español)')).toBeNull();
    await userEvent.selectOptions(within(dialog).getByTestId('manual-translations-add'), 'es');
    expect([within(dialog).getByLabelText('Traduction (English)'), within(dialog).getByLabelText('Traduction (Español)')].map((field) => (field as HTMLInputElement).value))
      .toEqual(['Check the request', '']);
    expect(within(dialog).getByLabelText('Traduction (English)')).toHaveAttribute('maxlength', '150');
    expect(within(dialog).queryByLabelText('Traduction (Français)')).toBeNull();
    expect(within(dialog).getByTestId('manual-translations-save')).toBeDisabled();
    await userEvent.type(within(dialog).getByLabelText('Traduction (Español)'), ' Verificar la solicitud ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Enregistrer' }));
    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
    const put = callsTo(sent, 'PUT', '/api/steps/s2/translations')[0];
    expect([put?.body, put?.headers.get('If-Match')]).toEqual([{ translations: { es: 'Verificar la solicitud' } }, '"1"']);
  });

  it('a labelled flow and a lane by their own routes; a field emptied sends an empty text, which deletes the translation', async () => {
    const english = sampleMap();
    const map = { ...english, steps: english.steps.map((step) => (step.id === 's3' ? { ...step, source_language: 'en' } : step)) };
    const saved = { ...map, flows: map.flows.map((flow) => (flow.id === 'f3' ? { ...flow, translations: { fr: 'oui !' } } : flow)) };
    const sent = fakeApi({ 'PUT /api/flows/f3/translations': { json: replyOf(saved) }, 'PATCH /api/lanes/l1': { json: replyOf(saved) } });
    const { canvas } = await showWith(['fr', 'en'], map);
    rightClick(within(canvas).getByTestId('rf__edge-f3'));
    await userEvent.click(screen.getByTestId('menu-flow-flow.translate'));
    // The language of origin of a flow is the one of its source step (A-2.14-06): « Conforme ? » is English here.
    expect(screen.getByText('Texte saisi (English)')).toBeInTheDocument();
    expect(screen.queryByLabelText('Traduction (English)')).toBeNull();
    await userEvent.type(screen.getByLabelText('Traduction (Français)'), 'oui !{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f3/translations')[0]?.body).toEqual({ translations: { fr: 'oui !' } });
    });
    rightClick(within(canvas).getByTestId('rf__edge-f3'));
    await userEvent.click(screen.getByTestId('menu-flow-flow.translate'));
    expect(screen.getByLabelText('Traduction (Français)')).toHaveAttribute('maxlength', '60');
    await userEvent.clear(screen.getByLabelText('Traduction (Français)'));
    await userEvent.keyboard('{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f3/translations')[1]?.body).toEqual({ translations: { fr: '' } });
    });
    fireEvent.contextMenu(screen.getByTestId('lane-head-VTE-C01'), { clientX: 50, clientY: 50 });
    await userEvent.click(screen.getByTestId('menu-lane-lane.translate'));
    await userEvent.clear(screen.getByLabelText('Traduction (English)'));
    await userEvent.type(screen.getByLabelText('Traduction (English)'), 'Sales team{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/lanes/l1')[0]?.body).toEqual({ translations: { en: 'Sales team' } });
    });
  });

});

describe('« Traduire… » refused or not offered (B-5-28, spec 10)', () => {
  it('not offered with one language, for a flow without label; a refusal keeps the dialog open and is said', async () => {
    fakeApi({ 'PUT /api/steps/s2/translations': { refused: 400, code: 'TEXTE_HTML_INTERDIT', message: 'Le champ EN ne peut pas contenir de balises HTML.' } });
    const single = await showWith(['fr']);
    rightClick(within(single.canvas).getByTestId('rf__node-s2'));
    expect(screen.queryByTestId('menu-step-step.translate')).toBeNull();
    const reader = await showWith(['fr', 'en'], sampleMap({ rights: { structure: false, role: 'reader' } }));
    rightClick(within(reader.canvas).getAllByTestId('rf__node-s2').at(-1) as Element);
    expect(screen.getAllByTestId('menu-step-step.copy-code')).toHaveLength(2);
    expect(screen.queryAllByTestId('menu-step-step.translate')).toEqual([]);
    const { canvas, notes } = await showWith(['fr', 'en']);
    rightClick(within(canvas).getAllByTestId('rf__edge-f1').at(-1) as Element);
    expect(screen.getByTestId('menu-flow-flow.label')).toBeInTheDocument();
    expect(screen.queryByTestId('menu-flow-flow.translate')).toBeNull();
    rightClick(within(canvas).getAllByTestId('rf__node-s2').at(-1) as Element);
    await userEvent.click(screen.getByTestId('menu-step-step.translate'));
    await userEvent.type(screen.getByLabelText('Traduction (English)'), ' <b>');
    await userEvent.click(screen.getByTestId('manual-translations-save'));
    await vi.waitFor(() => {
      expect(notes).toEqual(['Le champ EN ne peut pas contenir de balises HTML.']);
    });
    expect(screen.getByRole('dialog', { name: 'Traductions de « Vérifier la demande »' })).toBeInTheDocument();
    expect(screen.getByTestId('manual-translations-save')).toBeEnabled();
  });
});
