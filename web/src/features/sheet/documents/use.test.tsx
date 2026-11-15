// use.test.tsx
// The use of a ticked input: the wait, the share usable as is, the pain point always offered (F-118), raised on the producer, and its time lost; sent on leaving a field, only when something changed, its fields open while the host answers and its writes one after the other (annex B 4, audit 3.1 V3).

import { act, fireEvent, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo, heldReply } from '../../../test/fakeApi.ts';
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
    expect(sent.filter((call) => call.method === 'PUT')).toEqual([]);
    expect(screen.getByTestId('input-d1').querySelector('.entry-details')?.textContent).toBe('attente 4 h · déclaré par Camille Bernard');
  });

  it('the pain point of 2000 characters raised on the producer, then its time lost; sent on leaving the field or on Enter', async () => {
    const answered = sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 70, pain_point_id: 'p1' })], pain_points: [painPointOf('p1', { description: 'Sans numéro' })] });
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [ticked()] }), { 'PUT /api/steps/s2/uses/d1': { json: answered } });
    fireEvent.change(screen.getByLabelText('Part de « Devis » utilisable telle quelle, en %'), { target: { value: '70' } });
    const pain = within(screen.getByTestId('use-pain-d1'));
    const text = pain.getByLabelText('Irritant sur « Devis »');
    expect([text.getAttribute('maxlength'), pain.queryByLabelText('Temps perdu à cause de « Devis », en heures par mois')]).toEqual(['2000', null]);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('il s’affiche sur l’étape qui produit le document (VIS-E001).');
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
      .toEqual([[{ wait_hours: null, percent_complete_accurate: 70, pain_point: 'Sans numéro', lost_hours_per_month: 2, action: null, owner: null, due_date: null }, '"3"']]);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('Irritant VIS-I001');
  });

  it('a pain point that exists is shown at 100 % too, under its code, with what the host holds', async () => {
    const pain = painPointOf('p1', { code: 'VIS-I007', description: 'Format illisible', lost_hours_per_month: 5 });
    await renderSheet(sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 100, pain_point_id: 'p1' })], pain_points: [pain] }));
    const box = within(screen.getByTestId('use-pain-d1'));
    expect([box.getByLabelText<HTMLTextAreaElement>('Irritant sur « Devis »').value, box.getByLabelText<HTMLInputElement>('Temps perdu à cause de « Devis », en heures par mois').value])
      .toEqual(['Format illisible', '5']);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('Irritant VIS-I007');
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

  it('Escape in a field of the use closes the sheet and sends what was typed in it (audit 3.2)', async () => {
    const answered = sheetViewOf({ inputs: [ticked({ wait_hours: 5.5 })] });
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [ticked()] }), { 'PUT /api/steps/s2/uses/d1': { json: answered } });
    const wait = screen.getByLabelText('Attente avant réception de « Devis », en heures');
    wait.focus();
    fireEvent.change(wait, { target: { value: '5.5' } });
    fireEvent.keyDown(wait, { key: 'Escape' });
    expect(record.events).toContain('close');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s2/uses/d1').map((call) => call.body))
        .toEqual([{ wait_hours: 5.5, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null, action: null, owner: null, due_date: null }]);
    });
  });
});

