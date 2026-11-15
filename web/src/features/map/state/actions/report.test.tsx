// report.test.tsx
// The report told by the AI from the map (plan 3.1, L4): the command opens the panel of the assistant and sends its practice with its fixed request; the report saved is a Markdown file named after the map, its classification and the model that wrote it in its header.

import { act } from '@testing-library/react';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { captureDownloads } from '../../../../test/downloads.ts';
import { showEditor } from '../../../../test/editor.tsx';
import { fakeApi } from '../../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../../test/maps.ts';
import { sampleReport } from '../../../../test/reports.ts';
import { askReport, saveReport } from './exports.ts';

let downloads: ReturnType<typeof captureDownloads>;

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  downloads = captureDownloads();
});

afterEach(() => {
  downloads.restore();
  vi.unstubAllGlobals();
});

describe('the report told by the AI (plan 3.1, L4)', () => {
  it('« Rapport raconté par l’IA » opens the panel of the assistant and sends its practice with its fixed request', async () => {
    fakeApi({});
    const { editor } = await showEditor(sampleMap());
    const send = vi.fn(() => Promise.resolve());
    const loadStatus = vi.fn(() => Promise.resolve());
    editor.assistant.setState({ send, loadStatus, status: null });
    await act(() => askReport(editor));
    // The state is read before the request (the panel reads it too when it opens), as for a translation.
    expect([editor.assistant.getState().open, loadStatus.mock.calls.length > 0]).toEqual([true, true]);
    expect(send).toHaveBeenCalledWith('Rédige le rapport de la carte.', 'report', []);
  });

  it('saves the report as a Markdown file named after the map, with its classification and the model that wrote it', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 1 } } });
    const { editor, notes } = await showEditor(sampleMap());
    await act(() => saveReport(editor, sampleReport(), 'claude-opus-5-5'));
    const [file] = downloads.files;
    expect([file?.name.startsWith('VIS_'), file?.name.includes('Rapport'), file?.name.endsWith('.md'), file?.content.type]).toEqual([true, true, true, 'text/markdown;charset=utf-8']);
    const text = new TextDecoder().decode(await file?.content.arrayBuffer());
    expect(text).toContain('> Rédigé avec l’IA (claude-opus-5-5) : à relire avant de le diffuser.');
    expect(text).toContain('Classification : Interne');
    expect(text).toContain('## Ce qui coince');
    expect(notes.at(-1)).toEqual(['Export terminé : le fichier est dans le dossier Téléchargements.', 'success']);
  });
});
