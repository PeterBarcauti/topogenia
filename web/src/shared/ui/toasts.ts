// toasts.ts
// The notifications of the application: a stack in the corner, each closing by itself after its delay (annex B 1.2, spec 9.5).

import { createStore, type StoreApi } from 'zustand/vanilla';

export type ToastKind = 'info' | 'success' | 'error';

/** An action offered by a notification (Undo after an alignment, spec 9.5): its label, already translated, and what it does. */
export interface ToastAction {
  readonly label: string;
  readonly run: () => void;
}

export interface Toast {
  readonly id: number;
  readonly kind: ToastKind;
  readonly text: string;
  readonly action: ToastAction | undefined;
}

/** How long a notification stays: 3.5 s, 7 s for an error (B-1.2-17, B-1.2-18). */
export const TOAST_DELAYS: Readonly<Record<ToastKind, number>> = { info: 3500, success: 3500, error: 7000 };

/** What a screen is given to notify: a text and its kind, the notification itself kept by the store (B-8.1-27). */
export type Notifier = (text: string, kind?: ToastKind) => void;

export interface ToastState {
  /** The notifications shown, the oldest first. */
  readonly toasts: readonly Toast[];
  /** Shows a notification; gives its identifier. */
  readonly notify: (text: string, kind?: ToastKind, action?: ToastAction) => number;
  /** Closes a notification (its close button, its delay, its action); nothing when it is already gone. */
  readonly dismiss: (id: number) => void;
}

/** A stack of notifications; the timers are those of the window (the tests replace them with fake ones). */
export function createToastStore(): StoreApi<ToastState> {
  let next = 0;
  const timers = new Map<number, ReturnType<typeof setTimeout>>();
  return createStore<ToastState>()((set, get) => ({
    toasts: [],
    notify(text, kind = 'info', action) {
      const id = ++next;
      set({ toasts: [...get().toasts, { id, kind, text, action }] });
      timers.set(id, setTimeout(() => {
        get().dismiss(id);
      }, TOAST_DELAYS[kind]));
      return id;
    },
    dismiss(id) {
      clearTimeout(timers.get(id));
      timers.delete(id);
      if (get().toasts.some((toast) => toast.id === id)) set({ toasts: get().toasts.filter((toast) => toast.id !== id) });
    },
  }));
}

/** The notifications of the application, shown by the shell and fed by every screen. */
export const toasts = createToastStore();

/** Shows a notification of the application (B-1.2-20: the one stack the shell shows is the one the screens feed). */
export const notify = (text: string, kind?: ToastKind, action?: ToastAction): number => toasts.getState().notify(text, kind, action);
