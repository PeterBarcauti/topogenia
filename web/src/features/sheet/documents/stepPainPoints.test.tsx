// stepPainPoints.test.tsx
// R10 on the sheet: « Irritants de l'étape », the pain points of the step without a document, after the outputs, folded with its count, read only but for the status button of who may fill the sheet; under a pain point a consumer of an output noted, the status button and « Modifier dans CODE » for who may fill the sheet of that consumer.

import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo } from '../../../test/fakeApi.ts';
import { allSections, renderSheet } from '../../../test/sheetPanel.tsx';
import { documentOf, documentUseOf, painPointOf, sheetViewOf } from '../../../test/sheets.ts';

const STATUS = '/api/steps/s2/pain-points/p9/status';
const CONSUMER_STATUS = '/api/steps/s3/pain-points/p1/status';
const IN_PROGRESS = 'En cours — action en cours de mise en œuvre';

/** A pain point of the step s2 that no use carries, with its plan. */
const bare = painPointOf('p9', {
  code: 'VIS-I009', description: 'Planning illisible', lost_hours_per_month: 2, action: 'Refaire le planning', owner: 'Accueil', due_date: '2026-12-15', version: 3,
});
const withBare = (overrides: Parameters<typeof sheetViewOf>[0] = {}) => sheetViewOf({ step_pain_points: [bare], ...overrides });
const statusButton = (code: string) => screen.queryByRole('button', { name: (name) => name.startsWith(`État de l’irritant ${code} : `) });

/** The output « Facture » of s2, received by s3 (on this map) with the pain point p1. */
const outputs = [{ document: documentOf('o1', { step_id: 's2', name: 'Facture' }), used_by: [documentUseOf('o1', 's3', { pain_point_id: 'p1' })] }];
const noted = painPointOf('p1', { code: 'VIS-I001', description: 'Ressaisie à la main', version: 5 });
const producer = sheetViewOf({ outputs, pain_points: [noted] });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('« Irritants de l’étape »: the pain points of the step without a document (R10)', () => {
  it('a section after the outputs, with its count, that shows the code, the status, the description, the time lost and the plan, none of them to type', async () => {
    await renderSheet(withBare());
    const heads = [...screen.getByTestId('sheet-form').querySelectorAll('[data-testid^="sheet-section-"]')].map((section) => section.getAttribute('data-testid'));
    expect(heads).toEqual(['facts', 'inputs', 'procedure', 'outputs', 'pain_points', 'tools'].map((section) => `sheet-section-${section}`));
    expect(screen.getByTestId('sheet-fold-pain_points')).toHaveTextContent('Irritants de l’étape');
    expect(screen.getByTestId('sheet-summary-pain_points')).toHaveTextContent('1 irritant');
    const section = screen.getByTestId('sheet-section-pain_points');
    expect(section).toHaveTextContent('Irritants rattachés à l’étape, sans document.');
    // The sentence about the status is there because the button is.
    expect(section).toHaveTextContent('Leur état se change ici');
    expect(screen.getByTestId('pain-point-p9')).toHaveTextContent('VIS-I009 · signalé : Planning illisible · 2 h perdues par mois');
    expect(screen.getByTestId('pain-point-plan-p9').textContent).toBe('Action : Refaire le planning · Responsable : Accueil · Échéance : 15/12/2026');
    expect(within(section).queryAllByRole('textbox')).toEqual([]);
    expect(statusButton('VIS-I009')).not.toBeNull();
  });

  it('none without such a pain point; « Tout déplier » leaves it out then', async () => {
    const { record } = await renderSheet(sheetViewOf(), {}, { folded: { ...allSections(false), pain_points: true } });
    expect(screen.queryByTestId('sheet-section-pain_points')).toBeNull();
    // Every section drawn is unfolded: the button folds them all.
    expect(screen.getByTestId('sheet-fold-all')).toHaveTextContent('Tout replier');
    fireEvent.click(screen.getByTestId('sheet-fold-all'));
    expect(record.events).toEqual(['fold-all:true']);
  });

  it('the status chosen is written under the step of the sheet with the version of the pain point; the sheet given back is shown', async () => {
    const after = withBare({ step_pain_points: [{ ...bare, status: 'in_progress', version: 4 }] });
    const { sent } = await renderSheet(withBare(), { [`PUT ${STATUS}`]: { json: after } });
    await userEvent.click(statusButton('VIS-I009') ?? document.body);
    await userEvent.click(within(screen.getByRole('menu', { name: 'État de l’irritant VIS-I009' })).getByRole('menuitemradio', { name: IN_PROGRESS }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('pain-point-p9')).toHaveTextContent('VIS-I009 · en cours : Planning illisible');
    });
    expect(callsTo(sent, 'PUT', STATUS).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'in_progress' }, '"3"']]);
  });

  it('who may not fill the sheet reads the section and the status, without the button; nor in the view in BPMN 2.0', async () => {
    const reading = { is_draft: false, permissions: { fill: false, validate: false, request_clarification: false, assign: false } };
    const read = await renderSheet(withBare(reading));
    expect(screen.getByTestId('sheet-section-pain_points')).toBeVisible();
    expect(screen.getByTestId('pain-point-p9')).toHaveTextContent('VIS-I009 · signalé');
    expect(statusButton('VIS-I009')).toBeNull();
    expect(screen.getByTestId('sheet-section-pain_points')).not.toHaveTextContent('Leur état se change ici');
    expect(screen.getByTestId('sheet-section-pain_points')).toHaveTextContent('Leur description et leur plan se lisent seulement.');
    read.unmount();
    await renderSheet(withBare({ is_draft: false }), {}, { readOnly: true });
    expect(screen.getByTestId('pain-point-p9')).toBeVisible();
    expect(statusButton('VIS-I009')).toBeNull();
    expect(screen.getByTestId('sheet-section-pain_points')).not.toHaveTextContent('Leur état se change ici');
  });
});

