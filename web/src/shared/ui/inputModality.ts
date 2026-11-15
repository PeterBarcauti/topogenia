// inputModality.ts
// Whether the gesture of the moment came from the keyboard or from a pointer: a panel opened from the keyboard takes the focus, one opened by the mouse leaves it where it is (NF08).

let byKeys = false;

/**
 * A key that opens something: what it opens takes the focus. Told by the handlers of the one keyboard manager that open a
 * panel (Enter on a step, an entry of a menu), the manager alone listening to the keys of the window (B-8.1-08).
 */
export const noteKeyboard = (): void => {
  byKeys = true;
};

/** A press of the pointer, heard on the window in the capture phase before any handler: what opens next was asked by the mouse. */
if (typeof window !== 'undefined')
  for (const type of ['pointerdown', 'mousedown'])
    window.addEventListener(type, () => {
      byKeys = false;
    }, true);

/** Whether the gesture of the moment comes from the keyboard. */
export const fromKeyboard = (): boolean => byKeys;
