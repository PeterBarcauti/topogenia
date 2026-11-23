// ProjectFilter.tsx
// The field that filters the list of the projects by code or by name; Escape empties it.

import type { KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';

export interface ProjectFilterProps {
  readonly value: string;
  readonly onChange: (value: string) => void;
}

export function ProjectFilter({ value, onChange }: ProjectFilterProps) {
  const { t } = useTranslation();
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Escape' || value === '') return;
    event.preventDefault();
    onChange('');
  };
  return (
    <input type="search" className="field-control home-filter" data-testid="home-filter" value={value} aria-label={t('home.filter')}
      placeholder={t('home.filter_placeholder')} autoComplete="off" spellCheck={false}
      onChange={(event) => {
        onChange(event.target.value);
      }}
      onKeyDown={onKeyDown} />
  );
}
