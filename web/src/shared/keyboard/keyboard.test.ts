// keyboard.test.ts
// The one keyboard manager: chords, the layers in their order of priority, the global chords, the capture before the canvas (S-3.5-02, annex B 2.5).

import { chordOf, createKeyboard, isInputField, type KeyHandler } from './keyboard.ts';

/** A key pressed (or released) on an element of the page, dispatched as the window would. */
function press(key: string, options: KeyboardEventInit & { on?: Element; type?: 'keydown' | 'keyup' } = {}): KeyboardEvent {
  const { on = document.body, type = 'keydown', ...init } = options;
  const event = new KeyboardEvent(type, { key, bubbles: true, cancelable: true, ...init });
  on.dispatchEvent(event);
  return event;
}

const using = (result: boolean) => vi.fn<KeyHandler>(() => result);

let detach: () => void = () => undefined;
afterEach(() => {
  detach();
  document.body.innerHTML = '';
});

function attached() {
  const keyboard = createKeyboard();
  detach = keyboard.attach(window);
  return keyboard;
}

describe('chords', () => {
  it('write the modifiers in a fixed order and a letter in capitals', () => {
    expect(chordOf({ key: 'z', ctrlKey: true, metaKey: false, altKey: false, shiftKey: true })).toBe('Ctrl+Shift+Z');
    expect(chordOf({ key: 'F2', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false })).toBe('F2');
    expect(chordOf({ key: 'k', ctrlKey: false, metaKey: false, altKey: true, shiftKey: false })).toBe('Alt+K');
    expect(chordOf({ key: ' ', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false })).toBe('Space');
    expect(chordOf({ key: 'Esc', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false })).toBe('Escape');
  });

  it('take metaKey as Ctrl, for a Mac (B-2.5-35)', () => {
    expect(chordOf({ key: 's', ctrlKey: false, metaKey: true, altKey: false, shiftKey: false })).toBe('Ctrl+S');
  });

  it('are none for a modifier pressed alone', () => {
    for (const key of ['Control', 'Shift', 'Alt', 'Meta'])
      expect(chordOf({ key, ctrlKey: key === 'Control', metaKey: false, altKey: false, shiftKey: key === 'Shift' })).toBeNull();
  });
});

describe('the input fields (B-2.5-02)', () => {
  it('are the text fields, areas, lists and editable content', () => {
    const editable = document.createElement('div');
    editable.setAttribute('contenteditable', 'true');
    for (const tag of ['input', 'textarea', 'select']) expect(isInputField(document.createElement(tag))).toBe(true);
    const inside = editable.appendChild(document.createElement('span'));
    expect([isInputField(editable), isInputField(inside)]).toEqual([true, true]);
    expect(isInputField(document.createElement('button'))).toBe(false);
    expect(isInputField(null)).toBe(false);
  });
});

describe('the layers (S-3.5-02, B-2.5-01 to B-2.5-04)', () => {
  it('one listener on the window hands a key to the canvas, which stops it there', () => {
    const keyboard = attached();
    const canvas = using(true);
    keyboard.add('canvas', canvas);
    const event = press('Delete');
    expect(canvas).toHaveBeenCalledWith(event, 'Delete');
    expect(event.defaultPrevented).toBe(true);
  });

  it('a key typed in an input field reaches no layer (B-2.5-02)', () => {
    const keyboard = attached();
    const canvas = using(true);
    keyboard.add('canvas', canvas);
    const field = document.body.appendChild(document.createElement('input'));
    const event = press('Delete', { on: field });
    expect(canvas).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
  });

  it('a modal element takes every key: the panel and the canvas hear nothing (B-2.5-03)', () => {
    const keyboard = attached();
    const [panel, canvas, modal] = [using(true), using(true), using(false)];
    keyboard.add('canvas', canvas);
    keyboard.add('panel', panel);
    const close = keyboard.add('modal', modal);
    const event = press('z', { ctrlKey: true });
    expect(modal).toHaveBeenCalledTimes(1);
    expect(panel).not.toHaveBeenCalled();
    expect(canvas).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
    close();
    press('z', { ctrlKey: true });
    expect(panel).toHaveBeenCalledTimes(1);
  });

  it('only the last modal element opened is asked', () => {
    const keyboard = attached();
    const [below, above] = [using(true), using(true)];
    keyboard.add('modal', below);
    keyboard.add('modal', above);
    press('Escape');
    expect(above).toHaveBeenCalledTimes(1);
    expect(below).not.toHaveBeenCalled();
  });

  it('a non-modal panel leaves the canvas its shortcuts, and is asked first (B-2.5-04)', () => {
    const keyboard = attached();
    const order: string[] = [];
    const heard = (name: string, used: boolean): KeyHandler => () => {
      order.push(name);
      return used;
    };
    keyboard.add('canvas', heard('canvas', true));
    keyboard.add('panel', heard('panel', false));
    press('a', { ctrlKey: true });
    expect(order).toEqual(['panel', 'canvas']);
    keyboard.add('panel', heard('panel used', true));
    press('a', { ctrlKey: true });
    expect(order).toEqual(['panel', 'canvas', 'panel used']);
  });

});

describe('the listener on the window (B-2.5-01, B-2.5-21)', () => {
  it('a key no layer uses keeps its default action', () => {
    const keyboard = attached();
    keyboard.add('canvas', using(false));
    expect(press('p', { ctrlKey: true }).defaultPrevented).toBe(false);
  });

  it('listens in the capture phase: a key used never reaches the listeners of the canvas (B-2.5-21)', () => {
    const keyboard = attached();
    keyboard.add('canvas', (_, chord) => chord === 'ArrowLeft');
    const flow = document.body.appendChild(document.createElement('div'));
    const below = vi.fn();
    flow.addEventListener('keydown', below);
    press('ArrowLeft', { on: flow });
    press('ArrowUp', { on: flow });
    expect(below.mock.calls.map(([event]) => (event as KeyboardEvent).key)).toEqual(['ArrowUp']);
  });

  it('hands the released keys too (Space held for the hand, B-2.5-17), and stops listening once detached', () => {
    const keyboard = createKeyboard();
    const stop = keyboard.attach(window);
    const canvas = using(true);
    keyboard.add('canvas', canvas);
    press(' ', { type: 'keyup' });
    expect(canvas.mock.calls[0]?.[0].type).toBe('keyup');
    stop();
    press(' ');
    expect(canvas).toHaveBeenCalledTimes(1);
  });
});

describe('the global chords (B-2.5-05)', () => {
  it('are heard in an input field and under a modal element, without the default action of the browser', () => {
    const keyboard = attached();
    const save = vi.fn();
    keyboard.addGlobal('Ctrl+S', save);
    keyboard.add('modal', using(true));
    const field = document.body.appendChild(document.createElement('textarea'));
    const inField = press('s', { ctrlKey: true, on: field });
    const onPage = press('s', { metaKey: true });
    expect(save).toHaveBeenCalledTimes(2);
    expect([inField.defaultPrevented, onPage.defaultPrevented]).toEqual([true, true]);
  });

  it('run once per key pressed, not when it is released, and no more once removed', () => {
    const keyboard = attached();
    const save = vi.fn();
    const remove = keyboard.addGlobal('Ctrl+S', save);
    press('s', { ctrlKey: true, type: 'keyup' });
    expect(save).not.toHaveBeenCalled();
    remove();
    expect(press('s', { ctrlKey: true }).defaultPrevented).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
