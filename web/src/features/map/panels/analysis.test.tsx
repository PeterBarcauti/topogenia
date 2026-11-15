// analysis.test.tsx
// The layer « Analysis » of the Makigami (plan 3.1): its marks as the core gives them, the handoffs drawn and numbered under the focus, the badge of a detailed step, its tile that puts the handoffs forward, its panel that turns the layer off.

import { act, fireEvent, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { sampleMap, streamOf } from '../../../test/maps.ts';
import { ARROW_MARKER, flowElements, HANDOFF_MARKER, VIEW_ONLY, type StepNode } from '../canvas/flowElements.ts';
import { analysisMarksOf, analysisTilesOf, middleOf } from '../model/analysis.ts';
import { layoutMap } from '../model/mapLayout.ts';
import type { TextMeasure } from '../model/textSize.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const handoffs = (overrides: Partial<Schema<'MapHandoffs'>> = {}): Schema<'MapHandoffs'> => ({
  flows: ['f3', 'f2'], detailed_steps: { s2: 2, s3: 0 }, count: 4, per_case: 1.5, ...overrides,
});

/** The value of the steps and the breaks of medium as the core gives them (Makigami M1, M3). */
const valued = (overrides: Partial<Schema<'ValueClassShares'>> = {}): Schema<'ValueClassShares'> => ({
  value_added: 1, necessary: 1, waste: 0, unclassified: 1, value_added_minutes: 30, necessary_minutes: 10, waste_minutes: 0, unclassified_minutes: 40,
  value_added_share: 0.75, coverage: 0.5, ...overrides,
});
const broken: Schema<'MediaBreaks'> = {
  steps: [{ step_id: 's3', changes: [{ from: 'paper', to: 'email' }] }], detailed_steps: { s2: 0 }, count: 1, documents: 4, documents_with_medium: 3,
};
const streamed = (overrides: Partial<Schema<'MapStream'>> = {}) => streamOf({ handoffs: handoffs(), value_classes: valued(), media_breaks: broken, ...overrides });

/** The sample map, its task detailed: two lanes, the decision handing the case over to the accounts and classified necessary. */
const analysed = () => sampleMap({
  steps: sampleMap().steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false } : step.id === 's3' ? { ...step, value_class: 'necessary' } : step)),
  value_stream: streamed(),
});

const plain = (text: string | null | undefined) => text?.replace(/[\u00a0\u202f]/g, ' ');

describe('the marks and the tile (plan 3.1)', () => {
  it('numbers the handoffs in the order the core gives them and keeps a badge for a detailed step that carries some', () => {
    const marks = analysisMarksOf(streamed(), analysed().steps);
    expect([...marks.handoffs]).toEqual([['f3', 1], ['f2', 2]]);
    expect(marks.detailed).toEqual({ s2: 2 });
    expect([marks.classes, [...marks.breaks]]).toEqual([{ s3: 'necessary' }, ['s3']]);
  });

  it('writes the count and the count by case in the language of the interface, or says the count by case unknown', () => {
    const t = i18next.t.bind(i18next);
    expect(analysisTilesOf(streamed(), t, 'fr').map((tile) => [tile.key, plain(tile.value), plain(tile.note)])).toEqual([
      ['handoffs', '4', '1,5 par dossier'], ['value', '75 %', 'Travail classé : 50 %'], ['breaks', '1', 'Supports renseignés : 3 / 4'],
    ]);
    expect(analysisTilesOf(streamed({ handoffs: handoffs({ per_case: null }) }), t, 'fr')[0]?.note).toBe('Par dossier : inconnu');
    expect(analysisTilesOf(streamed(), i18next.getFixedT('en'), 'en')[0]?.note).toBe('1.5 per case');
    // Without the work of the steps, the steps classified are counted, never a share of nought.
    const counted = analysisTilesOf(streamed({ value_classes: valued({ value_added_share: null, coverage: null }) }), t, 'fr')[1];
    expect([counted?.value, counted?.note]).toEqual(['1 / 3', 'Non classées : 1']);
  });

  it('puts the number of a handoff halfway along its path, by its length', () => {
    expect(middleOf([{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }])).toEqual({ x: 10, y: 0 });
    expect(middleOf([{ x: 0, y: 0 }, { x: 0, y: 30 }])).toEqual({ x: 0, y: 15 });
    expect(middleOf([{ x: 1, y: 1 }])).toBeNull();
  });
});

