// menuLook.test.tsx
// The context menus as the validated mock-up draws them: the type and the colour of a lane in two named groups, swatches in a grid, one choice checked a group; an icon on each entry of a step (spec 9.5, ARIA 1.2, mock-up 05-menus).

import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';
import { setClipboard } from '../state/clipboard.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

/** The menu of the lane VTE-C02 (« Service », Lavande), opened by its « ⋯ ». */
async function laneMenu(): Promise<HTMLElement> {
  await showEditor(sampleMap());
  await userEvent.click(screen.getByTestId('lane-more-VTE-C02'));
  return screen.getByRole('menu', { name: 'Menu du couloir' });
}

describe('the menu of a lane (mock-up 05-menus, ARIA 1.2)', () => {
  it('its type in a group titled « Type »: an icon, the name of the type, a radio on the right, one checked', async () => {
    const group = within(await laneMenu()).getByRole('group', { name: 'Type' });
    const radios = within(group).getAllByRole('menuitemradio');
    expect(radios.map((radio) => radio.querySelector('.menu-label')?.textContent)).toEqual(['Personne ou rôle', 'Service', 'Système']);
    expect(radios.map((radio) => [radio.firstElementChild?.tagName.toLowerCase(), radio.lastElementChild?.querySelector('.menu-check') !== null])).toEqual(Array(3).fill(['svg', true]));
    expect(radios.filter((radio) => radio.getAttribute('aria-checked') === 'true').map((radio) => radio.getAttribute('aria-label'))).toEqual(['Type : Service']);
  });

  it('its colour in a group titled « Couleur »: eight swatches in a grid, the one chosen ticked, its name and its rank under them', async () => {
    const group = within(await laneMenu()).getByRole('group', { name: 'Couleur' });
    const swatches = within(group).getAllByRole('menuitemradio');
    expect([swatches.length, swatches.every((swatch) => swatch.parentElement?.classList.contains('menu-swatches') === true)]).toEqual([8, true]);
    expect(swatches.filter((swatch) => swatch.getAttribute('aria-checked') === 'true').map((swatch) => swatch.getAttribute('aria-label'))).toEqual(['Couleur : Lavande']);
    expect(swatches.map((swatch) => swatch.querySelector('svg') !== null)).toEqual([false, true, false, false, false, false, false, false]);
    expect(group.querySelector('.menu-swatch-name')?.textContent).toBe('Lavandeteinte 2 sur 8');
    expect(swatches[3]?.querySelector('.menu-swatch')?.getAttribute('style')).toContain('--ink: var(--lane-4-ink)');
  });

  it('one choice checked in each group, never two in one set; the arrows go through the swatches, left and right among them', async () => {
    const menu = await laneMenu();
    const groups = within(menu).getAllByRole('group');
    expect(groups.map((group) => within(group).getAllByRole('menuitemradio').filter((radio) => radio.getAttribute('aria-checked') === 'true').length)).toEqual([1, 1]);
    expect(within(menu).getAllByRole('menuitemradio').filter((radio) => radio.closest('[role="group"]') === null)).toEqual([]);
    const swatches = within(groups[1] as HTMLElement).getAllByRole('menuitemradio');
    within(groups[0] as HTMLElement).getByRole('menuitemradio', { name: 'Type : Système' }).focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(swatches[0]);
    await userEvent.keyboard('{ArrowRight}{ArrowRight}{ArrowLeft}');
    expect(document.activeElement).toBe(swatches[1]);
  });
});

describe('the menu of a step (mock-up 05-menus)', () => {
  it('an icon before each action, the seven types in a group named « Type de l’étape », a radio before them and their shape after', async () => {
    const { canvas } = await showEditor(sampleMap());
    fireEvent.contextMenu(canvas.querySelector('.react-flow__node[data-id="s2"]') as HTMLElement, { clientX: 300, clientY: 50 });
    const menu = screen.getByRole('menu', { name: 'Menu de l’étape' });
    const actions = within(menu).getAllByRole('menuitem');
    expect(actions.filter((item) => item.firstElementChild?.tagName.toLowerCase() !== 'svg').map((item) => item.getAttribute('data-testid'))).toEqual([]);
    const types = within(within(menu).getByRole('group', { name: 'Type de l’étape' })).getAllByRole('menuitemradio');
    expect(types.map((item) => [item.firstElementChild?.classList.contains('menu-check'), item.lastElementChild?.querySelector('svg') !== null])).toEqual(Array(7).fill([true, true]));
    expect(types.filter((item) => item.getAttribute('aria-checked') === 'true').map((item) => item.textContent)).toEqual(['Changer le type : Étape']);
  });

  it('an icon before each action of the other menus too: a detailed step, a selection, the background, a flow', async () => {
    const map = sampleMap();
    const { canvas, editor } = await showEditor({ ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) });
    const node = (id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;
    const withoutIcon = () => within(screen.getByRole('menu')).getAllByRole('menuitem').filter((item) => item.firstElementChild?.tagName.toLowerCase() !== 'svg').map((item) => item.getAttribute('data-testid'));
    const opened: (string | null)[][] = [];
    for (const open of [
      () => fireEvent.contextMenu(node('s2')),
      () => fireEvent.contextMenu(canvas.querySelector('.react-flow__pane') as HTMLElement),
      () => fireEvent.contextMenu(within(canvas).getByTestId('rf__edge-f1')),
      () => {
        editor.store.setState({ selection: { steps: ['s2', 's3'], flows: [] } });
        fireEvent.contextMenu(node('s3'));
      },
    ]) {
      open();
      opened.push(withoutIcon());
      await userEvent.keyboard('{Escape}');
    }
    expect(opened).toEqual([[], [], [], []]);
  });
});
