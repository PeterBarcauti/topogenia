// StepBadges.test.tsx
// What is pinned on a step: the gauge of its sheet by state and completeness, the warning of the layout and its tooltip (annex B 2.10).

import { screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showMap } from '../../../test/editor.tsx';
import { sampleMap, sheetOf } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the gauge of the sheet (B-2.10-20 to B-2.10-25)', () => {
  it('is absent when the sheet is empty', async () => {
    await showMap(sampleMap());
    expect(screen.queryAllByTestId('step-gauge')).toHaveLength(0);
  });

  it('is as wide as the completeness, 4 % at least, in the class of the state of the sheet', async () => {
    const map = sampleMap();
    const sheets = new Map([['s1', sheetOf({ status: 'draft', completeness: 1 })], ['s2', sheetOf({ status: 'validated', completeness: 100 })], ['s4', sheetOf({ status: 'needs_review', completeness: 60 })]]);
    await showMap({ ...map, steps: map.steps.map((step) => ({ ...step, sheet: sheets.get(step.id) ?? step.sheet })) });
    const fill = (code: string) => within(screen.getByTestId(`step-${code}`)).getByTestId('step-gauge').firstElementChild as HTMLElement;
    expect([fill('VTE-E001').className, fill('VTE-E001').style.width]).toEqual(['gauge--draft', '4%']);
    expect([fill('VTE-E002').className, fill('VTE-E002').style.width]).toEqual(['gauge--validated', '100%']);
    expect([fill('VTE-E004').className, fill('VTE-E004').style.width]).toEqual(['gauge--needs_review', '60%']);
    expect(within(screen.getByTestId('step-VTE-E004')).getByTestId('step-gauge')).toHaveAttribute('title', 'Fiche à revoir, complétée à 60 %');
  });
});

describe('the warnings of the layout (B-2.10-16 to B-2.10-19)', () => {
  it('pin a « ! » on the step, its warnings in the tooltip, one a line', async () => {
    await showMap(sampleMap({ warnings: [{ step_id: 's1', code: 'DEBUT_LIEN_ENTRANT' }, { step_id: 's4', code: 'FIN_LIEN_SORTANT' }, { step_id: 's4', code: 'ETAPE_ISOLEE' }] }));
    const start = within(screen.getByTestId('step-VTE-E001')).getByTestId('step-warning');
    expect(start).toHaveTextContent('!');
    expect(start).toHaveAttribute('title', 'Une étape de début n’a normalement pas de flèche entrante.');
    const end = within(screen.getByTestId('step-VTE-E004')).getByTestId('step-warning');
    expect(end.getAttribute('title')?.split('\n')).toEqual([
      'Une étape de fin n’a normalement pas de flèche sortante.',
      'Cette étape n’est reliée à aucune autre : tracer les flèches qui la relient au processus.',
    ]);
    expect(within(screen.getByTestId('step-VTE-E002')).queryByTestId('step-warning')).toBeNull();
  });
});
