// DocumentFolder.test.tsx
// The desktop mode read with the profile reaches the sheet of a step: there the line of a document offers to show its folder; on the server mode it offers the copy alone (ADR 104).

import { fireEvent, render, screen } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import { openSheetOf } from '../test/editor.tsx';
import { fakeApi } from '../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../test/maps.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { documentOf, inputOf, sheetViewOf } from '../test/sheets.ts';
import { App } from './App.tsx';

const onShare = inputOf(documentOf('e1', { direction: 'input', step_id: 's2', name: 'Programme des visites', location: '\\\\serveur\\partage\\visites\\programme.xlsx', location_type: 'path' }),
  { producer: null, near: false, used: true });

/** The application on the sample map, the sheet of its step s2 opened as the user opens it. */
async function sheetOf(profile: Partial<Schema<'ProfileView'>>): Promise<void> {
  window.history.replaceState(null, '', `/maps/${MAP_ID}`);
  fakeApi({ [`GET /api/maps/${MAP_ID}`]: { json: { map: sampleMap(), history: { undo: [], redo: [] } } }, 'GET /api/steps/s2/sheet': { json: sheetViewOf({ inputs: [onShare] }) } });
  render(<App source={fakeSource([profileOf(profile)]).source} />);
  openSheetOf(await screen.findByTestId('map-canvas'), 's2');
  // A sheet opens folded (essai J10): its inputs are unfolded as the user unfolds them.
  fireEvent.click(await screen.findByTestId('sheet-fold-inputs'));
  await screen.findByTestId('input-e1');
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  window.history.replaceState(null, '', '/');
});

describe('the folder of a document, from the profile to the sheet (ADR 104)', () => {
  it('is offered where the application runs on the computer of the user', async () => {
    await sheetOf({ desktop: true });
    expect(screen.getByRole('button', { name: 'Ouvrir le dossier de « Programme des visites »' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Copier le chemin de « Programme des visites »' })).toBeEnabled();
  });

  it('is not offered on a server, where the path is still copied', async () => {
    await sheetOf({ desktop: false });
    expect(screen.queryByTestId('document-folder-e1')).toBeNull();
    expect(screen.getByRole('button', { name: 'Copier le chemin de « Programme des visites »' })).toBeEnabled();
  });
});
