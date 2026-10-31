// Dialog.tsx
// A modal dialog: title, focus kept inside and given back, Escape closes, the shortcuts below are silent while it is open (B-2.5-03).

import { useEffect, useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useKeyLayer } from '../keyboard/KeyboardContext.tsx';
import { Icon } from './Icon.tsx';

const FOCUSABLE = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface DialogProps {
  readonly title: string;
  readonly testId: string;
  readonly onClose: () => void;
  readonly children: ReactNode;
  /** The buttons at the bottom of the dialog. */
  readonly footer?: ReactNode;
  readonly wide?: boolean;
}

/** Keeps Tab and Shift+Tab inside the dialog. */
function trapTab(event: KeyboardEvent<HTMLDivElement>): void {
  const items = [...event.currentTarget.querySelectorAll<HTMLElement>(FOCUSABLE)];
  const first = items[0];
  const last = items.at(-1);
  if (first === undefined || last === undefined) return;
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

/** The first element to focus: the one marked `data-autofocus`, else the first field or button of the body, else the close button. */
function firstFocus(box: HTMLElement | null): HTMLElement | null {
  return box?.querySelector<HTMLElement>('[data-autofocus]') ?? box?.querySelector<HTMLElement>(`.dialog-body :is(${FOCUSABLE}), .dialog-foot :is(${FOCUSABLE})`)
    ?? box?.querySelector<HTMLElement>(FOCUSABLE) ?? null;
}

export function Dialog({ title, testId, onClose, children, footer, wide = false }: DialogProps) {
  const { t } = useTranslation();
  const titleId = useId();
  const box = useRef<HTMLDivElement>(null);
  useKeyLayer('modal', () => false);
  useEffect(() => {
    const before = document.activeElement;
    firstFocus(box.current)?.focus();
    return () => {
      if (before instanceof HTMLElement) before.focus();
    };
  }, []);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
    } else if (event.key === 'Tab') trapTab(event);
  };
  return (
    <div className="overlay" data-testid={`${testId}-overlay`}>
      <div ref={box} className={wide ? 'dialog dialog--wide' : 'dialog'} role="dialog" aria-modal="true" aria-labelledby={titleId} data-testid={testId} onKeyDown={onKeyDown}>
        <header className="dialog-head">
          <h2 id={titleId}>{title}</h2>
          <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid={`${testId}-close`} onClick={onClose}>
            <Icon icon={X} size={16} />
          </button>
        </header>
        <div className="dialog-body">{children}</div>
        {footer !== undefined && <footer className="dialog-foot">{footer}</footer>}
      </div>
    </div>
  );
}