describe('the pain point noted where it is suffered (F-118)', () => {
  it('the pain point is always offered under a ticked input, at 100 % as without a share; its time lost and its plan wait for a text (F-118)', async () => {
    await renderSheet(sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 100 })] }));
    expect(screen.getByTestId('use-pain-d1')).toBeVisible();
    const full = within(screen.getByTestId('use-pain-d1'));
    expect([full.getByLabelText<HTMLTextAreaElement>('Irritant sur « Devis »').value, full.queryByLabelText('Temps perdu à cause de « Devis », en heures par mois'),
      full.queryByTestId('use-action-plan')]).toEqual(['', null, null]);
    // The notion first: a pain point is noted where it is suffered.
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('Notez-le ici, là où vous le subissez : il s’affiche sur l’étape qui produit le document (VIS-E001).');
    expect(screen.getByTestId('sheet-section-inputs')).toHaveTextContent('Un irritant se note ici, sous le document qui pose problème.');
  });

  it('without a share either, the pain point is there', async () => {
    await renderSheet(sheetViewOf({ inputs: [ticked()] }));
    expect(within(screen.getByTestId('use-pain-d1')).getByLabelText('Irritant sur « Devis »')).toBeVisible();
  });

  it('asked from the outputs of its producer, the pain point of the document takes the caret, once; another document leaves it alone (F-118)', async () => {
    const focused = vi.fn();
    const other = inputOf(documentOf('d2', { name: 'Bordereau' }), { used: true, use: documentUseOf('d2', 's2', { version: 1 }) });
    await renderSheet(sheetViewOf({ inputs: [ticked(), other] }), {}, { painFocus: 'd2', onPainFocused: focused });
    await vi.waitFor(() => { expect(document.activeElement).toBe(screen.getByLabelText('Irritant sur « Bordereau »')); });
    expect(focused).toHaveBeenCalledTimes(1);
  });

  it('nothing takes the caret while nothing is asked', async () => {
    const focused = vi.fn();
    await renderSheet(sheetViewOf({ inputs: [ticked()] }), {}, { onPainFocused: focused });
    await act(() => new Promise((resolve) => setTimeout(resolve, 20)));
    expect([document.activeElement === screen.getByLabelText('Irritant sur « Devis »'), focused.mock.calls.length]).toEqual([false, 0]);
  });

  it('a caret asked that no field of the sheet can take is forgotten once the sheet is drawn: no use of the document, a sheet in reading (R4-10)', async () => {
    const focused = vi.fn();
    const filled = await renderSheet(sheetViewOf({ inputs: [ticked()] }), {}, { painFocus: 'unknown', onPainFocused: focused });
    await vi.waitFor(() => { expect(focused).toHaveBeenCalledTimes(1); });
    expect(document.activeElement === screen.getByLabelText('Irritant sur « Devis »')).toBe(false);
    filled.unmount();
    const reader = { fill: false, validate: false, request_clarification: false, assign: false };
    await renderSheet(sheetViewOf({ is_draft: false, permissions: reader, inputs: [ticked()] }), {}, { painFocus: 'd1', onPainFocused: focused });
    await vi.waitFor(() => { expect(focused).toHaveBeenCalledTimes(2); });
    expect(screen.queryByLabelText('Irritant sur « Devis »')).toBeNull();
  });
});

