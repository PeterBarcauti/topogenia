// ReadView.test.tsx
// The sheet for who may not fill it: its latest validated version, the fields filled only, the procedure numbered, the documents used and produced, the free text, without a validated version, and nothing to write (annex B 4).

import { screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { AUTHOR_ID, contentOf, documentOf, inputOf, painPointOf, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';

const reader = { fill: false, validate: false, request_clarification: false, assign: false };
const validated = { number: 2, validated_at: '2026-10-21T08:00:00Z', validated_by: AUTHOR_ID };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the latest validated version (B-4-58, B-4-59)', () => {
  it('who validated it and when, then only the fields filled, the procedure numbered with its tools and natures, the free text; no field to type in', async () => {
    const content = contentOf({
      purpose: 'Facturer', tools: 'ERP', frequency: 'monthly', duration_minutes: 30, free_text: 'Notes de l’équipe',
      procedure: [{ text: 'Extraire', tool: 'ERP', nature: 'extract' }, { text: 'Vérifier', tool: null, nature: 'check' }, { text: 'Envoyer', tool: null, nature: null }],
    });
    await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, content, permissions: reader }));
    const read = screen.getByTestId('sheet-read');
    expect(screen.getByTestId('sheet-read-validated')).toHaveTextContent('Version validée le 21/10/2026 par Camille Bernard.');
    expect([...read.querySelectorAll('dt')].map((term) => term.textContent)).toEqual(['Finalité', 'Outils', 'Fréquence', 'Durée par exécution']);
    expect([...read.querySelectorAll('dd')].map((value) => value.textContent)).toEqual(['Facturer', 'ERP', 'Mensuelle', '30 minutes']);
    expect([...read.querySelectorAll('.duration-value')].map((value) => value.textContent)).toEqual(['30 minutes']);
    const steps = within(screen.getByTestId('sheet-read-procedure')).getAllByRole('listitem');
    expect(steps.map((step) => step.textContent)).toEqual(['Extraire · ERP · Extraire', 'Vérifier · Contrôler', 'Envoyer']);
    expect(steps.map((step) => step.classList.contains('is-check'))).toEqual([false, true, false]);
    expect(screen.getByTestId('sheet-read-free')).toHaveTextContent('Écrire librementNotes de l’équipe');
    expect([read.querySelectorAll('input, textarea, select').length, screen.queryByRole('button', { name: 'Je valide ma fiche' }), screen.queryByTestId('sheet-saving')]).toEqual([0, null, null]);
  });

  it('the inputs used only, with their wait, their share usable as is and their pain point; the outputs with who uses them', async () => {
    const pain = painPointOf('p1', { code: 'VTE-I002', description: 'Incomplet', status: 'in_progress', lost_hours_per_month: 2 });
    const inputs = [
      inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2', { wait_hours: 4, percent_complete_accurate: 60, pain_point_id: 'p1' }) }),
      inputOf(documentOf('d2', { name: 'Brouillon' })),
    ];
    const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [documentUseOf('o1', 's3')] }];
    await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, permissions: reader, inputs, outputs, pain_points: [pain] }));
    expect(within(screen.getByTestId('sheet-inputs')).getAllByRole('listitem').map((item) => item.dataset.testid)).toEqual(['input-d1']);
    expect(screen.getByTestId('input-d1').querySelector('.entry-details')?.textContent).toBe('attente 4 h · utilisable tel quel à 60 % · déclaré par Camille Bernard');
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VTE-I002 · en cours : Incomplet · 2 h perdues par mois');
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.getByTestId('output-o1').querySelector('.entry-details')?.textContent).toBe('utilisé par VTE-E003 · déclaré par Camille Bernard');
    expect(screen.getByTestId('sheet-summary-inputs')).toHaveTextContent('1 document');
  });
});

describe('no validated version (B-4-60, B-4-61)', () => {
  it('« Aucune fiche validée… », the documents still shown', async () => {
    const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [] }];
    await renderSheet(sheetViewOf({ status: 'empty', is_draft: false, permissions: reader, outputs, content: contentOf({ purpose: 'Caché', free_text: 'Caché aussi' }) }));
    expect(screen.getByTestId('sheet-read-empty')).toHaveTextContent('Aucune fiche validée pour cette étape pour l’instant.');
    expect([screen.getByTestId('output-o1'), screen.queryByText('Caché'), screen.queryByTestId('sheet-read-procedure'), screen.queryByTestId('sheet-read-free')].map((part) => part === null))
      .toEqual([false, true, true, true]);
  });

  it('nothing of the documents when the step neither uses nor produces any', async () => {
    await renderSheet(sheetViewOf({ status: 'empty', is_draft: false, permissions: reader, inputs: [inputOf(documentOf('d2'))] }));
    expect([screen.queryByTestId('sheet-section-inputs'), screen.queryByTestId('sheet-section-outputs')]).toEqual([null, null]);
  });
});

describe('a reader writes nothing (B-4-58, B-4-70, B-4-77, B-4-78)', () => {
  it('in the whole panel, no button that writes a document and no field to type in, the documents shown', async () => {
    const external = inputOf(documentOf('e1', { direction: 'input', step_id: 's2', name: 'Relevé bancaire' }), { producer: null, near: false, used: true });
    const produced = inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2') });
    const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [] }];
    await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, permissions: reader, inputs: [external, produced], outputs, content: contentOf({ purpose: 'Facturer' }) }));
    const panel = screen.getByTestId('sheet-panel');
    expect(['input-e1', 'input-d1', 'output-o1'].map((id) => within(panel).queryByTestId(id) === null)).toEqual([false, false, false]);
    const writes = ['Modifier « Relevé bancaire »', 'Retirer « Relevé bancaire »', 'Modifier « Facture »', 'Retirer « Facture »', 'Entrée extérieure', 'Ajouter une sortie', 'Autre…'];
    expect(writes.filter((name) => within(panel).queryByRole('button', { name }) !== null)).toEqual([]);
    expect(panel.querySelectorAll('input, textarea, select')).toHaveLength(0);
  });
});
