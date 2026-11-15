// reports.ts
// The reports of the tests (plan 3.1, L4): the reply of the model for the map of the sample, and a hard one with long titles, many actions of the same scores and every wave, for the checks of the assembly and of the rendering.

import type { Schema } from '../api/client.ts';

type Report = Schema<'AssistantReportView'>;

/** A report of the map of the sample (`sampleMap`): its steps VIS-E001 to VIS-E004, one pain point noted. */
export function sampleReport(overrides: Partial<Report> = {}): Report {
  return {
    summary: 'Une demande reçue est vérifiée, puis facturée si elle est conforme.',
    actors: 'Le commercial fait l’essentiel ; la comptabilité facture.',
    chapters: [
      { title: 'Recevoir et vérifier', short: 'Recevoir', steps: ['VIS-E001', 'VIS-E002'], story: 'La demande arrive, le commercial la vérifie.' },
      { title: 'Décider et facturer', short: 'Facturer', steps: ['VIS-E003', 'VIS-E004'], story: 'Conforme, elle est facturée ; sinon, elle revient.' },
    ],
    journey: [
      { step: 'VIS-E001', short: 'Recevoir', score: 4, why: 'Simple.' },
      { step: 'VIS-E002', short: 'Vérifier', score: 2, why: 'Ressaisie des pièces.' },
      { step: 'VIS-E003', short: 'Décider', score: 3, why: 'Sans fiche.' },
      { step: 'VIS-E004', short: 'Facturer', score: 5, why: 'Automatique.' },
    ],
    problems: [
      { source: 'noted', pain_point: 'VIS-I001', steps: ['VIS-E002'], title: 'Ressaisie des pièces', short: 'Ressaisie', detail: '6 h perdues par mois.', kind: 're_entry' },
      { source: 'spotted', pain_point: null, steps: ['VIS-E003', 'VIS-E002'], title: 'Boucle de reprise', short: 'Boucle', detail: 'Une demande non conforme revient.', kind: 'loop' },
    ],
    actions: [
      { title: 'Importer les pièces', short: 'Importer', detail: 'Un import depuis le courriel.', problems: [1], owner: 'Commercial', effort: 2, impact: 5, wave: 'now' },
      { title: 'Compléter avant l’envoi', short: 'Compléter', detail: 'Une liste des pièces.', problems: [2], owner: 'Commercial', effort: 1, impact: 3, wave: 'month' },
    ],
    conclusion: 'Commencer par l’import des pièces.',
    noted: [{ code: 'VIS-I001', step: 'VIS-E002', description: 'Pièces ressaisies à la main', lost_hours_per_month: 6, status: 'reported' }],
    ...overrides,
  };
}

const LONG = 'Vérifier la complétude du dossier de demande et des pièces justificatives jointes avant validation';

/** A hard report: long titles everywhere, twelve actions, several of the same scores, every wave, a pain point without its step. */
export function hardReport(): Report {
  const base = sampleReport();
  return {
    ...base,
    chapters: base.chapters.map((chapter) => ({ ...chapter, title: `${chapter.title} : ${LONG}`, short: LONG.slice(0, 25) })),
    // Titles of a journey hard to fit in their box: 25 characters, a word wider than it, Chinese and Russian.
    journey: base.journey.map((step, index) => ({
      ...step, short: [LONG.slice(0, 25), 'Rechnungsprüfungsverfahrensordnung', '核对供应商发票与订单是否一致并记录差异原因和处理结果', 'Проверка полноты пакета документов поставщика'][index] ?? LONG,
    })),
    problems: Array.from({ length: 12 }, (_, index) => ({ ...base.problems[1], title: `${LONG} ${String(index)}`, short: LONG.slice(0, 25) } as Report['problems'][number])),
    actions: Array.from({ length: 12 }, (_, index) => ({
      title: `${LONG} ${String(index)}`, short: LONG.slice(0, 25), detail: LONG, problems: [index + 1], owner: 'Commercial',
      effort: index < 6 ? 2 : (index % 5) + 1, impact: index < 6 ? 4 : ((index * 3) % 5) + 1, wave: ['now', 'month', 'later'][index % 3] ?? 'later',
    })),
    noted: [...base.noted, { code: 'VIS-I002', step: null, description: LONG, lost_hours_per_month: null, status: 'in_progress' }],
  };
}