describe('the marks on the canvas (plan 3.1)', () => {
  const six: TextMeasure = (text) => text.length * 6;
  const layout = layoutMap(analysed(), 'fr', six);
  const marks = analysisMarksOf(streamed(), analysed().steps);

  it('draws nothing of its own while the layer is off', () => {
    const { nodes, edges } = flowElements(layout, VIEW_ONLY);
    expect(edges.every((edge) => edge.markerEnd === ARROW_MARKER && edge.data?.handoff === null && !edge.data.faded)).toBe(true);
    expect(nodes.filter((node): node is StepNode => node.type === 'step').every((node) => node.data.handoffs === null && !node.data.faded)).toBe(true);
  });

  it('marks the handoffs with their head, numbers them only under the focus, and fades the rest', () => {
    const byId = <T extends { id: string }>(items: T[]) => [...items].sort((a, b) => a.id.localeCompare(b.id));
    const on = flowElements(layout, { ...VIEW_ONLY, analysis: marks });
    expect(byId(on.edges).map((edge) => [edge.id, edge.data?.handoff, edge.markerEnd, edge.data?.rankAt === null])).toEqual([
      ['f1', null, ARROW_MARKER, true], ['f2', 2, HANDOFF_MARKER, true], ['f3', 1, HANDOFF_MARKER, true], ['f4', null, ARROW_MARKER, true],
    ]);
    const focused = flowElements(layout, { ...VIEW_ONLY, analysis: marks, focus: { tile: 'handoffs', held: true } });
    expect(byId(focused.edges).map((edge) => [edge.id, edge.data?.faded, edge.data?.rankAt !== null])).toEqual([
      ['f1', true, false], ['f2', false, true], ['f3', false, true], ['f4', true, false],
    ]);
    const steps = focused.nodes.filter((node): node is StepNode => node.type === 'step');
    expect(byId(steps).map((node) => [node.id, node.data.faded, node.data.handoffs])).toEqual([['s1', true, null], ['s2', false, 2], ['s3', false, null], ['s4', false, null]]);
    expect(byId(steps).map((node) => [node.id, node.data.valueClass, node.data.mediaBreak])).toEqual([
      ['s1', null, false], ['s2', null, false], ['s3', 'necessary', true], ['s4', null, false],
    ]);
  });

  it('lights the steps classified for the value, the steps where the medium breaks for the breaks, and fades every flow', () => {
    const byId = <T extends { id: string }>(items: T[]) => [...items].sort((a, b) => a.id.localeCompare(b.id));
    for (const tile of ['value', 'breaks'] as const) {
      const focused = flowElements(layout, { ...VIEW_ONLY, analysis: marks, focus: { tile, held: true } });
      const lit = byId(focused.nodes.filter((node): node is StepNode => node.type === 'step')).filter((node) => !node.data.faded).map((node) => node.id);
      expect(lit).toEqual(['s3']);
      expect(focused.edges.every((edge) => edge.data?.faded === true && edge.data.rankAt === null)).toBe(true);
    }
  });
});

async function withLayer(map = analysed()) {
  const shown = await showEditor(map);
  act(() => {
    shown.editor.preferences.getState().set('showAnalysis', true);
  });
  return { ...shown, panel: screen.getByTestId('analysis-panel') };
}

