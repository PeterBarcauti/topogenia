// status.test.tsx
// The status of a pain point in the sheet of the receiving step: a button that shows it and opens five choices in the order of the PDCA wheel, dropped apart, each with its help on its line; the arrows write nothing, the choice is written once, after the writes of the use; read only for who may not fill the sheet (ADR 116).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo, heldReply } from '../../../test/fakeApi.ts';
import { mountSheet, renderSheet } from '../../../test/sheetPanel.tsx';
import { AUTHOR_ID, documentOf, documentUseOf, inputOf, painPointOf, sheetViewOf } from '../../../test/sheets.ts';
import { OFFERED_STATUSES, statusChoices } from '../model/painPointStatus.ts';

const STATUS = '/api/steps/s2/pain-points/p1/status';
const LIST = 'État de l’irritant VIS-I001';
const HELP = 'Réalisé, adopté ou abandonné : l’irritant quitte la carte et ses heures ne comptent plus.';
const FIVE = [
  'Signalé — problème repéré, action à prévoir', 'En cours — action en cours de mise en œuvre', 'Réalisé — action faite, résultat à vérifier',
  'Adopté — vérifié, c’est la nouvelle façon de faire', '|', 'Abandonné — on décide de ne pas le traiter',
];

const ticked = (overrides: Partial<Schema<'DocumentUse'>> = {}) =>
  inputOf(documentOf('d1', { name: 'Devis' }), { used: true, use: documentUseOf('d1', 's2', { version: 3, ...overrides }) });
/** The sheet whose input « Devis » carries the pain point p1, as given. */
const withPain = (pain: Partial<Schema<'PainPoint'>> = {}, use: Partial<Schema<'DocumentUse'>> = {}, sheet: Partial<Schema<'SheetView'>> = {}) =>
  sheetViewOf({ inputs: [ticked({ pain_point_id: 'p1', ...use })], pain_points: [painPointOf('p1', { description: 'Sans numéro', ...pain })], ...sheet });
