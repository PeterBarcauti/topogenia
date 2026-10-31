// use.test.tsx
// The use of a ticked input: the wait, the share usable as is, the pain point under 100 % raised on the producer and its time lost; sent on leaving a field, only when something changed (annex B 4).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo } from '../../../test/fakeApi.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { documentOf, inputOf, painPointOf, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

const ticked = (overrides = {}) => inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2', { version: 3, ...overrides }) });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the use of a ticked input (B-4-71 to B-4-75)', () => {
  it('a wait in hours by 0.5, a share usable as is from 0 to 100 by 5; nothing sent when a field is left unchanged', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [ticked({ wait_hours: 4, percent_complete_accurate: 100 })] }));
    const wait = screen.getByLabelText<HTMLInputElement>('Attente avant réception de « Devis », en heures');
    const percent = screen.getByLabelText<HTMLInputElement>('Part de « Devis » utilisable telle quelle, en %');
    expect([wait.getAttribute('step'), wait.value, percent.getAttribute('min'), percent.getAttribute('max'), percent.getAttribute('step'), percent.value])
      .toEqual(['0.5', '4', '0', '100', '5', '100']);
    fireEvent.blur(wait);
    fireEvent.blur(percent);
    expect([sent.filter((call) => call.method === 'PUT'), screen.queryByTestId('use-pain-d1')]).toEqual([[], null]);
    expect(screen.getByTestId('input-d1').querySelector('.entry-details')?.textContent).toBe('attente 4 h · déclaré par Camille Bernard');
  });

  it('under 100 %, the pain point of 2000 characters raised on the producer, then its time lost; sent on leaving the field or on Enter', async () => {
    const answered = sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 70, pain_point_id: 'p1' })], pain_points: [painPointOf('p1', { description: 'Sans numéro' })] });
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [ticked()] }), { 'PUT /api/steps/s2/uses/d1': { json: answered } });
    fireEvent.change(screen.getByLabelText('Part de « Devis » utilisable telle quelle, en %'), { target: { value: '70' } });
    const pain = within(screen.getByTestId('use-pain-d1'));
    const text = pain.getByLabelText('Irritant sur « Devis »');
    expect([text.getAttribute('maxlength'), pain.queryByLabelText('Temps perdu à cause de « Devis », en heures par mois')]).toEqual(['2000', null]);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('L’irritant est signalé à l’étape qui le produit (VTE-E001).');
    fireEvent.change(text, { target: { value: ' Sans numéro ' } });
    const lost = pain.getByLabelText('Temps perdu à cause de « Devis », en heures par mois');
    expect([lost.getAttribute('min'), lost.getAttribute('max')]).toEqual(['0', '99999']);
    fireEvent.change(lost, { target: { value: '2' } });
    lost.focus();
    fireEvent.keyDown(lost, { key: 'Enter' });
    expect(document.activeElement).not.toBe(lost);
    await vi.waitFor(() => {
      expect(record.events).toEqual(['map']);
    });
    expect(callsTo(sent, 'PUT', '/api/steps/s2/uses/d1').map((call) => [call.body, call.headers.get('If-Match')]))
      .toEqual([[{ wait_hours: null, percent_complete_accurate: 70, pain_point: 'Sans numéro', lost_hours_per_month: 2 }, '"3"']]);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('Irritant VTE-I001');
  });

  it('a pain point that exists is shown at 100 % too, under its code, with what the host holds', async () => {
    const pain = painPointOf('p1', { code: 'VTE-I007', description: 'Format illisible', lost_hours_per_month: 5 });
    await renderSheet(sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 100, pain_point_id: 'p1' })], pain_points: [pain] }));
    const box = within(screen.getByTestId('use-pain-d1'));
    expect([box.getByLabelText<HTMLTextAreaElement>('Irritant sur « Devis »').value, box.getByLabelText<HTMLInputElement>('Temps perdu à cause de « Devis », en heures par mois').value])
      .toEqual(['Format illisible', '5']);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('Irritant VTE-I007');
  });

  it('the typing starts again from the use the host gives back', async () => {
    const answered = sheetViewOf({ inputs: [ticked({ wait_hours: 6 })] });
    await renderSheet(sheetViewOf({ inputs: [ticked()] }), { 'PUT /api/steps/s2/uses/d1': { json: answered } });
    const wait = screen.getByLabelText('Attente avant réception de « Devis », en heures');
    fireEvent.change(wait, { target: { value: '5.5' } });
    fireEvent.blur(wait);
    await vi.waitFor(() => {
      expect(screen.getByLabelText('Attente avant réception de « Devis », en heures')).toHaveValue(6);
    });
  });
});
