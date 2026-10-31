// remainder.test.tsx
// The panel of the remainder and the dialog of what was stored: its sections, the choice « Affecter à… », the deletion, « Terminer », the sheet read only, a reader, the keys (annex B 2.9, B-2.5-04).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { frameOf, replyOf, sampleMap, stepOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const remainder: Partial<Schema<'FrameView'>> = {
  documents: [{ id: 'o', name: 'Devis', direction: 'output', used_by: ['VTE-E004'] }, { id: 'i', name: 'Demande', direction: 'input', used_by: [] }],
  usages: [{ document_id: 'u', name: 'Bon de commande', producer_code: 'VTE-E001' }],
  pain_points: [{ id: 'p', code: 'IRR-001', description: 'Ressaisie', status: 'reported' }],
};

/** A map whose frame holds s2 and a new step s5 with the remainder, unless told otherwise. */
function framed(overrides: Partial<Schema<'MapView'>> = {}): Schema<'MapView'> {
  const map = sampleMap({ dissolution: frameOf(['s5', 's2'], remainder), ...overrides });
  return { ...map, steps: [...map.steps, stepOf('s5', 'l2', 1, { code: 'VTE-E005', label: 'Chiffrer' })] };
}

/** The map shown, its panel opened by the label of the frame, found in the workspace of this editor. */
async function openPanel(overrides: Partial<Schema<'MapView'>> = {}) {
  const shown = await showEditor(framed(overrides));
  fireEvent.click(within(shown.canvas).getByTestId('dissolution-label'));
  return { ...shown, panel: within(shown.canvas.parentElement as HTMLElement).getByTestId('remainder-panel') };
}