/** The status button of the pain point, named by it and by the status it shows. */
const isButton = (name: string) => name.startsWith(`${LIST} : `);
const button = () => screen.getByRole<HTMLButtonElement>('button', { name: isButton });
/** The list the button opened, or none. */
const menu = () => screen.queryByRole('menu', { name: LIST });
const items = () => within(menu() ?? document.body).getAllByRole('menuitemradio');
/** The entries of the open list, each by its name (the status and its help), a separator as « | ». */
const entries = () => items().flatMap((item) => [...(item.classList.contains('menu-item--group') ? ['|'] : []), item.getAttribute('aria-label') ?? '']);
const marked = () => items().filter((item) => item.getAttribute('aria-checked') === 'true').map((item) => item.getAttribute('aria-label'));
const entry = (name: string | undefined) => within(menu() ?? document.body).getByRole('menuitemradio', { name: name ?? '' });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the status button of a pain point in the sheet of the receiving step (ADR 116)', () => {
  it('the button shows the status alone; its list offers reported, in progress, implemented, adopted, dropped apart, each with its help on its line, and the status it has when another', async () => {
    const shown = await renderSheet(withPain());
    expect([button().textContent, button().getAttribute('aria-haspopup'), button().getAttribute('aria-expanded')]).toEqual(['Signalé', 'menu', 'false']);
    expect(button()).toHaveAccessibleName('État de l’irritant VIS-I001 : Signalé');
    expect(button()).toHaveAccessibleDescription(HELP);
    expect(screen.getByTestId('use-pain-d1')).toHaveTextContent('État');
    await userEvent.click(button());
    expect([entries(), marked(), button().getAttribute('aria-expanded')]).toEqual([FIVE, [FIVE[0]], 'true']);
    expect(items().map((item) => item.dataset.testid)).toEqual(OFFERED_STATUSES.map((status) => `use-status-p1-menu-${status}`));
    // The help is on its own line under the name, seen by the mouse, the keyboard and the touch alike, and read aloud with it.
    expect([...entry(FIVE[2]).querySelectorAll('.menu-name, .menu-help')].map((line) => line.textContent)).toEqual(['Réalisé', 'action faite, résultat à vérifier']);
    shown.unmount();

    // A status of the format the list does not offer (a package): shown where the seven place it, nothing lost by opening the list.
    for (const [status, line, name] of [['scored', 'Pris en compte — irritant évalué, action à décider', 'Pris en compte'], ['under_review', 'À l’étude — action à l’étude, pas encore décidée', 'À l’étude']] as const) {
      const other = await renderSheet(withPain({ status }));
      expect(button().textContent).toBe(name);
      await userEvent.click(button());
      expect([entries(), marked()]).toEqual([[FIVE[0], line, ...FIVE.slice(1)], [line]]);
      other.unmount();
    }
  });

  it('no button before the pain point exists on the host', async () => {
    const { sent } = await renderSheet(sheetViewOf({ inputs: [ticked()] }), { 'PUT /api/steps/s2/uses/d1': { json: withPain() } });
    const pain = screen.getByLabelText('Irritant sur « Devis »');
    fireEvent.change(pain, { target: { value: 'Sans numéro' } });
    // Typed, not yet held by the host: its time lost and its plan are offered, not its status.
    expect(screen.getByLabelText('Temps perdu à cause de « Devis », en heures par mois')).toBeVisible();
    expect(screen.queryByRole('button', { name: (name) => name.includes('État de l’irritant') })).toBeNull();
    fireEvent.blur(pain);
    expect((await screen.findByRole('button', { name: isButton })).textContent).toBe('Signalé');
    expect(callsTo(sent, 'PUT', STATUS)).toEqual([]);
  });

  it('a sheet read only, a reader, the BPMN view: the status is read, no button', async () => {
    const reader = { fill: false, validate: false, request_clarification: false, assign: false };
    const validated = { number: 2, validated_at: '2026-10-21T08:00:00Z', validated_by: AUTHOR_ID };
    const read = await renderSheet(withPain({ status: 'implemented' }, {}, { status: 'validated', is_draft: false, validated, permissions: reader }));
    const said = () => screen.getByTestId('pain-point-p1').textContent;
    const choosers = () => screen.getByTestId('sheet-panel').querySelectorAll('select, [aria-haspopup]').length;
    expect([said(), screen.queryByRole('button', { name: isButton }), choosers()]).toEqual(['VIS-I001 · réalisé : Sans numéro', null, 0]);
    read.unmount();

    // The view in BPMN 2.0 changes nothing, whatever the rights of the person (ADR 106).
    mountSheet({ 'GET /api/steps/s2/sheet': { json: withPain({ status: 'dropped' }) } }, { readOnly: true });
    await screen.findByTestId('sheet-read');
    expect([said(), screen.queryByRole('button', { name: isButton }), choosers()]).toEqual(['VIS-I001 · abandonné : Sans numéro', null, 0]);
  });
});

describe('the status chosen from the keyboard in the sheet (ADR 116, review M1)', () => {
  it('Enter, Space or Alt+Down opens the list on the status of the moment; the arrows move and write nothing; Escape closes it, the sheet stays open, the focus back on the button', async () => {
    const { sent, record } = await renderSheet(withPain({ status: 'in_progress' }), { [`PUT ${STATUS}`]: { json: withPain({ status: 'implemented', version: 2 }) } });
    for (const opening of ['{Enter}', ' ', '{Alt>}{ArrowDown}{/Alt}']) {
      act(() => {
        button().focus();
      });
      await userEvent.keyboard(opening);
      expect(document.activeElement).toBe(entry(FIVE[1]));
      await userEvent.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}');
      expect(document.activeElement).toBe(entry(FIVE[2]));
      await userEvent.keyboard('{Escape}');
      expect([menu(), document.activeElement, button().textContent]).toEqual([null, button(), 'En cours']);
    }
    await new Promise((resolve) => setTimeout(resolve, 20));
    // Escape closed the list only: the sheet was not asked to close.
    expect([callsTo(sent, 'PUT', STATUS), record.events]).toEqual([[], []]);
  });

  it('the arrows then Enter write the status chosen, once; the status of the moment chosen again writes nothing', async () => {
    const { sent } = await renderSheet(withPain(), { [`PUT ${STATUS}`]: { json: withPain({ status: 'adopted', version: 2 }) } });
    act(() => {
      button().focus();
    });
    await userEvent.keyboard('{Enter}{Enter}');
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect([callsTo(sent, 'PUT', STATUS), menu(), document.activeElement]).toEqual([[], null, button()]);

    await userEvent.keyboard('{Alt>}{ArrowDown}{/Alt}{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');
    await vi.waitFor(() => {
      expect(button().textContent).toBe('Adopté');
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(callsTo(sent, 'PUT', STATUS).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'adopted' }, '"1"']]);
    expect(document.activeElement).toBe(button());
  });
});