describe('the outputs: the pain point a consumer noted, its status and « Modifier dans CODE » (R10)', () => {
  it('for who may fill the sheet of the consumer: the status button and « Modifier dans VIS-E003 » in place of « Signaler un irritant… »', async () => {
    const asked: [string, string][] = [];
    await renderSheet(producer, {}, { onReport: (stepId, documentId) => asked.push([stepId, documentId]) });
    const output = screen.getByTestId('output-o1');
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VIS-I001 · signalé : Ressaisie à la main');
    expect([...output.querySelectorAll('.report-pain')].map((link) => link.textContent)).toEqual(['Modifier dans VIS-E003']);
    expect(statusButton('VIS-I001')).not.toBeNull();
    // Named by what it shows, then by its document (R4-12).
    fireEvent.click(screen.getByRole('button', { name: 'Modifier dans VIS-E003 Facture' }));
    expect(asked).toEqual([['s3', 'o1']]);
  });

  it('the status chosen is written under the step that receives, then the sheet of the producer is read again', async () => {
    const after = sheetViewOf({ outputs, pain_points: [{ ...noted, status: 'in_progress', version: 6 }] });
    const { sent } = await renderSheet(producer, { [`PUT ${CONSUMER_STATUS}`]: { json: sheetViewOf({ step_id: 's3' }) }, 'GET /api/steps/s2/sheet': [{ json: producer }, { json: after }] },
      { onReport: () => undefined });
    await userEvent.click(statusButton('VIS-I001') ?? document.body);
    await userEvent.click(within(screen.getByRole('menu', { name: 'État de l’irritant VIS-I001' })).getByRole('menuitemradio', { name: IN_PROGRESS }));
    await vi.waitFor(() => {
      expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VIS-I001 · en cours');
    });
    expect(callsTo(sent, 'PUT', CONSUMER_STATUS).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'in_progress' }, '"5"']]);
    expect(callsTo(sent, 'GET', '/api/steps/s2/sheet')).toHaveLength(2);
    expect(callsTo(sent, 'PUT', '/api/steps/s2/pain-points/p1/status')).toEqual([]);
  });

  it('without the right to fill the sheet of the consumer, the status is read: no button, no link; nor in the view in BPMN 2.0', async () => {
    const refused = await renderSheet(producer, {}, { onReport: () => undefined }, () => false);
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VIS-I001 · signalé');
    expect(statusButton('VIS-I001')).toBeNull();
    expect(screen.getByTestId('output-o1').querySelectorAll('.report-pain')).toHaveLength(0);
    refused.unmount();
    await renderSheet({ ...producer, is_draft: false }, {}, { onReport: () => undefined, readOnly: true });
    expect(screen.getByTestId('pain-point-p1')).toHaveTextContent('VIS-I001 · signalé');
    expect(statusButton('VIS-I001')).toBeNull();
    expect(screen.getByTestId('output-o1').querySelectorAll('.report-pain')).toHaveLength(0);
  });
});