describe('the panel of the remainder (B-2.9-24 to B-2.9-28)', () => {
  it('is docked on the right of the canvas, titled with the dissolved step, its sections in order', async () => {
    const { panel } = await openPanel();
    expect(panel).toHaveClass('side-panel');
    // In its frame, which resizes and folds (spec 3.0 section 15.2).
    expect(panel.closest('.side-dock')?.parentElement).toHaveClass('map-workspace');
    expect(within(panel).getByRole('heading', { level: 2 })).toHaveTextContent('Reliquat de VTE-E009 « Préparer la commande »');
    expect(within(panel).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual(['Documents produits et reçus', 'Documents reçus d’autres étapes', 'Irritants']);
    expect(within(panel).getByTestId('remainder-document-o')).toHaveTextContent('Devis · produit · utilisé par VTE-E004');
    expect(within(panel).getByTestId('remainder-document-i')).toHaveTextContent('Demande · reçu de l’extérieur');
    expect(within(panel).getByTestId('remainder-usage-u')).toHaveTextContent('Bon de commande · produit par VTE-E001');
    expect(within(panel).getByTestId('remainder-pain_point-p')).toHaveTextContent('Ressaisie · IRR-001');
  });

  it('shows the sheet read only: its fields, its procedure and its free text; nothing for an empty sheet', async () => {
    const content: Schema<'SheetContent'> = {
      actor: 'Assistante', purpose: null, tools: 'ERP', frequency: 'weekly', duration_minutes: 90, procedure: [{ text: 'Saisir', tool: 'ERP', nature: null }], free_text: 'Vérifier le stock.',
    };
    const sheet: Schema<'RemainderSheetView'> = { status: 'draft', content, is_draft: true, validated: null, clarification_request: null };
    const { panel } = await openPanel({ dissolution_sheet: sheet });
    const section = within(panel).getByTestId('remainder-sheet');
    expect([...section.querySelectorAll('dt, dd')].map((item) => item.textContent)).toEqual(['Acteur', 'Assistante', 'Outils', 'ERP', 'Fréquence', 'Hebdomadaire', 'Durée par exécution', '1 h 30']);
    expect([...section.querySelectorAll('.duration-value')].map((item) => item.textContent)).toEqual(['1 h 30']);
    expect([within(section).getByRole('listitem').textContent, within(section).getByText('Vérifier le stock.').tagName]).toEqual(['Saisir · ERP', 'P']);
    const empty = { ...sheet, content: { ...sheet.content, actor: null, tools: null, frequency: null, duration_minutes: null, procedure: [], free_text: ' ' } };
    const again = await openPanel({ dissolution_sheet: empty });
    expect(within(again.panel).queryByTestId('remainder-sheet')).toBeNull();
  });

  it('is not modal: Escape closes it, the other shortcuts still work on the canvas (B-2.5-04)', async () => {
    const { editor } = await openPanel();
    await userEvent.keyboard('v');
    expect(editor.store.getState().tool).toBe('select');
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('remainder-panel')).toBeNull();
  });

  it('Escape that closes the confirmation of a deletion leaves the panel open: the release of the key is no second Escape', async () => {
    const { panel } = await openPanel({ dissolution: frameOf(['s2'], { documents: [{ id: 'o', name: 'Devis', direction: 'output', used_by: [] }] }) });
    await userEvent.click(within(panel).getByTestId('remainder-delete-o'));
    expect(screen.getByTestId('remainder-delete-confirm')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect([screen.queryByTestId('remainder-delete-confirm'), screen.queryByTestId('remainder-panel')].map((part) => part === null)).toEqual([true, false]);
  });

  it('closes with the frame (B-2.9-40)', async () => {
    const { editor } = await openPanel();
    act(() => {
      editor.store.setState({ map: sampleMap() });
    });
    expect(screen.queryByTestId('remainder-panel')).toBeNull();
  });
});

describe('the choice « Affecter à… » (B-2.9-29 to B-2.9-31)', () => {
  it('offers the steps of the frame then the others, in the reading order, without start nor end; the map for a pain point only', async () => {
    const { panel } = await openPanel();
    const groups = (id: string) => [...within(panel).getByTestId(`remainder-assign-${id}`).querySelectorAll('optgroup')].map((group) => [group.label, [...group.querySelectorAll('option')].map((option) => option.textContent)]);
    expect(groups('o')).toEqual([['Étapes du cadre', ['VTE-E002 · Vérifier la demande', 'VTE-E005 · Chiffrer']], ['Autres étapes', ['VTE-E003 · Conforme ?']]]);
    const options = (id: string) => [...within(panel).getByTestId<HTMLSelectElement>(`remainder-assign-${id}`).options].map((option) => option.textContent);
    expect([options('o').at(0), options('o').at(-1), options('p').at(-1)]).toEqual(['Affecter à…', 'VTE-E003 · Conforme ?', 'La carte (sans étape)']);
  });

  it('sends the element to the step chosen, or to the map', async () => {
    const sent = fakeApi({ 'POST /api/dissolutions/d1/assign': { json: { ...replyOf(framed()), frame: null } } });
    const { panel } = await openPanel();
    await userEvent.selectOptions(within(panel).getByTestId('remainder-assign-p'), 'map');
    expect(callsTo(sent, 'POST', '/api/dissolutions/d1/assign')[0]?.body).toEqual({ kind: 'pain_point', object_id: 'p', step_id: null, from_step_id: null });
    await userEvent.selectOptions(within(panel).getByTestId('remainder-assign-o'), 's2');
    expect(callsTo(sent, 'POST', '/api/dissolutions/d1/assign')[1]?.body).toEqual({ kind: 'document', object_id: 'o', step_id: 's2', from_step_id: null });
  });
});

describe('the deletion and « Terminer » (B-2.9-32, B-2.9-36, B-2.9-37, AR-122)', () => {
  it('asks before deleting for good, naming the uses removed with it', async () => {
    const sent = fakeApi({ 'POST /api/dissolutions/d1/remove': { json: { ...replyOf(framed()), frame: null } } });
    const { panel } = await openPanel();
    await userEvent.click(within(panel).getByRole('button', { name: 'Supprimer « Devis »' }));
    const confirm = screen.getByTestId('remainder-delete-confirm');
    expect(within(confirm).getByText('Cette suppression est définitive. Utilisé par VTE-E004 : ces usages seront retirés aussi.')).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('remainder-delete-confirm-confirm'));
    expect(callsTo(sent, 'POST', '/api/dissolutions/d1/remove').map((call) => call.body)).toEqual([{ kind: 'document', object_id: 'o' }]);
  });

  it('« Terminer » with its help stores the rest and says where it went; « Fermer le cadre » when nothing is left', async () => {
    const arrangements = [
      { kind: 'document', object_id: 'o', name: 'Devis', step_id: 's5', step_code: 'VTE-E005' }, { kind: 'usage', object_id: 'u', name: 'Bon de commande', step_id: null, step_code: 'VTE-E002' },
      { kind: 'pain_point', object_id: 'p', name: 'IRR-001 Ressaisie', step_id: null, step_code: null }, { kind: 'document', object_id: 'i', name: 'Demande', step_id: null, step_code: null },
    ];
    fakeApi({ 'POST /api/dissolutions/d1/finish': { json: { ...replyOf(sampleMap()), arrangements } } });
    const { panel } = await openPanel();
    const finish = within(panel).getByTestId('remainder-finish');
    expect([finish.textContent, finish.getAttribute('title')]).toEqual(['Terminer', expect.stringContaining('documents produits à la dernière étape du cadre')]);
    await userEvent.click(finish);
    const dialog = await screen.findByTestId('arrangements');
    expect(within(dialog).getByRole('heading')).toHaveTextContent('Reliquat de VTE-E009 rangé');
    expect(within(dialog).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Devis → VTE-E005', 'Bon de commande : déjà reçu par VTE-E002', 'IRR-001 Ressaisie → la carte (sans étape)', 'Demande : supprimé (aucune étape sur la carte)',
    ]);
    expect([within(dialog).queryByTestId('arrangements-auto'), document.activeElement]).toEqual([null, within(dialog).getByTestId('arrangements-done')]);
    const empty = await openPanel({ dissolution: frameOf(['s2', 's5']) });
    expect(within(empty.panel).getByTestId('remainder-finish')).toHaveTextContent('Fermer le cadre');
    expect([within(empty.panel).getByText('Rien à ventiler.').tagName, within(empty.panel).queryAllByRole('heading', { level: 3 })]).toEqual(['P', []]);
  });

  it('a reader sees the remainder without the choices nor the buttons', async () => {
    const { panel } = await openPanel({ rights: { structure: false, role: 'reader' } });
    expect(within(panel).getByTestId('remainder-document-o')).toHaveTextContent('Devis');
    expect([within(panel).queryAllByRole('combobox'), within(panel).queryByTestId('remainder-finish'), within(panel).queryByRole('button', { name: 'Supprimer « Devis »' })]).toEqual([[], null, null]);
  });

  it('a frame stored on its own says so first', async () => {
    const arrangements = [{ kind: 'pain_point', object_id: 'p', name: 'IRR-001 Ressaisie', step_id: 's2', step_code: 'VTE-E002' }];
    fakeApi({ 'POST /api/dissolutions/d1/finish': { json: { ...replyOf(sampleMap()), arrangements } } });
    await showEditor(sampleMap({ dissolution: frameOf(['s2'], remainder) }));
    const dialog = await screen.findByTestId('arrangements');
    expect(within(dialog).getByTestId('arrangements-auto')).toHaveTextContent('Il ne reste qu’une étape (ou aucune) dans le cadre');
    expect(within(dialog).getByRole('listitem')).toHaveTextContent('IRR-001 Ressaisie → VTE-E002');
  });
});