describe('the status written as it is chosen (ADR 116)', () => {
  it('choosing a status sends it with the version of the pain point, after the writes of the use asked before', async () => {
    const use = heldReply();
    const { sent, record } = await renderSheet(withPain(), {
      'PUT /api/steps/s2/uses/d1': use.reply,
      [`PUT ${STATUS}`]: { json: withPain({ description: 'Sans numéro du devis', status: 'implemented', version: 3 }, { version: 4 }) },
    });
    const pain = screen.getByLabelText('Irritant sur « Devis »');
    fireEvent.change(pain, { target: { value: 'Sans numéro du devis' } });
    fireEvent.blur(pain);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/steps/s2/uses/d1')).toHaveLength(1);
    });
    await userEvent.click(button());
    await userEvent.click(entry(FIVE[2]));
    // The use is not answered: the status waits for it, the choice stays shown.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect([callsTo(sent, 'PUT', STATUS), button().textContent]).toEqual([[], 'Réalisé']);
    use.give({ json: withPain({ description: 'Sans numéro du devis', version: 2 }, { version: 4 }) });
    await vi.waitFor(() => {
      expect(record.events).toEqual(['map', 'map']);
    });
    expect(callsTo(sent, 'PUT', STATUS).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ status: 'implemented' }, '"2"']]);
    expect([button().textContent, record.notes]).toEqual(['Réalisé', []]);
  });

  it('a refusal for a version changed is said and the sheet is read again', async () => {
    const changed = withPain({ status: 'in_progress', version: 2 });
    const { sent, record } = await renderSheet(withPain(), {
      'GET /api/steps/s2/sheet': [{ json: withPain() }, { json: changed }],
      [`PUT ${STATUS}`]: { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifié par quelqu’un d’autre.' },
    });
    await userEvent.click(button());
    await userEvent.click(entry(FIVE[5]));
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Modifié par quelqu’un d’autre.', 'error']]);
    });
    await vi.waitFor(() => {
      expect(button().textContent).toBe('En cours');
    });
    expect([callsTo(sent, 'PUT', STATUS).map((call) => call.headers.get('If-Match')), callsTo(sent, 'GET', '/api/steps/s2/sheet').length]).toEqual([['"1"'], 2]);
  });
});

describe('the statuses offered (ADR 116)', () => {
  const contract = JSON.parse(readFileSync(join(import.meta.dirname, '../../../api/generated/openapi.json'), 'utf8')) as
    { components: { schemas: { PainPointStatus: { enum: string[] } } } };
  const seven = contract.components.schemas.PainPointStatus.enum;

  it('the offered statuses are codes of the contract, in its order; the list of each of the seven holds it, in that order too', () => {
    expect(OFFERED_STATUSES).toEqual(seven.filter((status) => (OFFERED_STATUSES as readonly string[]).includes(status)));
    expect(OFFERED_STATUSES).toEqual(['reported', 'in_progress', 'implemented', 'adopted', 'dropped']);
    for (const status of seven as Schema<'PainPointStatus'>[]) {
      const choices = statusChoices(status);
      expect(choices).toContain(status);
      expect(choices).toEqual(seven.filter((code) => (choices as readonly string[]).includes(code)));
    }
  });

  it('each of the seven has its help, in French and in English, a key of its own', () => {
    for (const language of ['fr', 'en']) {
      expect(seven.filter((status) => !i18next.exists(`sheet.pain_point.status_helps.${status}`, { lng: language }))).toEqual([]);
    }
  });
});
