// inputs.test.tsx
// The inputs of a sheet: the outputs of the steps before offered unticked, a tick that uses them, those from elsewhere in their colour, « Autre… », the inputs from outside (annex B 4).

import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo } from '../../../test/fakeApi.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { documentOf, inputOf, producerOf, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

const near = inputOf(documentOf('d1', { name: 'Bon de commande' }));
const used = inputOf(documentOf('d2', { name: 'Devis' }), { used: true, use: documentUseOf('d2', 's2', { version: 4 }) });
const other = inputOf(documentOf('d3', { name: 'Liste des clients', step_id: 's3' }), { producer: producerOf('s3', 'VTE-E003', 'Classer'), near: false, used: true, use: documentUseOf('d3', 's2') });
const afterTick = { json: sheetViewOf({ inputs: [{ ...near, used: true, use: documentUseOf('d1', 's2') }] }) };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the outputs of the steps before (B-4-62, B-4-63, B-4-65)', () => {
  it('offered unticked with the code of their producer; a tick uses the document with an empty use, then the map is read again', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ inputs: [near] }), { 'PUT /api/steps/s2/uses/d1': afterTick });
    const tick = screen.getByRole('checkbox', { name: 'Utiliser « Bon de commande »' });
    expect([tick, screen.getByTestId('input-d1').querySelector('.from')?.textContent]).toEqual([expect.objectContaining({ checked: false }), 'VTE-E001']);
    fireEvent.click(tick);
    await vi.waitFor(() => {
      expect(record.events).toEqual(['map']);
    });
    const call = callsTo(sent, 'PUT', '/api/steps/s2/uses/d1')[0];
    expect([call?.body, call?.headers.get('If-Match')]).toEqual([{ wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null }, '"0"']);
    expect(screen.getByRole('checkbox', { name: 'Utiliser « Bon de commande »' })).toBeChecked();
    expect(screen.getByTestId('use-d1')).toBeInTheDocument();
  });

  it('an untick stops using it with the version of its use', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [used] }), { 'DELETE /api/steps/s2/uses/d2': { json: sheetViewOf({ inputs: [{ ...used, used: false, use: null }] }) } });
    fireEvent.click(screen.getByRole('checkbox', { name: 'Utiliser « Devis »' }));
    await vi.waitFor(() => {
      expect(screen.getByRole('checkbox', { name: 'Utiliser « Devis »' })).not.toBeChecked();
    });
    expect(callsTo(sent, 'DELETE', '/api/steps/s2/uses/d2').map((call) => call.headers.get('If-Match'))).toEqual(['"4"']);
  });

  it('a refusal is said, the box stays as the host holds it, the map is not read', async () => {
    const { record } = await renderSheet(sheetViewOf({ inputs: [near] }), { 'PUT /api/steps/s2/uses/d1': { refused: 403, code: 'DROITS_INSUFFISANTS', message: 'Refusé.' } });
    fireEvent.click(screen.getByRole('checkbox', { name: 'Utiliser « Bon de commande »' }));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Refusé.', 'error']]);
    });
    expect([record.events, screen.getByRole<HTMLInputElement>('checkbox', { name: 'Utiliser « Bon de commande »' }).checked]).toEqual([[], false]);
  });

  it('those from elsewhere in their own colour, said « d’ailleurs »; the near ones first', async () => {
    await renderSheet(sheetViewOf({ inputs: [other, near] }));
    expect(within(screen.getByTestId('sheet-inputs')).getAllByRole('listitem').map((item) => item.dataset.testid)).toEqual(['input-d1', 'input-d3']);
    expect([screen.getByTestId('input-d3').classList.contains('entry--other'), screen.getByTestId('input-d3').querySelector('.from')?.textContent]).toEqual([true, 'VTE-E003 · d’ailleurs']);
  });

  it('none yet: the outputs of the steps linked by a flow will show here', async () => {
    await renderSheet(sheetViewOf());
    expect(screen.getByTestId('sheet-inputs')).toHaveTextContent('Les sorties des étapes reliées par une flèche apparaîtront ici, à cocher.');
  });
});

describe('« Autre… » (B-4-64)', () => {
  const available = [
    { producer: producerOf('s3', 'VTE-E003', 'Classer'), documents: [{ document: documentOf('d3', { name: 'Liste des clients' }), used: false }, { document: documentOf('d4', { name: 'Tarifs' }), used: true }] },
    { producer: producerOf('s4', 'VTE-E004', 'Facturer'), documents: [] },
  ];

  it('a dialog of the outputs of the other steps by step; a tick uses the document with an empty use and stays ticked', async () => {
    const { sent } = await renderSheet(sheetViewOf(), {
      'GET /api/steps/s2/documents/available': { json: available }, 'PUT /api/steps/s2/uses/d3': { json: sheetViewOf({ inputs: [other] }) },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Autre…' }));
    const dialog = await screen.findByRole('dialog', { name: 'Documents produits par les autres étapes' });
    const rows = await within(dialog).findAllByRole('row');
    expect(rows.map((row) => [...row.querySelectorAll('th, td')].map((cell) => cell.textContent))).toEqual([
      ['Étape', 'Document', 'Utilisé'], ['VTE-E003 · Classer', 'Liste des clients', ''], ['Tarifs', ''],
    ]);
    expect(['Liste des clients', 'Tarifs'].map((name) => within(dialog).getByRole<HTMLInputElement>('checkbox', { name: `Utiliser « ${name} »` }).checked)).toEqual([false, true]);
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Utiliser « Liste des clients »' }));
    await vi.waitFor(() => {
      expect(within(dialog).getByRole('checkbox', { name: 'Utiliser « Liste des clients »' })).toBeChecked();
    });
    expect(callsTo(sent, 'PUT', '/api/steps/s2/uses/d3').map((call) => [call.body, call.headers.get('If-Match')]))
      .toEqual([[{ wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null }, '"0"']]);
    fireEvent.click(within(dialog).getByTestId('other-documents-done'));
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('Escape closes the dialog and leaves the sheet open: the release of the key is no second Escape', async () => {
    const { record } = await renderSheet(sheetViewOf(), { 'GET /api/steps/s2/documents/available': { json: [] } });
    await userEvent.click(screen.getByRole('button', { name: 'Autre…' }));
    await screen.findByTestId('other-documents-empty');
    await userEvent.keyboard('{Escape}');
    expect([screen.queryByRole('dialog'), record.events]).toEqual([null, []]);
  });

  it('says when no other step declared an output, a step without output counting for none', async () => {
    await renderSheet(sheetViewOf(), { 'GET /api/steps/s2/documents/available': { json: [{ producer: producerOf('s4', 'VTE-E004', 'Facturer'), documents: [] }] } });
    fireEvent.click(screen.getByRole('button', { name: 'Autre…' }));
    expect(await screen.findByTestId('other-documents-empty')).toHaveTextContent('Aucune autre étape de la carte n’a déclaré de sortie.');
  });
});
