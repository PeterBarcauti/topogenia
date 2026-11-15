// SheetForm.test.tsx
// The form of the sheet: its five sections in their order and their summaries, the actor, the purpose, the tools typed as pills and the other tools of the map offered, the frequency and its resumption, the duration, the sections that fold (annex B 4, AR-91, essai J10).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { allSections, renderSheet } from '../../../test/sheetPanel.tsx';
import { contentOf, documentOf, sheetViewOf } from '../../../test/sheets.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the rubrics (B-4-21 to B-4-23)', () => {
  it('in five sections, repères (acteur, finalité, fréquence, durée, then the lead time of the value stream, spec 14.6), entrées, déroulé, sorties, outils (essai J10)', async () => {
    await renderSheet(sheetViewOf());
    const form = screen.getByTestId('sheet-form');
    const titles = [...form.querySelectorAll('.field-label, .sheet-section-head span[id]')].map((title) => title.textContent);
    expect(titles).toEqual(['Repères', 'Acteur', 'Finalité', 'Fréquence', 'Durée par exécution', 'Durée la plus courte', 'Durée la plus longue', 'Délai', 'Entrées', 'Déroulé', 'Sorties', 'Outils']);
  });

  it('the key facts typed are the summary of their section: the frequency, the duration and the lead time, those entered (essai J10)', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ frequency: 'per_case', duration_minutes: 10, lead_time: { value: 1.5, unit: 'day' } }) }));
    expect(screen.getByTestId('sheet-summary-facts')).toHaveTextContent('À chaque dossier · 10 minutes · Délai 1,5 jour ouvré');
    fireEvent.change(screen.getByLabelText('Durée par exécution'), { target: { value: '' } });
    expect(screen.getByTestId('sheet-summary-facts')).toHaveTextContent('À chaque dossier · Délai 1,5 jour ouvré');
  });

  it('a sheet without a key fact says « vide » in the head of that section, and « Tout replier » folds its five sections at once (essai J10)', async () => {
    const { record } = await renderSheet(sheetViewOf());
    expect([screen.getByTestId('sheet-summary-facts').textContent, screen.getByTestId('sheet-summary-tools').textContent, screen.getByTestId('sheet-fold-all').textContent])
      .toEqual(['vide', 'vide', 'Tout replier']);
    fireEvent.click(screen.getByTestId('sheet-fold-all'));
    const heads = ['facts', 'inputs', 'procedure', 'outputs', 'tools'].map((section) => screen.getByTestId(`sheet-fold-${section}`).getAttribute('aria-expanded'));
    expect([heads, screen.getByTestId('sheet-fold-all').textContent, record.events, screen.queryByLabelText('Acteur')]).toEqual([['false', 'false', 'false', 'false', 'false'], 'Tout déplier', ['fold-all:true'], null]);
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
    const tools = screen.getByRole('textbox', { name: 'Outils' });
    expect(tools.getAttribute('maxlength')).toBe('4000');
    const held = () => [...screen.getByTestId('sheet-tools-field').querySelectorAll('.tool-pill')].map((pill) => pill.textContent);
    expect(held()).toEqual(['erp']);
    // The other tools of the map are folded under the field, their number in their head (essai J10).
    const offers = screen.getByTestId('sheet-tool-offers');
    expect([offers.getAttribute('aria-expanded'), offers.textContent, screen.queryByTestId('sheet-tool-suggestions')]).toEqual(['false', 'Autres outils de la carte2', null]);
    fireEvent.click(offers);
    const pills = within(screen.getByTestId('sheet-tool-suggestions')).getAllByRole('button');
    expect(pills.map((pill) => pill.getAttribute('aria-label'))).toEqual(['Ajouter Tableur', 'Ajouter Portail']);
    // A stable identifier, by the tool, for the journeys (S-3.5-04).
    expect(pills.map((pill) => pill.getAttribute('data-testid'))).toEqual(['sheet-tool-suggestion-Tableur', 'sheet-tool-suggestion-Portail']);
    fireEvent.click(pills[0] as HTMLElement);
    expect(held()).toEqual(['erp', 'Tableur']);
    expect(within(screen.getByTestId('sheet-tool-suggestions')).getAllByRole('button').map((pill) => pill.textContent)).toEqual(['Portail']);
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter Portail' }));
    expect([screen.queryByTestId('sheet-tool-offers'), screen.queryByTestId('sheet-tool-suggestions')]).toEqual([null, null]);
  });

  it('are pills, each in its tint with its cross; a tool is typed then ended by Enter, a comma or leaving the field, never kept twice; Backspace removes the last one (essai J10)', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ tools: 'Excel; ERP' }) }));
    const field = screen.getByTestId('sheet-tools-field');
    const held = () => [...field.querySelectorAll('.tool-pill')].map((pill) => [pill.textContent, pill.className.replace('tool-pill ', '')]);
    const input = screen.getByRole<HTMLInputElement>('textbox', { name: 'Outils' });
    expect(held()).toEqual([['Excel', 'is-tint-4'], ['ERP', expect.stringMatching(/^is-tint-[1-8]$/)]]);
    // The head of the section shows the same pills, folded or not.
    expect([...screen.getByTestId('sheet-summary-tools').querySelectorAll('.tool-pill')].map((pill) => pill.textContent)).toEqual(['Excel', 'ERP']);
    // What is typed is no pill yet; Enter ends it.
    fireEvent.change(input, { target: { value: 'Outlook' } });
    expect([held().length, input.value]).toEqual([2, 'Outlook']);
    fireEvent.keyDown(input, { key: 'Enter' });
    expect([held().map(([name]) => name), input.value]).toEqual([['Excel', 'ERP', 'Outlook'], '']);
    // A comma ends a tool, what follows it is still typed; a tool already held is not kept twice, whatever its case.
    fireEvent.change(input, { target: { value: 'excel, Tea' } });
    expect([held().map(([name]) => name), input.value]).toEqual([['Excel', 'ERP', 'Outlook'], ' Tea']);
    fireEvent.change(input, { target: { value: ' Teams' } });
    fireEvent.blur(input);
    expect([held().map(([name]) => name), input.value]).toEqual([['Excel', 'ERP', 'Outlook', 'Teams'], '']);
    // The cross removes its tool, Backspace in the empty field the last one.
    fireEvent.click(screen.getByRole('button', { name: 'Retirer ERP' }));
    fireEvent.keyDown(input, { key: 'Backspace' });
    expect(held().map(([name]) => name)).toEqual(['Excel', 'Outlook']);
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
    await renderSheet(sheetViewOf({ suggestions: { frequency: { frequency: 'weekly', step_code: 'VIS-E001' }, tools: [] } }));
    fireEvent.click(screen.getByRole('button', { name: 'Reprendre « Hebdomadaire » (étape VIS-E001)' }));
    expect(screen.queryByTestId('sheet-frequency-resume')).toBeNull();
    expect(screen.getByLabelText('Fréquence')).toHaveValue('weekly');
  });

  it('none when the frequency is filled or nothing is offered', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ frequency: 'daily' }), suggestions: { frequency: { frequency: 'weekly', step_code: 'VIS-E001' }, tools: [] } }));
    expect(screen.queryByTestId('sheet-frequency-resume')).toBeNull();
  });

  it('the duration per run and its bounds in minutes, from 0 to 100 000, with the decimals the core keeps, the arrows by 1 (B-4-28; audit 3.1 V4, A1)', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ duration_minutes: 12.5, duration_min_minutes: 10.25, duration_max_minutes: 15.5 }) }));
    const fields = ['Durée par exécution', 'Durée la plus courte', 'Durée la plus longue'].map((label) => screen.getByLabelText<HTMLInputElement>(label));
    expect(fields.map((field) => [field.type, field.min, field.max, field.step, field.value, field.validity.valid])).toEqual([
      ['number', '0', '100000', 'any', '12.5', true], ['number', '0', '100000', 'any', '10.25', true], ['number', '0', '100000', 'any', '15.5', true],
    ]);
    expect(fields[0]?.parentElement).toHaveTextContent('minutes');
  });
});

describe('the sections that fold (AR-91)', () => {
  it('folded by their head which keeps their summary; the choice is given to the panel that holds it', async () => {
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

  it('folded when the panel says so', async () => {
    await renderSheet(sheetViewOf(), {}, { folded: { ...allSections(false), inputs: true, outputs: true } });
    expect([screen.queryByTestId('sheet-inputs'), screen.queryByTestId('sheet-outputs'), screen.getByTestId('procedure-lines')].map((part) => part === null))
      .toEqual([true, true, false]);
  });
});
