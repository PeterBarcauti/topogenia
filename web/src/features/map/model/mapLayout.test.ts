// mapLayout.test.ts
// The map laid out for the canvas: lane bands, steps and their labels by language, flows routed with their label boxes, the frame of a dissolution, the RACI (annex B 2.9 to 2.12, 3).

import { flowOf, frameOf, laneOf, mapOf, sampleMap, sheetOf, stepOf } from '../../../test/maps.ts';
import { layoutMap, routingStepOf } from './mapLayout.ts';
import { edgeLabelSize, gatewayLabelSize, type TextMeasure } from './textSize.ts';

const six: TextMeasure = (text) => text.length * 6;

describe('the lanes (B-2.12-05, B-2.12-08, B-2.12-10, spec 9.3)', () => {
  it('are bands in their order, alternating odd and even bodies, with the tint of their colour', () => {
    const layout = layoutMap(sampleMap({ lanes: [laneOf('l2', 1), laneOf('l1', 0)] }), 'fr', six);
    expect(layout.lanes.map((band) => [band.lane.id, band.parity, band.tint, band.top, band.height])).toEqual([
      ['l1', 'odd', 1, 0, 120],
      ['l2', 'even', 2, 120, 120],
    ]);
  });

  it('take their name in the language shown, else the name entered', () => {
    const layout = layoutMap(sampleMap(), 'en', six);
    expect(layout.lanes.map((band) => band.name)).toEqual(['Sales', 'Comptabilité']);
    expect(layoutMap(sampleMap(), 'fr', six).lanes[0]?.name).toBe('Commercial');
  });

  it('are as high as their rows: a step on the row 1 makes a lane of two rows, the next one goes down as much', () => {
    const map = sampleMap();
    const layout = layoutMap({ ...map, steps: [...map.steps, stepOf('s5', 'l1', 4, { row: 1 })] }, 'fr', six);
    expect(layout.lanes.map((band) => [band.rows, band.top, band.height])).toEqual([[2, 0, 240], [1, 240, 120]]);
  });
});

describe('the steps (B-3.1-15, B-3.1-16, B-3.2-19)', () => {
  it('stand in the box of their cell, with their label in the language shown', () => {
    const layout = layoutMap(sampleMap(), 'en', six);
    expect(layout.steps.map((figure) => [figure.step.id, figure.box.x, figure.box.y, figure.label])).toEqual([
      ['s1', 30, 28, 'Demande reçue'],
      ['s2', 250, 28, 'Check the request'],
      ['s3', 470, 28, 'Conforme ?'],
      ['s4', 690, 148, 'Facture émise'],
    ]);
  });

  it('carry the tint and the body of their lane, their warnings, their gauge and whether they are detailed', () => {
    const map = sampleMap({ warnings: [{ step_id: 's4', code: 'FIN_LIEN_SORTANT' }, { step_id: 's4', code: 'ETAPE_ISOLEE' }] });
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: sheetOf({ status: 'draft', completeness: 40 }) } : step));
    const layout = layoutMap({ ...map, steps }, 'fr', six);
    const [s1, s2, , s4] = layout.steps;
    expect([s1?.tint, s1?.parity, s4?.tint, s4?.parity]).toEqual([1, 'odd', 2, 'even']);
    expect(s4?.warnings).toEqual(['FIN_LIEN_SORTANT', 'ETAPE_ISOLEE']);
    expect([s2?.gauge, s2?.macro, s1?.gauge, s1?.macro]).toEqual([{ status: 'draft', width: 40 }, true, null, false]);
  });

  it('leave out a step whose lane is not on the map', () => {
    const map = sampleMap();
    expect(layoutMap({ ...map, steps: [...map.steps, stepOf('lost', 'nowhere', 0)] }, 'fr', six).steps).toHaveLength(4);
  });

  it('put what hangs under a gateway or a task that leaves by its bottom point right of the axis (AR-58, AR-128), centred elsewhere', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [stepOf('g', 'l1', 0, { type: 'decision', label: 'Urgent ?' }), stepOf('a', 'l1', 1), stepOf('b', 'l2', 0), stepOf('c', 'l2', 1)],
      flows: [flowOf('f1', 'g', 'a'), flowOf('f2', 'g', 'b'), flowOf('f3', 'a', 'c')],
    });
    const layout = layoutMap(map, 'fr', six);
    expect(['f2', 'f3'].map((id) => layout.flows.find((flow) => flow.route.id === id)?.route.sourceSide)).toEqual(['bottom', 'bottom']);
    expect(layout.steps.map((figure) => [figure.step.id, figure.under])).toEqual([['g', 'right'], ['a', 'right'], ['b', 'centre'], ['c', 'centre']]);
  });

  it('give the routing the size of the label written under a gateway, and nothing for another step (AR-57)', () => {
    const gateway = stepOf('g', 'l1', 0, { type: 'inclusive' });
    expect(routingStepOf(gateway, 'Quels canaux ?', six)).toEqual({ id: 'g', laneId: 'l1', column: 0, row: 0, type: 'inclusive', below: gatewayLabelSize('Quels canaux ?', six) });
    expect(routingStepOf(gateway, '', six)).not.toHaveProperty('below');
    expect(routingStepOf(stepOf('t', 'l1', 1), 'Saisir', six)).not.toHaveProperty('below');
  });
});

