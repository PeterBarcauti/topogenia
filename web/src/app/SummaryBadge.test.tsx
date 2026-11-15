// SummaryBadge.test.tsx
// The summary of the map beside its title (plan 3.1, lot R): its bubble at the opening, folded at the first gesture or after a few seconds, shown again on hover; the panel opened by a click; nothing for a reader of a map without summary.

import { act, fireEvent, render, screen } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import type { MapSummaryState } from '../shared/state/openProject.ts';
import { BUBBLE_MS, SummaryBadge } from './SummaryBadge.tsx';

const SENTENCE = 'Le commercial reçoit la demande, la vérifie et envoie le devis au client.';

const summary = (overrides: Partial<Schema<'MapSummaryView'>> = {}): Schema<'MapSummaryView'> => ({
  text: SENTENCE, source_language: 'fr', translations: { en: 'The clerk checks the request and sends the quote.' }, origin: 'ai', model: 'm',
  written_at: '2026-11-06T20:00:00Z', stale: false, version: 1, ...overrides,
});

function state(overrides: Partial<MapSummaryState> = {}): MapSummaryState {
  return { mapId: 'map-1', summary: summary(), canWrite: true, showOnOpen: true, open: vi.fn(), ...overrides };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.useRealTimers();
});

describe('the summary beside the title (plan 3.1, lot R)', () => {
  it('opens its bubble at the opening of the map, folds it at the first pointer pressed, shows it again on hover', () => {
    render(<SummaryBadge state={state()} language="fr" />);
    expect(screen.getByTestId('summary-bubble')).toHaveTextContent(SENTENCE);
    fireEvent.pointerDown(document.body);
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
    fireEvent.mouseEnter(screen.getByTestId('summary-info'));
    expect(screen.getByTestId('summary-bubble')).toHaveTextContent(SENTENCE);
    fireEvent.mouseLeave(screen.getByTestId('summary-info'));
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
  });

  it('folds its bubble by itself after a few seconds', () => {
    vi.useFakeTimers();
    const { unmount } = render(<SummaryBadge state={state()} language="fr" />);
    act(() => {
      vi.advanceTimersByTime(BUBBLE_MS - 1);
    });
    expect(screen.getByTestId('summary-bubble')).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
    unmount();
  });

  it('keeps its bubble closed when the preference says so, shows the sentence in the language shown and says a map changed since', () => {
    render(<SummaryBadge state={state({ showOnOpen: false, summary: summary({ stale: true }) })} language="en" />);
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
    fireEvent.focus(screen.getByTestId('summary-info'));
    expect(screen.getByTestId('summary-bubble')).toHaveTextContent('The clerk checks the request and sends the quote.');
    expect(screen.getByTestId('summary-bubble')).toHaveTextContent(i18next.t('summary.stale_short'));
    expect(screen.getByTestId('summary-info')).toHaveClass('summary-info--stale');
  });

  it('opens the panel of the summary on a click, its bubble folded', () => {
    const shown = state();
    render(<SummaryBadge state={shown} language="fr" />);
    fireEvent.click(screen.getByTestId('summary-info'));
    expect(shown.open).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
  });

  it('shows to a facilitator the icon of a map without summary, which says where to ask one, and nothing to a reader', () => {
    const { unmount } = render(<SummaryBadge state={state({ summary: null })} language="fr" />);
    expect(screen.getByTestId('summary-info')).toHaveAccessibleName(i18next.t('summary.none_help'));
    expect(screen.queryByTestId('summary-bubble')).toBeNull();
    unmount();
    render(<SummaryBadge state={state({ summary: null, canWrite: false })} language="fr" />);
    expect(screen.queryByTestId('summary-info')).toBeNull();
  });
});
