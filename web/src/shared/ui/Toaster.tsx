// Toaster.tsx
// The notifications of the application in the bottom right corner, stacked, with their state icon (annex B 1.2, spec 9.5).

import { Check, Info, X, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { StoreApi } from 'zustand/vanilla';
import { Icon } from './Icon.tsx';
import { toasts, type Toast, type ToastKind, type ToastState } from './toasts.ts';

const ICONS: Readonly<Record<ToastKind, LucideIcon>> = { info: Info, success: Check, error: X };

function ToastView({ toast, onClose }: { toast: Toast; onClose: () => void }) {
  const { t } = useTranslation();
  const { action } = toast;
  const runAction = () => {
    action?.run();
    onClose();
  };
  return (
    <div className={`toast toast--${toast.kind}`} role={toast.kind === 'error' ? 'alert' : 'status'} data-testid={`toast-${toast.kind}`}>
      <span className="toast-icon"><Icon icon={ICONS[toast.kind]} size={14} strokeWidth={3} /></span>
      <span className="toast-text">{toast.text}</span>
      <span className="toast-tools">
        {action !== undefined && (
          <button type="button" className="toast-action" data-testid="toast-action" onClick={runAction}>{action.label}</button>
        )}
        <button type="button" className="toast-close" aria-label={t('app.close_notification')} title={t('app.close')} data-testid="toast-close" onClick={onClose}>
          <Icon icon={X} size={14} />
        </button>
      </span>
    </div>
  );
}

/** Shows the stack of notifications of a store (the one of the application by default). */
export function Toaster({ store = toasts }: { store?: StoreApi<ToastState> }) {
  const { t } = useTranslation();
  const list = useStore(store, (state) => state.toasts);
  const dismiss = useStore(store, (state) => state.dismiss);
  return (
    <section className="toasts" aria-label={t('app.notifications')} data-testid="toasts">
      {list.map((toast) => (
        <ToastView key={toast.id} toast={toast} onClose={() => {
          dismiss(toast.id);
        }} />
      ))}
    </section>
  );
}
