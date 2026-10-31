// Canvas.test.tsx
// The canvas drawn: steps with codes, labels, shapes, pictograms, gauge, warnings, handles; lane bodies and headers (annex B 2.10, 2.12, 3.2, 3.3).

import { screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showMap as show } from '../../../test/editor.tsx';
import { laneOf, mapOf, sampleMap, sheetOf, stepOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the codes (B-2.10-01 to B-2.10-04, B-2.12-07)', () => {
  it('show the code above the label of each step and in the header of each lane, by default', async () => {
    await show(sampleMap());
    expect(within(screen.getByTestId('step-VTE-E002')).getByTestId('step-code')).toHaveTextContent('VTE-E002');
    expect(within(screen.getByTestId('lane-head-VTE-C01')).getByTestId('lane-head-code')).toHaveTextContent('VTE-C01');
  });

  it('are hidden by the preference, and the tooltip of the step still shows « code · path »', async () => {
    await show(sampleMap(), memoryStorage({ 'app.showCodes': '0' }));
    expect(screen.queryAllByTestId('step-code')).toHaveLength(0);
    expect(screen.queryAllByTestId('lane-head-code')).toHaveLength(0);
    expect(screen.getByTestId('step-VTE-E002')).toHaveAttribute('title', 'VTE-E002 · Ventes › Étape s2');
  });
});

describe('the steps (B-3.2-03 to B-3.2-19)', () => {
  it('carry the class of their type; the gateways draw a diamond and a marker, the others neither', async () => {
    const types: Schema<'StepType'>[] = ['task', 'wait', 'start', 'end', 'decision', 'parallel', 'inclusive'];
    await show(mapOf({ lanes: [laneOf('l1', 0)], steps: types.map((type, index) => stepOf(`s${String(index)}`, 'l1', index, { type })) }));
    for (const [index, type] of types.entries()) {
      const step = screen.getByTestId(`step-VTE-E00${String(index + 1)}`);
      expect(step).toHaveClass('step', `step--${type}`);
      const gateway = ['decision', 'parallel', 'inclusive'].includes(type);
      expect([step.classList.contains('step--gateway'), step.querySelector('.gateway-shape polygon') !== null, step.querySelector('.gateway-marker') !== null]).toEqual([gateway, gateway, gateway]);
    }
  });

  it('take the ink and the body colour of their lane', async () => {
    await show(sampleMap());
    expect(screen.getByTestId('step-VTE-E004').style.getPropertyValue('--ink')).toBe('var(--lane-2-ink)');
    expect(screen.getByTestId('step-VTE-E004').style.getPropertyValue('--lane-bg')).toBe('var(--lane-even)');
  });

  it('show their label in the language of the interface (B-3.2-19)', async () => {
    await i18next.changeLanguage('en');
    await show(sampleMap());
    expect(within(screen.getByTestId('step-VTE-E002')).getByTestId('step-label')).toHaveTextContent('Check the request');
    expect(within(screen.getByTestId('step-VTE-E001')).getByTestId('step-label')).toHaveTextContent('Demande reçue');
  });

  it('a detailed step shows its stack, its badge and the frequency of its sheet (spec 9.4)', async () => {
    const map = sampleMap();
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: sheetOf({ frequency: 'weekly' }) } : step));
    await show({ ...map, steps });
    const detailed = screen.getByTestId('step-VTE-E002');
    expect(detailed).toHaveClass('is-macro');
    expect(detailed.querySelector('.step-stack')).not.toBeNull();
    expect(within(detailed).getByTestId('step-detailed')).toHaveTextContent('détaillée');
    expect(within(detailed).getByTestId('step-frequency')).toHaveTextContent('Hebdomadaire');
    expect(screen.getByTestId('step-VTE-E001').querySelector('.step-stack')).toBeNull();
  });
});

describe('the handles (B-3.3-01)', () => {
  it('four handles a step, left, right, top, bottom, all sources', async () => {
    const canvas = await show(sampleMap());
    const handles = [...canvas.querySelectorAll('[data-id="s2"] .react-flow__handle')];
    expect(handles.map((handle) => handle.getAttribute('data-handleid'))).toEqual(['left', 'right', 'top', 'bottom']);
    expect(handles.every((handle) => handle.classList.contains('source') && handle.classList.contains('step-handle'))).toBe(true);
  });
});

describe('the pictograms (B-2.10-05 to B-2.10-15)', () => {
  const marked = (markers: Schema<'StepMarkersView'>, minutes: number | null = null) => {
    const map = sampleMap();
    return { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, markers, sheet: sheetOf({ duration_minutes: minutes }) } : step)) };
  };

  it('show the documents and their count, the names on hover with « produit » or « reçu »', async () => {
    await show(marked({ documents: [{ document_id: 'd1', name: 'Devis', direction: 'output' }, { document_id: 'd2', name: 'Bon', direction: 'input' }], pain_points: [] }));
    const documents = within(screen.getByTestId('step-VTE-E002')).getByTestId('picto-documents');
    expect(documents).toHaveTextContent('2');
    expect(documents).toHaveAttribute('title', 'Documents : Devis (produit), Bon (reçu)');
  });

  it('show the open pain points and the duration of the sheet', async () => {
    await show(marked({ documents: [], pain_points: [{ pain_point_id: 'p1', code: 'VTE-I0001', description: 'Saisie double' }] }, 90));
    const step = within(screen.getByTestId('step-VTE-E002'));
    expect(step.getByTestId('picto-pain-points')).toHaveTextContent('1');
    expect(step.getByTestId('picto-pain-points')).toHaveClass('picto-irritant');
    expect(step.getByTestId('picto-pain-points')).toHaveAttribute('title', 'Irritants ouverts : VTE-I0001 Saisie double');
    expect(step.getByTestId('picto-duration')).toHaveTextContent('1 h 30');
    expect(step.queryByTestId('picto-documents')).toBeNull();
  });

  it('are absent when all are at zero; a step with pictograms is marked so that its label moves up', async () => {
    await show(marked({ documents: [], pain_points: [] }, 45));
    expect(screen.getByTestId('step-VTE-E002')).toHaveClass('has-pictos');
    expect(screen.getByTestId('step-VTE-E001')).not.toHaveClass('has-pictos');
    expect(within(screen.getByTestId('step-VTE-E001')).queryByTestId('step-pictos')).toBeNull();
  });

  it('show always by default, only on hover with the preference « au survol »', async () => {
    const canvas = await show(sampleMap());
    expect(canvas).not.toHaveClass('pictos-on-hover');
  });

  it('only on hover when the preference says so', async () => {
    const canvas = await show(sampleMap(), memoryStorage({ 'app.alwaysShowPictos': '0' }));
    expect(canvas).toHaveClass('pictos-on-hover');
  });
});
