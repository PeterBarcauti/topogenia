// declared.test.tsx
// The documents the step declares: an input from outside and an output, their form, their link, their buttons; who uses an output and the pain points raised on it (annex B 4).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo } from '../../../test/fakeApi.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { documentOf, inputOf, painPointOf, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

const external = inputOf(documentOf('e1', { direction: 'input', step_id: 's2', name: 'Relevé bancaire', location: 'https://localhost/releve', location_type: 'url', wait_hours: 24, version: 2 }),
  { producer: null, near: false, used: true });
const pathOnly = inputOf(documentOf('e2', { direction: 'input', step_id: 's2', name: 'Tarifs', location: '\\\\srv\\tarifs', location_type: 'path', share_confirmed: true }),
  { producer: null, near: false, used: true });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('an input from outside (B-4-66 to B-4-70)', () => {
  it('its form: a name of 200, a link of 1000, a wait in hours by 0.5, the sharing named by the project; saved, then the map read again', async () => {
    const { sent, record } = await renderSheet(sheetViewOf(), { 'POST /api/steps/s2/documents': { json: sheetViewOf({ inputs: [external] }) } });
    fireEvent.click(screen.getByRole('button', { name: 'Entrée extérieure' }));
    const form = screen.getByRole('group', { name: 'Nouvelle entrée extérieure' });
    const [name, location, wait] = [within(form).getByLabelText('Nom du document'), within(form).getByLabelText('Lien vers le document'), within(form).getByLabelText('Attente avant réception, en heures')];
    expect([name.getAttribute('maxlength'), location.getAttribute('maxlength'), wait.getAttribute('min'), wait.getAttribute('max'), wait.getAttribute('step'), document.activeElement === name])
      .toEqual(['200', '1000', '0', '100000', '0.5', true]);
    expect(screen.getByRole('button', { name: 'Enregistrer le document' })).toBeDisabled();
    fireEvent.change(name, { target: { value: ' Relevé bancaire ' } });
    fireEvent.change(location, { target: { value: 'https://localhost/releve' } });
    fireEvent.change(wait, { target: { value: '24' } });
    fireEvent.click(within(form).getByRole('checkbox', { name: 'Partagé avec le groupe « Finance »' }));
    fireEvent.keyDown(name, { key: 'Enter' });
    await vi.waitFor(() => {
      expect(screen.queryByTestId('document-form-input')).toBeNull();
    });
    expect([callsTo(sent, 'POST', '/api/steps/s2/documents')[0]?.body, record.events]).toEqual([
      { direction: 'input', name: 'Relevé bancaire', location: 'https://localhost/releve', wait_hours: 24, share_confirmed: true }, ['map'],
    ]);
  });

  it('a link that opens in a new tab, a path said muted with the sharing and who declared it, its wait', async () => {
    await renderSheet(sheetViewOf({ inputs: [external, pathOnly] }));
    const link = within(screen.getByTestId('input-e1')).getByRole('link', { name: 'Relevé bancaire' });
    // A stable identifier, by the document, for the journeys (S-3.5-04).
    expect(link).toBe(screen.getByTestId('document-link-e1'));
    expect([link.getAttribute('href'), link.getAttribute('target'), link.getAttribute('rel')]).toEqual(['https://localhost/releve', '_blank', 'noopener noreferrer']);
    expect(screen.getByTestId('input-e1').querySelector('.entry-details')?.textContent).toBe('extérieur à la carte · attente 24 h · déclaré par Camille Bernard');
    expect(within(screen.getByTestId('input-e2')).queryByRole('link')).toBeNull();
    expect(screen.getByTestId('input-e2').querySelector('.entry-details')?.textContent).toBe('extérieur à la carte · \\\\srv\\tarifs · partagé · déclaré par Camille Bernard');
  });

  it('changed with its version, removed with it; the form says the default group when the project names none; Annuler leaves it', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [external] }), {
      'PUT /api/steps/s2/documents/e1': { json: sheetViewOf({ inputs: [external] }) }, 'DELETE /api/steps/s2/documents/e1': { json: sheetViewOf() },
    }, { project: { members: [], sharingGroupLabel: null } });
    fireEvent.click(screen.getByRole('button', { name: 'Modifier « Relevé bancaire »' }));
    const form = screen.getByRole('group', { name: 'Modifier « Relevé bancaire »' });
    expect([within(form).getByLabelText('Attente avant réception, en heures')].map((field) => (field as HTMLInputElement).value)).toEqual(['24']);
    expect(within(form).getByRole('checkbox')).toHaveAccessibleName('Partagé avec le groupe de partage du projet');
    fireEvent.click(within(form).getByRole('button', { name: 'Enregistrer le document' }));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s2/documents/e1').map((call) => call.headers.get('If-Match'))).toEqual(['"2"']);
    });
    fireEvent.click(await screen.findByRole('button', { name: 'Retirer « Relevé bancaire »' }));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/steps/s2/documents/e1').map((call) => call.headers.get('If-Match'))).toEqual(['"2"']);
    });
    fireEvent.click(screen.getByRole('button', { name: 'Entrée extérieure' }));
    fireEvent.click(screen.getByRole('button', { name: 'Annuler' }));
    expect(screen.queryByTestId('document-form-input')).toBeNull();
  });
});