describe('the flows (B-3.4-35, B-3.4-37, B-3.4-38)', () => {
  it('are routed, each with the label box measured from its text and its documents', () => {
    const layout = layoutMap(sampleMap(), 'fr', six);
    const byId = new Map(layout.flows.map((flow) => [flow.route.id, flow]));
    expect([...byId.keys()].sort()).toEqual(['f1', 'f2', 'f3', 'f4']);
    expect(byId.get('f1')?.route.label).toBeNull();
    const size = (id: string) => ({ width: byId.get(id)?.route.label?.width, height: byId.get(id)?.route.label?.height });
    expect(size('f2')).toEqual(edgeLabelSize('', 2, six));
    expect(size('f3')).toEqual(edgeLabelSize('oui', 0, six));
    expect(byId.get('f4')?.route.kind).toBe('loop');
    expect(byId.get('f2')?.documents).toEqual(['Bon de commande', 'Devis']);
  });

  it('take their label in the language of their source step: as entered there, translated elsewhere', () => {
    const text = (map: ReturnType<typeof sampleMap>, language: string) => layoutMap(map, language, six).flows.find((flow) => flow.route.id === 'f3')?.text;
    expect(text(sampleMap(), 'fr')).toBe('oui');
    expect(text(sampleMap(), 'en')).toBe('yes');
    const english = sampleMap();
    const steps = english.steps.map((step) => (step.id === 's3' ? { ...step, source_language: 'en' } : step));
    const flows = english.flows.map((flow) => (flow.id === 'f3' ? { ...flow, label: 'yes', translations: { fr: 'oui', en: 'oui (faux)' } } : flow));
    expect(text({ ...english, steps, flows }, 'en')).toBe('yes');
    expect(text({ ...english, steps, flows }, 'fr')).toBe('oui');
    expect(text(sampleMap(), 'de')).toBe('oui');
  });

  it('leave out a flow to a step that is not on the map', () => {
    const map = sampleMap();
    expect(layoutMap({ ...map, flows: [...map.flows, flowOf('f9', 's1', 'elsewhere')] }, 'fr', six).flows).toHaveLength(4);
  });
});

describe('the size of the map (B-3.1-19)', () => {
  it('is the width up to the free columns after the last step and the height of the lanes', () => {
    const layout = layoutMap(sampleMap(), 'fr', six);
    expect([layout.width, layout.height]).toEqual([30 + (3 + 1 + 3) * 220, 240]);
    expect([layoutMap(mapOf({ lanes: [laneOf('l1', 0)] }), 'fr', six).width]).toEqual([30 + 3 * 220]);
  });
});

describe('the frame of a dissolution (B-2.9-17 to B-2.9-21, B-3.2-17)', () => {
  it('stands round its steps with the size of its remainder; a step of another flow under it is outside', () => {
    const pain = { id: 'p', code: 'IRR-001', description: 'Ressaisie', status: 'reported' } as const;
    const map = sampleMap({ dissolution: frameOf(['s2', 's4'], { pain_points: [pain] }) });
    const layout = layoutMap(map, 'fr', six);
    expect(layout.frame).toEqual({ frame: map.dissolution, box: { x: 250 - 14, y: 28 - 26, width: 690 + 170 + 14 - 236, height: 148 + 64 + 14 - 2 }, remainder: 1 });
    expect(layout.steps.map((figure) => [figure.step.id, figure.outside])).toEqual([['s1', false], ['s2', false], ['s3', true], ['s4', false]]);
  });

  it('is absent without a dissolution, and no step is outside', () => {
    const layout = layoutMap(sampleMap(), 'fr', six);
    expect([layout.frame, layout.steps.some((figure) => figure.outside)]).toEqual([null, false]);
  });
});

describe('the RACI of the steps (B-2.11-03, B-2.11-05, B-2.11-07)', () => {
  it('a step concerned carries its badges, with the names of the lanes in the language shown, and whether it misses its approver', () => {
    const map = sampleMap({ raci: [{ step_id: 's3', lane_id: 'l2', role: 'A' }, { step_id: 's5', lane_id: 'l1', role: 'I' }] });
    const layout = layoutMap({ ...map, steps: [...map.steps, stepOf('s5', 'l2', 4)] }, 'en', six);
    expect(layout.steps.map((figure) => figure.raci)).toEqual([
      null, { badges: [], missingApprover: true }, { badges: [{ role: 'A', laneId: 'l2', name: 'Comptabilité' }], missingApprover: false }, null,
      { badges: [{ role: 'I', laneId: 'l1', name: 'Sales' }], missingApprover: true },
    ]);
  });

  it('the badges start 3 px under a step, under the label of a gateway, under the frequency of a detailed step', () => {
    const map = sampleMap();
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: sheetOf({ frequency: 'weekly' }) } : step));
    const layout = layoutMap({ ...map, steps: [...steps, stepOf('s5', 'l2', 4, { sub_map_id: 'm5', sub_map_empty: false })] }, 'fr', six);
    const label = gatewayLabelSize('Conforme ?', six)?.height ?? 0;
    expect(layout.steps.map((figure) => figure.raciTop)).toEqual([3, 5 + 18 + 3, 3 + label + 3, 3, 3]);
  });
});
