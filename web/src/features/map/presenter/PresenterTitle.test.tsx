// PresenterTitle.test.tsx
// Where a presentation stands (essai J10): the name of the process on the main map; in a detail the step it details, the levels above it before it and the name of the process left out; each in the language shown.

import { act, fireEvent, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const present = () => {
  act(() => {
    fireEvent.keyDown(window, { key: 'F5' });
  });
};

const levels = () => [...screen.getByTestId('presenter-title').querySelectorAll('.presenter-title-level')].map((level) => [level.textContent, level.classList.contains('is-current')]);

const crumb = (id: string, name: string, translations: Record<string, string> = {}) => ({ map_id: id, name, source_language: 'fr', translations });

describe('the title of a presentation', () => {
  it('names the process on its main map, as soon as the presentation opens', async () => {
    await showEditor(sampleMap());
    expect(screen.queryByTestId('presenter-title')).toBeNull();
    present();
    expect(levels()).toEqual([['Visites', true]]);
  });

  it('names in a detail the step it details, the levels above it before it, and leaves the name of the process out', async () => {
    const breadcrumb = [crumb('root', 'Visites'), crumb('middle', 'Préparer la visite'), crumb(MAP_ID, 'Réserver la salle')];
    await showEditor(sampleMap({ depth: 3, breadcrumb }));
    present();
    expect(levels()).toEqual([['Préparer la visite', false], ['Réserver la salle', true]]);
    expect(screen.getByTestId('presenter-title').textContent).toBe('Préparer la visite›Réserver la salle');
  });

  it('is written in the language shown, a level not translated left as it was entered', async () => {
    const breadcrumb = [crumb('root', 'Visites', { en: 'Visits' }), crumb('middle', 'Préparer la visite', { en: 'Prepare the visit' }), crumb(MAP_ID, 'Réserver la salle')];
    const { editor } = await showEditor(sampleMap({ depth: 3, breadcrumb }));
    act(() => {
      editor.store.getState().show({ language: 'en', origin: 'fr', locked: true });
    });
    present();
    expect(levels()).toEqual([['Prepare the visit', false], ['Réserver la salle', true]]);
    expect(screen.getByTestId('presenter-title').getAttribute('lang')).toBe('en');
  });
});