describe('a use written while the person types on (audit 3.1 V3, U1)', () => {
  const LABELS = [
    'Attente avant réception de « Devis », en heures', 'Part de « Devis » utilisable telle quelle, en %', 'Irritant sur « Devis »',
    'Temps perdu à cause de « Devis », en heures par mois', 'Action décidée sur « Devis »', 'Responsable de l’action sur « Devis »', 'Échéance de l’action sur « Devis »',
  ];
  const field = (label: string) => screen.getByLabelText<HTMLInputElement>(label);
  /** The use as the host gives it back: its version, its share of 70 %, its pain point p1 and what is said of it. */
  const answer = (version: number, pain: Partial<Schema<'PainPoint'>> = {}) => ({
    json: sheetViewOf({ inputs: [ticked({ version, percent_complete_accurate: 70, pain_point_id: 'p1' })], pain_points: [painPointOf('p1', { description: 'Sans numéro', ...pain })] }),
  });
  const body = (overrides: Partial<Schema<'DocumentUseBody'>> = {}) => ({
    wait_hours: null, percent_complete_accurate: 70, pain_point: 'Sans numéro', lost_hours_per_month: null, action: null, owner: null, due_date: null, ...overrides,
  });

  it('the fields stay open while the use leaves, what is typed meanwhile is kept, the writes leave one after the other with the version given back', async () => {
    const writes = [heldReply(), heldReply(), heldReply()];
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 70 })] }), { 'PUT /api/steps/s2/uses/d1': writes.map((write) => write.reply) });
    const puts = () => callsTo(sent, 'PUT', '/api/steps/s2/uses/d1');
    fireEvent.change(field('Irritant sur « Devis »'), { target: { value: ' Sans numéro ' } });
    fireEvent.blur(field('Irritant sur « Devis »'));
    await vi.waitFor(() => {
      expect(puts()).toHaveLength(1);
    });
    // The host has not answered: the seven fields take the focus and the typing; the tick waits for the host.
    expect(LABELS.map((label) => field(label).disabled)).toEqual(LABELS.map(() => false));
    expect(screen.getByTestId('input-tick-d1')).toBeDisabled();

    // Typed during the wait, its fields not left yet: kept when the host answers; the field sent takes the text of the host.
    fireEvent.change(field('Temps perdu à cause de « Devis », en heures par mois'), { target: { value: '2' } });
    fireEvent.change(field('Action décidée sur « Devis »'), { target: { value: 'Relire le devis' } });
    writes[0]?.give(answer(4));
    await vi.waitFor(() => {
      expect(field('Irritant sur « Devis »').value).toBe('Sans numéro');
    });
    expect([record.events, field('Temps perdu à cause de « Devis », en heures par mois').value, field('Action décidée sur « Devis »').value])
      .toEqual([['map'], '2', 'Relire le devis']);
    expect(screen.getByTestId('input-tick-d1')).not.toBeDisabled();

    // Two writes in a row: the second leaves once the first is answered, with the version it gave back.
    fireEvent.blur(field('Action décidée sur « Devis »'));
    await vi.waitFor(() => {
      expect(puts()).toHaveLength(2);
    });
    fireEvent.change(field('Responsable de l’action sur « Devis »'), { target: { value: 'Paie' } });
    fireEvent.blur(field('Responsable de l’action sur « Devis »'));
    // A field left unchanged sends nothing more: the use that waits already holds it (B-4-75).
    fireEvent.blur(field('Échéance de l’action sur « Devis »'));
    await act(() => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(puts()).toHaveLength(2);
    writes[1]?.give(answer(5, { lost_hours_per_month: 2, action: 'Relire le devis' }));
    await vi.waitFor(() => {
      expect(puts()).toHaveLength(3);
    });
    // The second write answered, the third on its way: what the third carries stays shown.
    await act(() => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(field('Responsable de l’action sur « Devis »').value).toBe('Paie');
    writes[2]?.give(answer(6, { lost_hours_per_month: 2, action: 'Relire le devis', owner: 'Paie' }));
    await vi.waitFor(() => {
      expect(record.events).toEqual(['map', 'map', 'map']);
    });
    expect(puts().map((call) => [call.headers.get('If-Match'), call.body])).toEqual([
      ['"3"', body()], ['"4"', body({ lost_hours_per_month: 2, action: 'Relire le devis' })], ['"5"', body({ lost_hours_per_month: 2, action: 'Relire le devis', owner: 'Paie' })],
    ]);
    expect([field('Action décidée sur « Devis »').value, field('Responsable de l’action sur « Devis »').value, record.notes]).toEqual(['Relire le devis', 'Paie', []]);
  });
});

describe('a use refused (audit 3.1 V3, U1)', () => {
  it('keeps what was typed when the host later gives the use changed by someone else; the fields left alone take it', async () => {
    const other = inputOf(documentOf('d2', { name: 'Bon de commande' }), { used: true, use: documentUseOf('d2', 's2', { version: 1, percent_complete_accurate: 100 }) });
    const changed = sheetViewOf({ inputs: [ticked({ version: 5, wait_hours: 8, percent_complete_accurate: 70 }), { ...other, use: documentUseOf('d2', 's2', { version: 2, wait_hours: 3 }) }] });
    const { record } = await renderSheet(sheetViewOf({ inputs: [ticked({ percent_complete_accurate: 70 }), other] }), {
      'PUT /api/steps/s2/uses/d1': { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifié par quelqu’un d’autre.' },
      'PUT /api/steps/s2/uses/d2': { json: changed },
    });
    const pain = screen.getByLabelText<HTMLTextAreaElement>('Irritant sur « Devis »');
    fireEvent.change(pain, { target: { value: 'Sans numéro' } });
    fireEvent.blur(pain);
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Modifié par quelqu’un d’autre.', 'error']]);
    });
    const wait = screen.getByLabelText('Attente avant réception de « Bon de commande », en heures');
    fireEvent.change(wait, { target: { value: '3' } });
    fireEvent.blur(wait);
    await vi.waitFor(() => {
      expect(screen.getByLabelText('Attente avant réception de « Devis », en heures')).toHaveValue(8);
    });
    expect([screen.getByLabelText<HTMLTextAreaElement>('Irritant sur « Devis »').value, record.events]).toEqual(['Sans numéro', ['map']]);
  });
});
