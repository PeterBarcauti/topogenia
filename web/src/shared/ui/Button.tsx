// Button.tsx
// The buttons of the interface: plain, secondary, primary, danger, and the large one of the home banner (spec 9.5, mockup).

import type { ButtonHTMLAttributes } from 'react';
import type { LucideIcon } from 'lucide-react';
import { Icon } from './Icon.tsx';

export type ButtonVariant = 'plain' | 'secondary' | 'primary' | 'danger';

export interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'type'> {
  /** The stable identifier of the end-to-end tests (S-3.5-04). */
  readonly testId: string;
  readonly icon?: LucideIcon;
  readonly variant?: ButtonVariant;
  readonly large?: boolean;
  readonly type?: 'button' | 'submit';
}

export function Button({ testId, icon, variant = 'plain', large = false, type = 'button', className, children, ...rest }: ButtonProps) {
  const classes = ['btn', variant === 'plain' ? '' : `btn--${variant}`, large ? 'btn--lg' : '', className ?? ''].filter((name) => name !== '').join(' ');
  return (
    <button {...rest} type={type === 'submit' ? 'submit' : 'button'} className={classes} data-testid={testId}>
      {icon !== undefined && <Icon icon={icon} size={large ? 18 : 15} />}
      {children}
    </button>
  );
}