describe('the panel « Analysis » (plan 3.1)', () => {
  it('opens with its layer, alone, beside the canvas, and gives the detailed step its badge', async () => {
    const { editor, canvas, panel } = await withLayer();
    expect(within(panel).getByRole('heading', { level: 2 })).toHaveTextContent('Analyse');
    expect(within(panel).getByTestId('analysis-value-handoffs')).toHaveTextContent('4');
    expect(plain(within(panel).getByTestId('analysis-note-handoffs').textContent)).toBe('1,5 par dossier');
    expect(within(panel).getByTestId('analysis-tile-handoffs')).toHaveAttribute('title', 'Flèches où le dossier change de mains, détails compris');
    expect(canvas.contains(panel)).toBe(false);
    expect(within(canvas).getByTestId('step-handoffs')).toHaveTextContent('2');
    expect(within(canvas).getByTestId('step-handoffs')).toHaveAttribute('title', 'Relais dans le détail : 2');
    expect(editor.preferences.getState()).toMatchObject({ showRaci: false, showValueStream: false, showAnalysis: true });
  });

  it('the badge of the handoffs takes the place of « détaillée » only: the frequency of the detailed step stays under it (audit 3.1 V3, U6)', async () => {
    const map = analysed();
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sheet: { ...step.sheet, frequency: 'weekly' as const } } : step));
    const { canvas } = await withLayer({ ...map, steps });
    const detailed = within(canvas).getByTestId('step-VIS-E002');
    expect([within(detailed).getByTestId('step-handoffs').textContent, within(detailed).queryByTestId('step-detailed'), within(detailed).queryByTestId('step-frequency')?.textContent])
      .toEqual(['2', null, 'Hebdomadaire']);
  });

  it('puts the handoffs forward while its tile is hovered, holds them on a click until a second one', async () => {
    const { editor, panel } = await withLayer();
    const tile = within(panel).getByTestId('analysis-tile-handoffs');
    fireEvent.mouseEnter(tile);
    expect(editor.store.getState().analysisFocus).toEqual({ tile: 'handoffs', held: false });
    fireEvent.mouseLeave(tile);
    expect(editor.store.getState().analysisFocus).toBeNull();
    fireEvent.click(tile);
    expect(tile).toHaveAttribute('aria-pressed', 'true');
    fireEvent.mouseLeave(tile);
    expect(editor.store.getState().analysisFocus).toEqual({ tile: 'handoffs', held: true });
    fireEvent.click(tile);
    expect(editor.store.getState().analysisFocus).toBeNull();
    expect(tile).toHaveAttribute('aria-pressed', 'false');
  });

  it('turns its layer off when closed, and the focus goes with it', async () => {
    const { editor, panel } = await withLayer();
    fireEvent.click(within(panel).getByTestId('analysis-tile-handoffs'));
    fireEvent.click(within(panel).getByTestId('analysis-panel-close'));
    expect(editor.preferences.getState().showAnalysis).toBe(false);
    expect(screen.queryByTestId('analysis-panel')).toBeNull();
    expect(editor.store.getState().analysisFocus).toBeNull();
  });

  it('gives way to the value stream, which turns it off', async () => {
    const { editor } = await withLayer();
    act(() => {
      editor.preferences.getState().set('showValueStream', true);
    });
    expect(screen.queryByTestId('analysis-panel')).toBeNull();
    expect(screen.getByTestId('stream-panel')).toBeInTheDocument();
  });
});

