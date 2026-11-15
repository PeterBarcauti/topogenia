// ReadView.test.tsx
// The sheet for who may not fill it: its latest validated version, the fields filled only, the procedure numbered, the documents used and produced, the free text, without a validated version, and nothing to write (annex B 4).

import { screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next, loadLanguage } from '../../../shared/i18n/i18n.ts';
import { allSections, renderSheet } from '../../../test/sheetPanel.tsx';
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
    expect([...read.querySelectorAll('dt')].map((term) => term.textContent)).toEqual(['Finalité', 'Fréquence', 'Durée par exécution']);
    expect([...read.querySelectorAll('dd')].map((value) => value.textContent)).toEqual(['Facturer', 'Mensuelle', '30 minutes']);
    // The key facts in the head of their section; the tools as pills, in sight whatever is folded (essai J10).
    expect(screen.getByTestId('sheet-summary-facts')).toHaveTextContent('Mensuelle · 30 minutes');
    expect([...screen.getByTestId('sheet-read-tools').querySelectorAll('.tool-pill')].map((pill) => pill.textContent)).toEqual(['ERP']);
    expect([...read.querySelectorAll('.duration-value')].map((value) => value.textContent)).toEqual(['30 minutes']);
    const steps = within(screen.getByTestId('sheet-read-procedure')).getAllByRole('listitem');
    expect(steps.map((step) => step.textContent)).toEqual(['Extraire · ERP · Extraire', 'Vérifier · Contrôler', 'Envoyer']);
    expect(steps.map((step) => step.classList.contains('is-check'))).toEqual([false, true, false]);
    expect(screen.getByTestId('sheet-read-free')).toHaveTextContent('Écrire librementNotes de l’équipe');
    expect([read.querySelectorAll('input, textarea, select').length, screen.queryByRole('button', { name: 'Je valide ma fiche' }), screen.queryByTestId('sheet-saving')]).toEqual([0, null, null]);
  });

  it('the duration with its shortest and longest after it, or one bound alone, or the bounds without a duration (Makigami M5)', async () => {
    const durationOf = async (bounds: Record<string, number | null>) => {
      const content = contentOf({ duration_minutes: 15, ...bounds });
      const shown = await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, content, permissions: reader }));
      const text = screen.getByTestId('sheet-read-duration').querySelector('dd')?.textContent;
      shown.unmount();
      return text;
    };
    expect(await durationOf({ duration_min_minutes: 10, duration_max_minutes: 25 })).toBe('15 minutes (10 à 25 minutes)');
    expect(await durationOf({ duration_min_minutes: 10 })).toBe('15 minutes (au moins 10 minutes)');
    expect(await durationOf({ duration_max_minutes: 25 })).toBe('15 minutes (au plus 25 minutes)');
    expect(await durationOf({ duration_minutes: null, duration_max_minutes: 25 })).toBe('au plus 25 minutes');
  });

  it('the numbers of the duration, of its bounds and of the lead time as the language of the interface writes them (audit 3.1 V3, U7)', async () => {
    const readOf = async (language: string) => {
      await i18next.changeLanguage(language);
      const content = contentOf({ duration_minutes: 12.5, duration_min_minutes: 10.25, duration_max_minutes: 25, lead_time: { value: 1.5, unit: 'hour' } });
      const shown = await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, content, permissions: reader }));
      const texts = ['duration', 'lead_time'].map((name) => screen.getByTestId(`sheet-read-${name}`).querySelector('dd')?.textContent);
      shown.unmount();
      return texts;
    };
    expect(await readOf('fr')).toEqual(['12,5 minutes (10,25 à 25 minutes)', '1,5 heure']);
    expect(await readOf('en')).toEqual(['12.5 minutes (10.25 to 25 minutes)', '1.5 hours']);
  });

  it('the inputs used only, with their wait, their share usable as is and their pain point; the outputs with who uses them', async () => {
    const pain = painPointOf('p1', { code: 'VIS-I002', description: 'Incomplet', status: 'in_progress', lost_hours_per_month: 2 });
    const inputs = [
      inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2', { wait_hours: 4, percent_complete_accurate: 60, pain_point_id: 'p1' }) }),
      inputOf(documentOf('d2', { name: 'Brouillon' })),
    ];
    const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [documentUseOf('o1', 's3')] }];
    await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, permissions: reader, inputs, outputs, pain_points: [pain] }));
    expect(within(screen.getByTestId('sheet-inputs')).getAllByRole('listitem').map((item) => item.dataset.testid)).toEqual(['input-d1']);
    expect(screen.getByTestId('input-d1').querySelector('.entry-details')?.textContent).toBe('attente 4 h · utilisable tel quel à 60 % · déclaré par Camille Bernard');
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VIS-I002 · en cours : Incomplet · 2 h perdues par mois');
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.getByTestId('output-o1').querySelector('.entry-details')?.textContent).toBe('utilisé par VIS-E003 · déclaré par Camille Bernard');
    expect(screen.getByTestId('sheet-summary-inputs')).toHaveTextContent('1 document');
  });
});

