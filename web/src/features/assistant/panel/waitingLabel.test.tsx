// waitingLabel.test.tsx
// The label of the wait of an exchange with the assistant (lot J10): it says that the model works. Most connectors read the whole reply at once, so the panel shows this label during all the work of the model; the former « Lecture du projet… » made a long reply look like a slow reading by the application (trial of the project owner with a model that took six minutes, 13/11/2026).

import { render, screen } from '@testing-library/react';
import { beforeAll, describe, expect, it } from 'vitest';
import { BUNDLED_LANGUAGES, i18next, loadLanguage } from '../../../shared/i18n/i18n.ts';
import { ProgressCard } from './TurnCards.tsx';

beforeAll(async () => {
  await Promise.all(BUNDLED_LANGUAGES.map((language) => loadLanguage(language)));
});

const waiting = (language: string) => i18next.getFixedT(language)('assistant.panel.phases.waiting');

describe('the label shown while an exchange waits for the model', () => {
  it('says that the model works, in French and in English, and nothing of a reading of the project', () => {
    expect([waiting('fr'), waiting('en')]).toEqual(['Le modèle travaille…', 'The model is working…']);
  });

  it.each(BUNDLED_LANGUAGES)('is a label of its own in %s: written, and not the one of another phase', (language) => {
    const t = i18next.getFixedT(language);
    const others = ['thinking', 'writing', 'correcting'].map((phase) => t(`assistant.panel.phases.${phase}`));
    expect(waiting(language).trim()).not.toBe('');
    expect(others).not.toContain(waiting(language));
    // Each of the eighteen languages has its own: none shows the French one for lack of a text.
    if (language !== 'fr') expect(waiting(language)).not.toBe(waiting('fr'));
  });

  it('is what the panel shows of an exchange that waits, with the time it has been', async () => {
    await i18next.changeLanguage('fr');
    render(<ProgressCard turn={{ kind: 'progress', id: 1, phase: 'waiting', thought: null, characters: 0, elapsed: 366_000, destination: null }} />);
    expect(screen.getByTestId('assistant-progress')).toHaveTextContent('Le modèle travaille…');
    expect(screen.getByTestId('assistant-progress')).toHaveTextContent('366 s');
  });
});