describe('the panel « Analysis » guided (plan 3.1)', () => {
  it('asks the team for the lead times while they are unknown, and says when there is nothing to act on', async () => {
    const { panel } = await withLayer();
    expect(within(panel).getByTestId('analysis-ask')).toHaveTextContent('Délai inconnu. À demander à l’équipe');
    expect(within(panel).getByTestId('analysis-places-none')).toHaveTextContent('Aucun relais, rupture, gaspillage ni irritant');
  });

  it('tells the lead time in three parts that add up to 100, the wait after a handoff in bold, the lead times entered', async () => {
    const lead = { work_hours: 3, waste_work_hours: 0.5, handoff_wait_hours: 15.4, other_wait_hours: 5.6 };
    const map = analysed();
    const totals = { ...map.value_stream.totals, expected_lead_hours: 24.5, steps_with_content: 3 };
    const { panel } = await withLayer({ ...map, value_stream: { ...map.value_stream, totals, analysis: { lead, leads_entered: 1, places: [] } } });
    expect(plain(within(panel).getByTestId('analysis-lead').textContent)).toBe('Délai3,5 jTravail3,5 h');
    expect([...within(panel).getByTestId('analysis-legend').querySelectorAll('li')].map((item) => plain(item.textContent))).toEqual([
      'Travail 14 %', 'Attente après un relais 63 %', 'Autre attente 23 %',
    ]);
    const say = within(panel).getByTestId('analysis-say');
    expect(plain(say.textContent)).toBe('Il attend 3 j sur 3,5 j, dont 2,2 j juste après un relais.');
    expect(plain(say.querySelector('strong')?.textContent)).toBe('2,2 j juste après un relais');
    expect(within(panel).getByTestId('analysis-entered')).toHaveTextContent('Délais saisis : 1 sur 3. Les autres étapes ne comptent que leur durée.');
  });

  it('keeps the lead time and says why its parts are not known yet', async () => {
    const map = analysed();
    const { panel } = await withLayer({ ...map, value_stream: { ...map.value_stream, totals: { ...map.value_stream.totals, expected_lead_hours: 14 } } });
    expect(plain(within(panel).getByTestId('analysis-lead').textContent)).toBe('Délai2 j');
    expect(within(panel).getByTestId('analysis-split-unknown')).toHaveTextContent('L’attente ne se répartit pas encore');
    expect(within(panel).queryByTestId('analysis-legend')).toBeNull();
  });

  it('lists the three places where a case loses the most, all of them on demand, each leading to its step', async () => {
    const map = analysed();
    const places = ['s3', 's2', 's4', 's1'].map((id, index) => ({
      step_id: id, lost_hours: 10 - index, visits: 1, lost_per_case: 10 - index, after_handoff: index === 0, handoffs: id === 's2' ? 2 : 0, media_breaks: 0, waste: false,
      usable_as_is: null, open_pain_points: 0,
    }));
    const { editor, panel } = await withLayer({ ...map, value_stream: { ...map.value_stream, analysis: { lead: null, leads_entered: 0, places } } });
    const shown = () => within(panel).getAllByRole('button').filter((button) => button.dataset.testid?.startsWith('analysis-place-') === true).map((button) => button.dataset.testid);
    expect(shown()).toEqual(['analysis-place-VIS-E003', 'analysis-place-VIS-E002', 'analysis-place-VIS-E004']);
    expect(within(panel).getByTestId('analysis-place-VIS-E002')).toHaveTextContent('Relais dans son détail : 2');
    fireEvent.click(within(panel).getByTestId('analysis-places-more'));
    expect(shown()).toHaveLength(4);
    expect(within(panel).getByTestId('analysis-places-more')).toHaveTextContent('Réduire');
    const reveal = vi.spyOn(editor.canvas, 'reveal');
    fireEvent.click(within(panel).getByTestId('analysis-place-VIS-E003'));
    expect(editor.store.getState().selection.steps).toEqual(['s3']);
    expect(reveal).toHaveBeenLastCalledWith(['s3']);
  });

  it('tells the time lost by case, the measure that ranks the list, and by passage only where the cases are unknown (F-106)', async () => {
    const map = analysed();
    const place = (id: string, lost: number, perCase: number | null) => ({
      step_id: id, lost_hours: lost, visits: perCase === null ? null : perCase / lost, lost_per_case: perCase, after_handoff: false, handoffs: 0,
      media_breaks: 0, waste: true, usable_as_is: null, open_pain_points: 0,
    });
    const shownWith = async (places: ReturnType<typeof place>[]) => {
      const { panel } = await withLayer({ ...map, value_stream: { ...map.value_stream, analysis: { lead: null, leads_entered: 0, places } } });
      return panel;
    };
    const byCase = await shownWith([place('s3', 6, 1.8), place('s2', 2, null)]);
    expect(within(byCase).getByTestId('analysis-places-measure')).toHaveTextContent('temps perdu par dossier');
    expect(plain(within(byCase).getByTestId('analysis-place-VIS-E003').textContent)).toContain('1,8 h');
    expect(plain(within(byCase).getByTestId('analysis-place-VIS-E002').textContent)).toContain('2 h par passage');
  });

  it('says the time lost by passage while no case is known', async () => {
    const map = analysed();
    const places = [{ step_id: 's3', lost_hours: 6, visits: null, lost_per_case: null, after_handoff: false, handoffs: 0, media_breaks: 0, waste: true,
      usable_as_is: null, open_pain_points: 0 }];
    const { panel } = await withLayer({ ...map, value_stream: { ...map.value_stream, analysis: { lead: null, leads_entered: 0, places } } });
    expect(within(panel).getByTestId('analysis-places-measure')).toHaveTextContent('temps perdu par passage');
    expect(plain(within(panel).getByTestId('analysis-place-VIS-E003').textContent)).not.toContain('par passage');
  });
});
