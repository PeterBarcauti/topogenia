// unsavedDialogs.test.tsx
// The modal dialogs that write what is typed (properties of the project, manual translations, summary of the map, settings of the assistant) declare a change not saved for « Quit » (R12): modified and not saved, yes; back as it opened, saved or closed, no.

import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState, type ReactNode } from 'react';
import type { Schema } from '../api/client.ts';
import { AssistantSettingsDialog } from '../features/assistant/AssistantSettingsDialog.tsx';
import { ProjectPropertiesDialog } from '../features/project/properties/ProjectPropertiesDialog.tsx';
import { ManualTranslationsDialog } from '../features/translations/ManualTranslationsDialog.tsx';
import { i18next } from '../shared/i18n/i18n.ts';
import { KeyboardProvider } from '../shared/keyboard/KeyboardContext.tsx';
import { formsUnsaved } from '../shared/state/pendingWrites.ts';
import { showEditor } from '../test/editor.tsx';
import { fakeApi } from '../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, replyOf, sampleMap } from '../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** A dialog that the page closes, as the real ones are: the dialog gone when it asks to be closed. */
function Closable({ children }: { children: (onClose: () => void) => ReactNode }) {
  const [open, setOpen] = useState(true);
  return <KeyboardProvider>{open ? children(() => { setOpen(false); }) : <p data-testid="gone" />}</KeyboardProvider>;
}

describe('the manual translations of a name', () => {
  const named = { name: 'Visites', sourceLanguage: 'fr', translations: {}, max: 100 };
  const open = () => render(
    <Closable>{(onClose) => <ManualTranslationsDialog named={named} languages={['fr', 'en']} onSave={() => Promise.resolve(true)} onClose={onClose} />}</Closable>,
  );

  it('declares a translation typed, lifts it once it is back as it was, and once the dialog is cancelled', async () => {
    open();
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(screen.getByTestId('manual-translations-en'), 'Visits');
    expect(formsUnsaved()).toBe(true);
    await userEvent.clear(screen.getByTestId('manual-translations-en'));
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(screen.getByTestId('manual-translations-en'), 'Visits');
    await userEvent.click(screen.getByTestId('manual-translations-cancel'));
    expect(formsUnsaved()).toBe(false);
  });

  it('lifts it once the translation is saved and the dialog closed', async () => {
    open();
    await userEvent.type(screen.getByTestId('manual-translations-en'), 'Visits');
    await userEvent.click(screen.getByTestId('manual-translations-save'));
    expect(await screen.findByTestId('gone')).toBeInTheDocument();
    expect(formsUnsaved()).toBe(false);
  });
});

describe('the properties of the project', () => {
  const detail = {
    id: PROJECT_ID, code: 'VIS', name: 'Visites', source_language: 'fr', name_translations: {}, description: null, shared_folder: null, classification: 'internal',
    sharing_group_label: null, translation_glossary: null, root_map_id: MAP_ID, role: 'facilitator', members: [], version: 4, workday_hours: 7, fte_hours_per_month: 151.67,
  };
  const dialog = () => render(
    <Closable>{(onClose) => <ProjectPropertiesDialog projectId={PROJECT_ID} code="VIS" onClose={onClose} onDetail={() => undefined} />}</Closable>,
  );

  it('declares a field changed, lifts it once it is back as it was, and once the dialog is closed', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: detail } });
    dialog();
    const label = await screen.findByTestId('project-properties-sharing-group');
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(label, 'Groupe');
    expect(formsUnsaved()).toBe(true);
    await userEvent.clear(label);
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(label, 'Groupe');
    await userEvent.click(screen.getByTestId('project-properties-cancel'));
    expect(formsUnsaved()).toBe(false);
  });

  it('declares nothing for a member who only reads', async () => {
    fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { ...detail, role: 'reader' } } });
    dialog();
    await screen.findByTestId('project-properties-read-only');
    expect(formsUnsaved()).toBe(false);
  });
});

describe('the settings of the assistant', () => {
  const settings = {
    connector: 'none', model: null, region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'], has_secret: false,
    secret_hint: null, version: 0, updated_at: null, connectors: ['none', 'ollama', 'anthropic'],
  };

  it('declares a connector chosen, lifts it once the first one is chosen again, and once the dialog is cancelled', async () => {
    fakeApi({ 'GET /api/admin/assistant': { json: settings } });
    render(<Closable>{(onClose) => <AssistantSettingsDialog onClose={onClose} />}</Closable>);
    await screen.findByTestId('assistant-connector');
    expect(formsUnsaved()).toBe(false);
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(formsUnsaved()).toBe(true);
    await userEvent.click(screen.getByTestId('assistant-connector-none'));
    expect(formsUnsaved()).toBe(false);
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    await userEvent.click(screen.getByTestId('assistant-cancel'));
    expect(formsUnsaved()).toBe(false);
  });
});

describe('the summary of the map', () => {
  const summary: Schema<'MapSummaryView'> = {
    text: 'Le commercial envoie le devis.', source_language: 'fr', translations: {}, origin: 'ai', model: 'claude-opus-5-5', written_at: '2026-11-06T20:00:00Z', stale: false, version: 4,
  };

  async function openPanel(map: Schema<'MapView'>) {
    const shown = await showEditor(map);
    act(() => {
      shown.editor.floating.getState().open({ kind: 'summary' });
    });
    await screen.findByRole('dialog', { name: i18next.t('summary.title') });
    return shown;
  }

  it('declares the sentence changed, lifts it once it is back as it was, and once it is saved', async () => {
    const written = sampleMap({ summary: { ...summary, text: 'Nouveau.', origin: 'manual', model: null, version: 5 } });
    fakeApi({ [`PUT /api/maps/${MAP_ID}/summaries`]: { json: replyOf(written) } });
    await openPanel(sampleMap({ summary }));
    await userEvent.click(screen.getByTestId('summary-edit'));
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(screen.getByTestId('summary-draft'), ' Plus.');
    expect(formsUnsaved()).toBe(true);
    await userEvent.clear(screen.getByTestId('summary-draft'));
    await userEvent.type(screen.getByTestId('summary-draft'), summary.text);
    expect(formsUnsaved()).toBe(false);
    await userEvent.clear(screen.getByTestId('summary-draft'));
    await userEvent.type(screen.getByTestId('summary-draft'), 'Nouveau.');
    expect(formsUnsaved()).toBe(true);
    await userEvent.click(screen.getByTestId('summary-save'));
    expect(await screen.findByTestId('summary-text')).toHaveTextContent('Nouveau.');
    expect(formsUnsaved()).toBe(false);
  });

  it('lifts it once the panel is closed with a sentence typed, and declares nothing for the field offered empty to a map without summary', async () => {
    await openPanel(sampleMap({ summary: null }));
    expect(formsUnsaved()).toBe(false);
    await userEvent.type(screen.getByTestId('summary-draft'), 'Une phrase.');
    expect(formsUnsaved()).toBe(true);
    await userEvent.click(screen.getByTestId('summary-done'));
    expect(formsUnsaved()).toBe(false);
  });
});
