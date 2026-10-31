// SheetForm.test.tsx
// The form of the sheet: its rubrics in their order, the actor, the purpose, the tools and the pills of the tools cited, the frequency and its resumption, the duration, the sections that fold (annex B 4, AR-91).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { renderSheet } from '../../../test/sheetPanel.tsx';
import { contentOf, documentOf, sheetViewOf } from '../../../test/sheets.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the rubrics (B-4-21 to B-4-23)', () => {
  it('in the order acteur, finalité, entrées, déroulé, sorties, outils, fréquence, durée, then the lead time of the value stream (spec 14.6)', async () => {
    await renderSheet(sheetViewOf());
    const form = screen.getByTestId('sheet-form');
    const titles = [...form.querySelectorAll('.field-label, .sheet-section-head span[id]')].map((title) => title.textContent);
    expect(titles).toEqual(['Acteur', 'Finalité', 'Entrées', 'Déroulé', 'Sorties', 'Outils', 'Fréquence', 'Durée par exécution', 'Délai']);
  });

  it('the actor offers the name of the lane, 4000 characters; the purpose holds two lines of 4000; each with its help', async () => {
    await renderSheet(sheetViewOf());
    const actor = screen.getByLabelText('Acteur');
    const purpose = screen.getByLabelText('Finalité');
    expect([actor.getAttribute('placeholder'), actor.getAttribute('maxlength'), purpose.tagName, purpose.getAttribute('rows'), purpose.getAttribute('maxlength')])
      .toEqual(['Commercial', '4000', 'TEXTAREA', '2', '4000']);
    expect(actor).toHaveAccessibleDescription('Qui réalise l’étape');
  });
});

describe('the tools (B-4-24, B-4-25)', () => {
  it('a field of 4000 characters; a pill « + outil » for each tool cited on the map and not here, which adds it after the others', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ tools: 'erp' }), suggestions: { frequency: null, tools: ['ERP', 'Tableur', 'Portail'] } }));
    const tools = screen.getByLabelText('Outils');
    expect(tools.getAttribute('maxlength')).toBe('4000');
    const pills = within(screen.getByTestId('sheet-tool-suggestions')).getAllByRole('button');
    expect(pills.map((pill) => pill.getAttribute('aria-label'))).toEqual(['Ajouter Tableur', 'Ajouter Portail']);
    // A stable identifier, by the tool, for the journeys (S-3.5-04).
    expect(pills.map((pill) => pill.getAttribute('data-testid'))).toEqual(['sheet-tool-suggestion-Tableur', 'sheet-tool-suggestion-Portail']);
    fireEvent.click(pills[0] as HTMLElement);
    expect(tools).toHaveValue('erp, Tableur');
    expect(within(screen.getByTestId('sheet-tool-suggestions')).getAllByRole('button').map((pill) => pill.textContent)).toEqual(['Portail']);
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter Portail' }));
    expect(screen.queryByTestId('sheet-tool-suggestions')).toBeNull();
  });

  it('the tools of the map are offered to the tool of each operation', async () => {
    await renderSheet(sheetViewOf({ suggestions: { frequency: null, tools: ['ERP', 'Tableur'] } }));
    const tool = screen.getByLabelText('Outil de l’opération 1');
    const list = document.getElementById(tool.getAttribute('list') ?? '');
    expect([...(list?.querySelectorAll('option') ?? [])].map((option) => option.getAttribute('value'))).toEqual(['ERP', 'Tableur']);
  });
});