describe('the outputs (B-4-78 to B-4-81)', () => {
  it('who uses each one and how well, or « pas encore utilisé »; the pain points of their consumers with their status and time lost', async () => {
    const pain = painPointOf('p1', { code: 'VTE-I001', description: 'Ressaisie à la main', status: 'under_review', lost_hours_per_month: 3 });
    const outputs = [
      { document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [documentUseOf('o1', 's3', { percent_complete_accurate: 90, pain_point_id: 'p1' }), documentUseOf('o1', 'far')] },
      { document: documentOf('o2', { step_id: 's2', name: 'Avoir' }), used_by: [] },
    ];
    await renderSheet(sheetViewOf({ outputs, pain_points: [pain] }));
    expect(screen.getByTestId('output-o1').querySelector('.entry-details')?.textContent)
      .toBe('utilisé par VTE-E003 (90 % utilisable tel quel), une étape d’une autre carte · déclaré par Camille Bernard');
    expect(screen.getByTestId('output-o2').querySelector('.entry-details')?.textContent).toBe('pas encore utilisé · déclaré par Camille Bernard');
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VTE-I001 · à l’étude : Ressaisie à la main · 3 h perdues par mois');
  });

  it('the form of an output has no wait; it is added as an output', async () => {
    const { sent } = await renderSheet(sheetViewOf(), { 'POST /api/steps/s2/documents': { json: sheetViewOf() } });
    expect(screen.getByTestId('sheet-outputs')).toHaveTextContent('Aucune sortie déclarée.');
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une sortie' }));
    const form = screen.getByRole('group', { name: 'Nouvelle sortie' });
    expect(within(form).queryByLabelText('Attente avant réception, en heures')).toBeNull();
    fireEvent.change(within(form).getByLabelText('Nom du document'), { target: { value: 'Facture' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Enregistrer le document' }));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s2/documents')[0]?.body).toEqual({ direction: 'output', name: 'Facture', location: null, wait_hours: null, share_confirmed: false });
    });
  });

  it('the seven statuses of a pain point', async () => {
    const statuses = ['reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped'] as const;
    const pains = statuses.map((status, index) => painPointOf(`p${String(index)}`, { status }));
    const outputs = [{ document: documentOf('o1', { step_id: 's2' }), used_by: pains.map((pain, index) => documentUseOf('o1', `c${String(index)}`, { pain_point_id: pain.id })) }];
    await renderSheet(sheetViewOf({ outputs, pain_points: pains }));
    expect(pains.map((pain) => screen.getByTestId(`pain-point-${pain.id}`).textContent.split(' · ')[1]?.split(' : ')[0]))
      .toEqual(['signalé', 'pris en compte', 'à l’étude', 'en cours', 'réalisé', 'adopté', 'abandonné']);
  });
});

describe('an output changed and removed (B-4-67, B-4-70, B-4-78)', () => {
  const output = {
    document: documentOf('o1', { step_id: 's2', name: 'Facture', location: 'https://localhost/facture', location_type: 'url', version: 3 }),
    used_by: [documentUseOf('o1', 's3')],
  };
  const changed = { ...output, document: { ...output.document, name: 'Facture v2', location: null, location_type: null, version: 4 } };

  it('its link opens in a new tab', async () => {
    await renderSheet(sheetViewOf({ outputs: [output] }));
    const link = within(screen.getByTestId('output-o1')).getByRole('link', { name: 'Facture' });
    expect([link.getAttribute('href'), link.getAttribute('target'), link.getAttribute('rel')]).toEqual(['https://localhost/facture', '_blank', 'noopener noreferrer']);
  });

  it('a refusal is said, the form kept with what was typed; saved again with its version, then removed: « Aucune sortie déclarée. »', async () => {
    const { sent, record } = await renderSheet(sheetViewOf({ outputs: [output] }), {
      'PUT /api/steps/s2/documents/o1': [{ refused: 422, code: 'ADRESSE_INVALIDE', message: 'Le lien doit être une adresse web ou un chemin de fichier.' }, { json: sheetViewOf({ outputs: [changed] }) }],
      'DELETE /api/steps/s2/documents/o1': { json: sheetViewOf() },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Modifier « Facture »' }));
    const form = screen.getByRole('group', { name: 'Modifier « Facture »' });
    fireEvent.change(within(form).getByLabelText('Lien vers le document'), { target: { value: 'facture.pdf' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Enregistrer le document' }));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Le lien doit être une adresse web ou un chemin de fichier.', 'error']]);
    });
    expect(screen.getByTestId('document-form-output')).toBe(form);
    expect(within(form).getByLabelText('Lien vers le document')).toHaveValue('facture.pdf');
    fireEvent.change(within(form).getByLabelText('Lien vers le document'), { target: { value: '' } });
    fireEvent.change(within(form).getByLabelText('Nom du document'), { target: { value: 'Facture v2' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Enregistrer le document' }));
    await vi.waitFor(() => {
      expect(screen.queryByTestId('document-form-output')).toBeNull();
    });
    expect(callsTo(sent, 'PUT', '/api/steps/s2/documents/o1').map((call) => [call.body, call.headers.get('If-Match')])).toEqual([
      [{ direction: 'output', name: 'Facture', location: 'facture.pdf', wait_hours: null, share_confirmed: false }, '"3"'],
      [{ direction: 'output', name: 'Facture v2', location: null, wait_hours: null, share_confirmed: false }, '"3"'],
    ]);
    fireEvent.click(screen.getByRole('button', { name: 'Retirer « Facture v2 »' }));
    expect(await within(screen.getByTestId('sheet-outputs')).findByText('Aucune sortie déclarée.')).toBeInTheDocument();
    expect([callsTo(sent, 'DELETE', '/api/steps/s2/documents/o1').map((call) => call.headers.get('If-Match')), record.events]).toEqual([['"4"'], ['map', 'map']]);
  });
});