describe('the duration and the lead time agree with their number (audit 3.2)', () => {
  it('« 1 minute », « 1 jour ouvré », then the plural, in the forms of the language', async () => {
    const readOf = async (language: string, minutes: number, days: number) => {
      await loadLanguage(language);
      await i18next.changeLanguage(language);
      const content = contentOf({ duration_minutes: minutes, lead_time: { value: days, unit: 'day' } });
      const shown = await renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, content, permissions: reader }));
      const texts = ['duration', 'lead_time'].map((name) => screen.getByTestId(`sheet-read-${name}`).querySelector('dd')?.textContent);
      shown.unmount();
      return texts;
    };
    expect(await readOf('fr', 1, 1)).toEqual(['1 minute', '1 jour ouvré']);
    expect(await readOf('fr', 2, 2)).toEqual(['2 minutes', '2 jours ouvrés']);
    expect(await readOf('en', 1, 1)).toEqual(['1 minute', '1 working day']);
    expect(await readOf('ru', 5, 5)).toEqual(['5 мин', '5 рабочих дней']);
  });
});

describe('the action plan of a pain point, under it (Makigami M7; audit 3.1 V3, U3)', () => {
  /** The sheet of a reader whose input « Devis » has the pain point p1, its action plan as given. */
  const read = (plan: Partial<Schema<'PainPoint'>>) => {
    const inputs = [inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2', { percent_complete_accurate: 60, pain_point_id: 'p1' }) })];
    const pain = painPointOf('p1', { code: 'VIS-I002', description: 'Incomplet', ...plan });
    return renderSheet(sheetViewOf({ status: 'validated', is_draft: false, validated, permissions: reader, inputs, pain_points: [pain] }));
  };

  it('the action, who carries it and the day it is due, the day as the language of the interface writes it; each part only when given', async () => {
    const french = await read({ action: 'Relire le devis', owner: 'Paie', due_date: '2026-12-15' });
    expect(screen.getByTestId('pain-point-plan-p1').textContent).toBe('Action : Relire le devis · Responsable : Paie · Échéance : 15/12/2026');
    french.unmount();
    await i18next.changeLanguage('en');
    const english = await read({ owner: 'Payroll', due_date: '2026-12-15' });
    expect(screen.getByTestId('pain-point-plan-p1').textContent).toBe('Owner: Payroll · Due date: 12/15/2026');
    english.unmount();
  });

  it('no line under a pain point without action, owner nor due date', async () => {
    await read({ action: null, owner: null, due_date: null });
    expect([screen.getByTestId('pain-point-p1'), screen.queryByTestId('pain-point-plan-p1')].map((part) => part === null)).toEqual([false, true]);
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

  it('« Tout déplier » only where a section folds, and named after the sections drawn (audit 3.2)', async () => {
    // Nothing folds: an input offered and not used, no output.
    const bare = await renderSheet(sheetViewOf({ status: 'empty', is_draft: false, permissions: reader, inputs: [inputOf(documentOf('d2'))] }));
    expect(screen.queryByTestId('sheet-fold-all')).toBeNull();
    bare.unmount();
    // The sections drawn are unfolded; the tools, a section of the form only, are not: « Tout replier ».
    const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [] }];
    await renderSheet(sheetViewOf({ status: 'empty', is_draft: false, permissions: reader, outputs }), {}, { folded: { ...allSections(false), tools: true } });
    expect(screen.getByTestId('sheet-fold-all')).toHaveTextContent('Tout replier');
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
