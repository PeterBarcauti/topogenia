// translationsButton.test.tsx
// The application gives the map the languages read with the profile: the button Translations of the header appears on a map when another language is configured (B-5-01, AR-29).

import { act, render, screen } from '@testing-library/react';
import { i18next } from '../shared/i18n/i18n.ts';
import { openProject } from '../shared/state/openProject.ts';
import { fakeApi } from '../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../test/maps.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';

function showMapAs(languages: string[]) {
  fakeApi({ [`GET /api/maps/${MAP_ID}`]: { json: { map: sampleMap(), history: { undo: [], redo: [] } } } });
  window.history.replaceState(null, '', `/maps/${MAP_ID}`);
  render(<App source={fakeSource([profileOf({ languages })]).source} />);
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    openProject.setState({ project: null });
  });
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the languages of the configuration reach the map (B-5-01, AR-29)', () => {
  it('two languages: the facilitator finds the button Translations in the header of the map', async () => {
    showMapAs(['fr', 'en']);
    await screen.findByTestId('map-canvas');
    expect(screen.getByTestId('header-translations')).toHaveTextContent('Traductions');
  });

  it('one language alone: no button, there is nothing to translate', async () => {
    showMapAs(['fr']);
    await screen.findByTestId('map-canvas');
    expect(screen.getByTestId('header-file')).toBeInTheDocument();
    expect(screen.queryByTestId('header-translations')).not.toBeInTheDocument();
  });
});
