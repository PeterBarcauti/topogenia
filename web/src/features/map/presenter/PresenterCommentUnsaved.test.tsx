// PresenterCommentUnsaved.test.tsx
// The text typed in the field of the comment of presentation and not sent is declared as a form not validated, for « Quit » (R12): not once it is sent, given up or back as it was.

import { act, fireEvent, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { formsUnsaved } from '../../../shared/state/pendingWrites.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { closed, field, key, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('the comment of presentation typed and not sent (R12)', () => {
  it('declares nothing while the field is open and untouched, and a text typed as soon as it differs from the line shown', async () => {
    await onTheSecondStep(memoryComments([{ step_id: 's2', text: 'Ici on vérifie.', language: 'fr', source: 'manual', version: 1 }]));
    key('t');
    expect(formsUnsaved()).toBe(false);
    act(() => {
      fireEvent.change(field(), { target: { value: 'Ici on vérifie la demande.' } });
    });
    expect(formsUnsaved()).toBe(true);
    act(() => {
      fireEvent.change(field(), { target: { value: 'Ici on vérifie.' } });
    });
    expect(formsUnsaved()).toBe(false);
  });

  it('declares nothing for an empty field over a step without comment', async () => {
    await onTheSecondStep();
    key('t');
    act(() => {
      fireEvent.change(field(), { target: { value: 'x' } });
    });
    expect(formsUnsaved()).toBe(true);
    act(() => {
      fireEvent.change(field(), { target: { value: '' } });
    });
    expect(formsUnsaved()).toBe(false);
  });

  it('is lifted once the text is sent by Entrée, and once it is kept by Échap', async () => {
    await onTheSecondStep();
    key('t');
    typeIn('Envoyé.', 'Enter');
    await closed();
    await waitFor(() => {
      expect(formsUnsaved()).toBe(false);
    });
    key('t');
    typeIn('Gardé par Échap.', 'Escape');
    await closed();
    await waitFor(() => {
      expect(formsUnsaved()).toBe(false);
    });
  });
});
