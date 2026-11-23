// fit.test.tsx
// « Show all » of the map: its button next to the zoom shows the whole map, its title gives Shift+1; the map opens at its start as before; with one step alone chosen, Shift+1 begins its name (B-7-09, AR-151, lot 1 of the 3.4).

import { act, fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/** The move and the zoom of the canvas shown, as React Flow writes them. */
const viewportOf = (): string => document.querySelector<HTMLElement>('.react-flow__viewport')?.style.transform ?? '';

/** A map of 21 columns, 4 620 px wide: more than the 1 000 px of the screen of the tests. */
const wide = () => mapOf({ lanes: [laneOf('l1', 0)], steps: [stepOf('s1', 'l1', 0), stepOf('s2', 'l1', 20)] });

describe('the button Show all', () => {
  it('shows the whole map, its title gives Maj+1', async () => {
    await showEditor(wide());
    const button = screen.getByTestId('map-fit');
    expect(button).toHaveAttribute('title', 'Tout voir (Maj+1)');
    expect(button).toHaveAccessibleName('Tout voir (Maj+1)');
    fireEvent.click(button);
    const scale = Number(/scale\(([\d.]+)\)$/.exec(viewportOf())?.[1]);
    expect(scale).toBeLessThan(0.25);
    expect(scale).toBeGreaterThanOrEqual(0.2);
    expect(viewportOf()).toMatch(/^translate\(192px,\s?16px\)/);
  });

  it('never enlarges a map: the 1 000 px of the window hold the sample map, so it is shown at less than 100 %, from its start', async () => {
    await showEditor(sampleMap());
    fireEvent.click(screen.getByTestId('map-fit'));
    const scale = Number(/scale\(([\d.]+)\)$/.exec(viewportOf())?.[1]);
    expect(scale).toBeLessThanOrEqual(1);
    expect(viewportOf()).toMatch(/^translate\(192px,\s?16px\)/);
  });
});

describe('the opening of the map', () => {
  it('stays at its start as before (B-7-09)', async () => {
    await showEditor(wide());
    expect(viewportOf()).toBe('translate(192px,16px) scale(1)');
  });
});

describe('Shift+1 with one step alone chosen (AR-151)', () => {
  it('begins its name, as any character typed', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    const step = canvas.querySelector('.react-flow__node[data-id="s2"]') as HTMLElement;
    fireEvent.click(step);
    act(() => {
      step.focus();
    });
    const before = viewportOf();
    await userEvent.keyboard('{Shift>}1{/Shift}');
    expect(editor.store.getState().editing).toBe('s2');
    expect(screen.getByTestId<HTMLTextAreaElement>('step-label-input').value).toBe('1');
    expect(viewportOf()).toBe(before);
  });
});