describe('the frequency and the duration (B-4-26 to B-4-28)', () => {
  it('the eight frequencies after « Choisir… », « À chaque dossier » last (spec 14.2, S5)', async () => {
    await renderSheet(sheetViewOf());
    expect(within(screen.getByLabelText('Fréquence')).getAllByRole('option').map((option) => option.textContent))
      .toEqual(['Choisir…', 'Quotidienne', 'Hebdomadaire', 'Mensuelle', 'Clôture mensuelle', 'Trimestrielle', 'Annuelle', 'Ponctuelle', 'À chaque dossier']);
  });

  it('the lead time: a number and its unit, minutes, hours or working days, hours by default (spec 14.2, S3)', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ lead_time: { value: 2, unit: 'day' } }) }));
    expect(screen.getByLabelText('Délai')).toHaveValue(2);
    expect(within(screen.getByTestId('sheet-lead-time-unit')).getAllByRole('option').map((option) => option.textContent)).toEqual(['minutes', 'heures', 'jours ouvrés']);
    expect(screen.getByTestId('sheet-lead-time-unit')).toHaveValue('day');
  });

  it('while it is empty, « Reprendre « X » (étape CODE) » takes the frequency of another step, then goes', async () => {
    await renderSheet(sheetViewOf({ suggestions: { frequency: { frequency: 'weekly', step_code: 'VTE-E001' }, tools: [] } }));
    fireEvent.click(screen.getByRole('button', { name: 'Reprendre « Hebdomadaire » (étape VTE-E001)' }));
    expect(screen.queryByTestId('sheet-frequency-resume')).toBeNull();
    expect(screen.getByLabelText('Fréquence')).toHaveValue('weekly');
  });

  it('none when the frequency is filled or nothing is offered', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ frequency: 'daily' }), suggestions: { frequency: { frequency: 'weekly', step_code: 'VTE-E001' }, tools: [] } }));
    expect(screen.queryByTestId('sheet-frequency-resume')).toBeNull();
  });

  it('the duration per run in minutes, from 0 to 100 000 by 1', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ duration_minutes: 20 }) }));
    const duration = screen.getByLabelText('Durée par exécution');
    expect([duration.getAttribute('type'), duration.getAttribute('min'), duration.getAttribute('max'), duration.getAttribute('step'), (duration as HTMLInputElement).value])
      .toEqual(['number', '0', '100000', '1', '20']);
    expect(duration.parentElement).toHaveTextContent('minutes');
  });
});

describe('the sections that fold (AR-91)', () => {
  it('open by default, folded by their head which keeps their summary; the choice goes to the preferences', async () => {
    const view = sheetViewOf({ outputs: [{ document: documentOf('d1', { step_id: 's2' }), used_by: [] }], content: contentOf({ procedure: [{ text: 'Saisir', tool: null, nature: null }] }) });
    const { record } = await renderSheet(view);
    const heads = ['inputs', 'procedure', 'outputs'].map((section) => screen.getByTestId(`sheet-fold-${section}`));
    expect(heads.map((head) => [head.getAttribute('aria-expanded'), head.querySelector('.count')?.textContent])).toEqual([
      ['true', 'vide'], ['true', '1 opération'], ['true', '1 document'],
    ]);
    fireEvent.click(heads[1] as HTMLElement);
    expect([heads[1]?.getAttribute('aria-expanded'), heads[1]?.title, screen.queryByTestId('procedure-lines'), record.events]).toEqual([
      'false', 'Déplier « Déroulé »', null, ['fold:procedure:true'],
    ]);
    expect(screen.getByTestId('sheet-summary-procedure')).toHaveTextContent('1 opération');
    fireEvent.click(heads[1] as HTMLElement);
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une opération' }));
    expect([screen.getAllByRole('textbox', { name: /^Opération \d+$/ }).length, screen.getByTestId('sheet-summary-procedure').textContent]).toEqual([2, '1 opération']);
  });

  it('folded when the preferences say so', async () => {
    await renderSheet(sheetViewOf(), {}, { folded: { inputs: true, procedure: false, outputs: true } });
    expect([screen.queryByTestId('sheet-inputs'), screen.queryByTestId('sheet-outputs'), screen.getByTestId('procedure-lines')].map((part) => part === null))
      .toEqual([true, true, false]);
  });
});
