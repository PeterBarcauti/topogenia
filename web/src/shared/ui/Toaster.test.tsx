// Toaster.test.tsx
// The notifications: stacked in the corner, state icon, close button, delays of 3.5 s and 7 s, roles alert and status (annex B 1.2, spec 9.5).

import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../i18n/i18n.ts';
import { Toaster } from './Toaster.tsx';
import { TOAST_DELAYS, createToastStore } from './toasts.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.useRealTimers();
});

describe('the store of the notifications', () => {
  it('stacks them, the oldest first, each with its identifier', () => {
    const store = createToastStore();
    const first = store.getState().notify('Carte alignée');
    const second = store.getState().notify('Refusé', 'error');
    expect(store.getState().toasts.map((toast) => [toast.id, toast.kind, toast.text])).toEqual([[first, 'info', 'Carte alignée'], [second, 'error', 'Refusé']]);
  });

  it('closes a notification after 3.5 s, an error after 7 s (B-1.2-17, B-1.2-18)', () => {
    vi.useFakeTimers();
    const store = createToastStore();
    store.getState().notify('Fiche validée', 'success');
    store.getState().notify('Information');
    store.getState().notify('Refusé', 'error');
    expect(TOAST_DELAYS).toEqual({ info: 3500, success: 3500, error: 7000 });
    vi.advanceTimersByTime(3499);
    expect(store.getState().toasts).toHaveLength(3);
    vi.advanceTimersByTime(1);
    expect(store.getState().toasts.map((toast) => toast.kind)).toEqual(['error']);
    vi.advanceTimersByTime(3499);
    expect(store.getState().toasts).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(store.getState().toasts).toEqual([]);
  });

  it('a notification closed by hand stops its timer and leaves the others', () => {
    vi.useFakeTimers();
    const store = createToastStore();
    const closed = store.getState().notify('Un');
    store.getState().notify('Deux');
    store.getState().dismiss(closed);
    store.getState().dismiss(closed);
    expect(store.getState().toasts.map((toast) => toast.text)).toEqual(['Deux']);
    vi.advanceTimersByTime(3500);
    expect(store.getState().toasts).toEqual([]);
  });
});

describe('a notification with a key', () => {
  it('replaces the one of the same key, in its place in the stack, and takes the text, the kind and the id of the new one', () => {
    const store = createToastStore();
    store.getState().notify('Avant');
    const first = store.getState().notify('Export en cours…', 'info', undefined, 'export');
    store.getState().notify('Après');
    const second = store.getState().notify('Export terminé', 'success', undefined, 'export');
    expect(second).not.toBe(first);
    expect(store.getState().toasts.map((toast) => [toast.text, toast.kind])).toEqual([['Avant', 'info'], ['Export terminé', 'success'], ['Après', 'info']]);
  });

  it('gives the replacement its own delay: the delay of the one replaced no longer closes it', () => {
    vi.useFakeTimers();
    const store = createToastStore();
    store.getState().notify('Export en cours…', 'info', undefined, 'export');
    vi.advanceTimersByTime(3000);
    store.getState().notify('Export terminé', 'success', undefined, 'export');
    vi.advanceTimersByTime(3499);
    expect(store.getState().toasts.map((toast) => toast.text)).toEqual(['Export terminé']);
    vi.advanceTimersByTime(1);
    expect(store.getState().toasts).toEqual([]);
  });

  it('leaves alone the notifications of another key and those without key', () => {
    const store = createToastStore();
    store.getState().notify('Un', 'info', undefined, 'export');
    store.getState().notify('Deux');
    store.getState().notify('Trois');
    store.getState().notify('Quatre', 'info', undefined, 'other');
    store.getState().notify('Cinq', 'info', undefined, 'export');
    expect(store.getState().toasts.map((toast) => toast.text)).toEqual(['Cinq', 'Deux', 'Trois', 'Quatre']);
  });

  it('shows one notification at a time for a key, the screen showing the one that replaced', () => {
    const store = createToastStore();
    render(<Toaster store={store} />);
    act(() => {
      store.getState().notify('Export en cours…', 'info', undefined, 'export');
    });
    act(() => {
      store.getState().notify('Export terminé', 'success', undefined, 'export');
    });
    expect(screen.queryByText('Export en cours…')).toBeNull();
    expect(screen.getByText('Export terminé')).toBeTruthy();
  });
});

describe('the stack shown (B-1.2-15, B-1.2-16, B-1.2-19)', () => {
  it('shows each notification with its state icon, an error as an alert, the others as a status', () => {
    const store = createToastStore();
    render(<Toaster store={store} />);
    act(() => {
      store.getState().notify('Carte alignée');
      store.getState().notify('Fiche validée', 'success');
      store.getState().notify('Modification refusée', 'error');
    });
    const region = screen.getByRole('region', { name: 'Notifications' });
    expect(within(region).getAllByRole('status').map((toast) => toast.textContent)).toEqual(['Carte alignée', 'Fiche validée']);
    expect(within(region).getByRole('alert')).toHaveTextContent('Modification refusée');
    for (const kind of ['info', 'success', 'error']) expect(screen.getByTestId(`toast-${kind}`).querySelector('.toast-icon svg')).not.toBeNull();
  });

  it('closes a notification by its close button, an icon named for assistive technologies', async () => {
    const store = createToastStore();
    render(<Toaster store={store} />);
    act(() => {
      store.getState().notify('Carte alignée');
    });
    const close = screen.getByRole('button', { name: 'Fermer la notification' });
    expect(close.querySelector('svg')).not.toBeNull();
    expect(close.textContent).toBe('');
    await userEvent.click(close);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(store.getState().toasts).toEqual([]);
  });

  it('runs the action offered, then closes the notification', async () => {
    const store = createToastStore();
    const undo = vi.fn();
    render(<Toaster store={store} />);
    act(() => {
      store.getState().notify('Carte alignée', 'info', { label: 'Annuler', run: undo });
    });
    await userEvent.click(screen.getByRole('button', { name: 'Annuler' }));
    expect(undo).toHaveBeenCalledTimes(1);
    expect(store.getState().toasts).toEqual([]);
  });

  it('disappears from the page when its delay runs out', () => {
    vi.useFakeTimers();
    const store = createToastStore();
    render(<Toaster store={store} />);
    act(() => {
      store.getState().notify('Refusé', 'error');
    });
    act(() => {
      vi.advanceTimersByTime(6999);
    });
    expect(screen.getByRole('alert')).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
