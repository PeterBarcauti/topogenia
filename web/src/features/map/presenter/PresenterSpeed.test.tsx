// PresenterSpeed.test.tsx
// The speed of a presentation, as the presenter sees and hears it: the button of the bar goes up through the fine steps around the normal speed and comes back to the slowest after the fastest; plus and minus move it a step; the voice speaks at the speed shown (essai of the project owner, 19/11/2026: 0.5 too slow, 1.5 far too fast to understand).

import { act, fireEvent, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';
import { fakeSpeech } from '../../../test/speech.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const key = (name: string) => {
  act(() => {
    fireEvent.keyDown(window, { key: name });
  });
};

const shown = () => screen.getByTestId('presenter-speed').textContent;

const clickSpeed = () => {
  act(() => {
    screen.getByTestId('presenter-speed').click();
  });
};

/** V, then the voice of the computer when the stage asks which one. */
const voiceOn = () => {
  key('v');
  const local = screen.queryByTestId('presenter-voice-local');
  if (local !== null) {
    act(() => {
      local.click();
    });
  }
};

describe('the speed of a presentation', () => {
  it('goes up a step at each click of its button, from the normal speed to ×1,25, then back to ×0,75', async () => {
    await showEditor(sampleMap());
    key('F5');
    const seen = [shown()];
    for (let index = 0; index < 5; index++) {
      clickSpeed();
      seen.push(shown());
    }
    expect(seen).toEqual(['×1', '×1,1', '×1,25', '×0,75', '×0,9', '×1']);
  });

  it('moves a step with plus and minus, and the voice speaks at the speed shown', async () => {
    const spoken = fakeSpeech();
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    voiceOn();
    key('-');
    expect(shown()).toBe('×0,9');
    key(' ');
    key(' ');
    expect(spoken.at(-1)?.rate).toBe(0.9);
    key('+');
    key('+');
    expect(shown()).toBe('×1,1');
    key(' ');
    key(' ');
    expect(spoken.at(-1)?.rate).toBe(1.1);
  });
});
