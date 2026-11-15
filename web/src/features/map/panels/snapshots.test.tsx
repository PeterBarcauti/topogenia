// snapshots.test.tsx
// The photos and the target state in the panel « Analysis » (Makigami M6, M8, P3e): a photo compared with now, taken and deleted by a facilitator, only read by a reader; the reference of a target state compared, its source said, its refresh offered to a facilitator who sees the source and refused with its reason otherwise.

import { fireEvent, render, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { french as t } from '../../../test/translations.ts';
import { SnapshotsSection } from './SnapshotsSection.tsx';

type SnapshotsView = Schema<'SnapshotsView'>;
type SnapshotView = Schema<'SnapshotView'>;
type IndicatorChange = Schema<'IndicatorChange'>;

const writing = { language: 'fr', workdayHours: 7, t };
const plain = (text: string | null | undefined) => text?.replace(/[\u00a0\u202f]/g, ' ');
const url = `/api/projects/${PROJECT_ID}/snapshots`;

const indicators: Schema<'StreamIndicators'> = {
  lead_hours: 56, handoff_wait_hours: 30, other_wait_hours: 20, work_minutes: 45, value_added_share: 0.5, coverage: 0.8, handoffs: 4,
  handoffs_per_case: 3.5, media_breaks: 2, rolled_complete_accurate: 0.85, lost_hours_per_month: 12, open_pain_points: 2,
};

const change = (indicator: string, before: number | null, after: number | null, better: boolean | null): IndicatorChange => ({
  indicator, before, after, gain: before === null || after === null ? null : after - before, gain_share: null, better,
});

/** The comparison the core would give: shorter, fewer handoffs, less value added, the work unchanged, one unknown. */
const changes: IndicatorChange[] = [
  change('lead_hours', 56, 28, true), change('handoff_wait_hours', 30, 6, true), change('other_wait_hours', 20, 18, true),
  change('work_minutes', 45, 45, null), change('value_added_share', 0.5, 0.25, false), change('handoffs', 4, 1, true), change('handoffs_per_case', 3.5, null, null),
];

const photo = (id: string, label: string): SnapshotView => ({
  id, label, origin: 'photo', created_at: '2026-09-01T09:09:00Z', created_by: 'u1', updated_at: null, version: 1, indicators, changes,
});

const viewOf = (overrides: Partial<SnapshotsView> = {}): SnapshotsView => ({
  photos: [photo('p1', 'Avant l’atelier')], current: indicators, baseline_project_id: null, baseline: null, comparison: null, source: null, ...overrides,
});

const target = (state: Schema<'BaselineSourceState'>): SnapshotsView => viewOf({
  photos: [], baseline_project_id: 'source-id', baseline: { ...photo('b1', 'État actuel'), origin: 'baseline' }, comparison: changes,
  source: { state, code: state === 'visible' ? 'CONGES' : null },
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the photos of the project (M8)', () => {
  it('compares a photo with now on demand: each indicator in its unit, the waits set in, the gain coloured by the way the core says', async () => {
    fakeApi({ [`GET ${url}`]: { json: viewOf() } });
    render(<SnapshotsSection map={sampleMap()} writing={writing} />);
    expect(await screen.findByText('Avant l’atelier')).toBeInTheDocument();
    expect(screen.queryByTestId('snapshot-comparison-p1')).toBeNull();
    fireEvent.click(screen.getByTestId('snapshot-compare-p1'));
    const table = screen.getByTestId('snapshot-comparison-p1');
    const cells = (code: string) => [...within(within(table).getByTestId(`snapshot-comparison-p1-${code}`)).getAllByRole('cell')].map((cell) => plain(cell.textContent));
    expect(cells('lead_hours')).toEqual(['8 j', '4 j', '−4 j']);
    expect(cells('work_minutes')).toEqual(['45 min', '45 min', '0']);
    expect(cells('value_added_share')).toEqual(['50 %', '25 %', '−25 %']);
    expect(cells('handoffs_per_case')).toEqual(['3,5', '?', '?']);
    expect(within(table).getByTestId('snapshot-comparison-p1-handoff_wait_hours')).toHaveClass('is-part');
    const gain = (code: string) => within(within(table).getByTestId(`snapshot-comparison-p1-${code}`)).getAllByRole('cell')[2];
    expect([gain('lead_hours')?.className, gain('value_added_share')?.className, gain('work_minutes')?.className]).toEqual(['is-better', 'is-worse', '']);
    expect(gain('lead_hours')).toHaveAttribute('title', 'mieux');
  });

  it('is taken under its label and deleted by a facilitator, the list read again from the reply', async () => {
    const sent = fakeApi({
      [`GET ${url}`]: { json: viewOf() },
      [`POST ${url}`]: { json: viewOf({ photos: [photo('p1', 'Avant l’atelier'), photo('p2', 'Après l’atelier')] }) },
      [`DELETE ${url}/p1`]: { json: viewOf({ photos: [photo('p2', 'Après l’atelier')] }) },
    });
    render(<SnapshotsSection map={sampleMap()} writing={writing} />);
    fireEvent.click(await screen.findByTestId('snapshot-take'));
    expect(screen.getByTestId('snapshot-take-form-send')).toBeDisabled();
    fireEvent.change(screen.getByTestId('snapshot-take-form-label'), { target: { value: '  Après l’atelier ' } });
    fireEvent.click(screen.getByTestId('snapshot-take-form-send'));
    expect(await screen.findByText('Après l’atelier')).toBeInTheDocument();
    expect(callsTo(sent, 'POST', url)[0]?.body).toEqual({ label: 'Après l’atelier' });
    fireEvent.click(screen.getByRole('button', { name: 'Supprimer la photo « Avant l’atelier »' }));
    expect(await screen.findByTestId('snapshot-p2')).toBeInTheDocument();
    expect(screen.queryByTestId('snapshot-p1')).toBeNull();
    expect(callsTo(sent, 'DELETE', `${url}/p1`)).toHaveLength(1);
  });

  it('is only read by a reader: neither taken nor deleted', async () => {
    fakeApi({ [`GET ${url}`]: { json: viewOf() } });
    render(<SnapshotsSection map={sampleMap({ rights: { structure: false, role: 'reader' } })} writing={writing} />);
    expect(await screen.findByTestId('snapshot-p1')).toBeInTheDocument();
    expect([screen.queryByTestId('snapshot-take'), screen.queryByTestId('snapshot-delete-p1')]).toEqual([null, null]);
  });

  it('is read again when the structure of the map changes alone, as changing the work time does: now moved with it (audit 3.1 V3, U5)', async () => {
    const sent = fakeApi({ [`GET ${url}`]: [{ json: viewOf() }, { json: viewOf({ photos: [photo('p1', 'Avant l’atelier'), photo('p2', 'Horaires changés')] }) }] });
    const map = sampleMap();
    const { rerender } = render(<SnapshotsSection map={map} writing={writing} />);
    expect(await screen.findByTestId('snapshot-p1')).toBeInTheDocument();
    rerender(<SnapshotsSection map={map} writing={writing} />);
    expect(callsTo(sent, 'GET', url)).toHaveLength(1);
    rerender(<SnapshotsSection map={{ ...map, structure_version: map.structure_version + 1 }} writing={writing} />);
    expect(await screen.findByTestId('snapshot-p2')).toBeInTheDocument();
    expect(callsTo(sent, 'GET', url)).toHaveLength(2);
  });

  it('says a refusal of the host in the section', async () => {
    fakeApi({
      [`GET ${url}`]: { json: viewOf() },
      [`POST ${url}`]: { refused: 400, code: 'CHAMP_OBLIGATOIRE', message: 'Le libellé est obligatoire.' },
    });
    render(<SnapshotsSection map={sampleMap()} writing={writing} />);
    fireEvent.click(await screen.findByTestId('snapshot-take'));
    fireEvent.change(screen.getByTestId('snapshot-take-form-label'), { target: { value: 'x' } });
    fireEvent.click(screen.getByTestId('snapshot-take-form-send'));
    expect(await screen.findByTestId('snapshots-error')).toHaveTextContent('Le libellé est obligatoire.');
    expect(screen.getByTestId('snapshot-take-form')).toBeInTheDocument();
  });
});

describe('the target state (M6)', () => {
  it('compares its reference with now, names its source, and a facilitator refreshes the reference under its label', async () => {
    const refreshed = target('visible');
    const sent = fakeApi({
      [`GET ${url}`]: { json: target('visible') },
      [`POST /api/projects/${PROJECT_ID}/baseline`]: { json: { ...refreshed, baseline: { ...refreshed.baseline, label: 'État actuel, relu' } } },
    });
    render(<SnapshotsSection map={sampleMap()} writing={writing} />);
    expect(plain((await screen.findByTestId('target-reference')).textContent)).toBe('Référence : « État actuel », 1 sept. 2026');
    expect(screen.getByTestId('target-source')).toHaveTextContent('Projet source : CONGES');
    expect(within(screen.getByTestId('target-comparison')).getAllByRole('columnheader').map((cell) => cell.textContent))
      .toEqual(['Indicateur', 'Référence', 'Maintenant', 'Écart']);
    const refresh = screen.getByTestId('target-refresh');
    expect([refresh.hasAttribute('disabled'), refresh.getAttribute('title')]).toEqual([false, 'Relit les indicateurs du projet source tels que vous les voyez ; la référence actuelle est remplacée.']);
    fireEvent.click(refresh);
    expect(screen.getByTestId('target-refresh-form-label')).toHaveValue('État actuel');
    fireEvent.change(screen.getByTestId('target-refresh-form-label'), { target: { value: 'État actuel, relu' } });
    fireEvent.click(screen.getByTestId('target-refresh-form-send'));
    expect(plain((await screen.findByText(/État actuel, relu/)).textContent)).toContain('État actuel, relu');
    expect(callsTo(sent, 'POST', `/api/projects/${PROJECT_ID}/baseline`)[0]?.body).toEqual({ label: 'État actuel, relu' });
  });

  it.each([
    ['hidden', 'facilitator', 'Le projet source ne vous est pas visible : un de ses animateurs ou un administrateur peut actualiser la référence.'],
    ['missing', 'facilitator', 'Le projet source n’existe plus : la référence reste lisible, elle ne peut plus être actualisée.'],
    ['visible', 'contributor', 'Un animateur de l’état cible actualise la référence.'],
  ] as const)('greys the refresh and says why: source %s, person %s', async (state, role, reason) => {
    fakeApi({ [`GET ${url}`]: { json: target(state) } });
    render(<SnapshotsSection map={sampleMap({ rights: { structure: false, role } })} writing={writing} />);
    const refresh = await screen.findByTestId('target-refresh');
    expect([refresh.hasAttribute('disabled'), refresh.getAttribute('title')]).toEqual([true, reason]);
    if (state !== 'visible') expect(screen.getByTestId('target-source-reason')).toHaveTextContent(reason);
    expect(screen.getByTestId('target-comparison')).toBeInTheDocument();
  });

  it('shows the reference of a target imported where its source was not found, its source said missing', async () => {
    fakeApi({ [`GET ${url}`]: { json: { ...target('missing'), baseline_project_id: null, source: null } } });
    render(<SnapshotsSection map={sampleMap()} writing={writing} />);
    expect(await screen.findByTestId('target-comparison')).toBeInTheDocument();
    expect(screen.getByTestId('target-source-reason')).toHaveTextContent('Le projet source n’existe plus');
    expect(screen.getByTestId('target-refresh')).toBeDisabled();
  });
});
