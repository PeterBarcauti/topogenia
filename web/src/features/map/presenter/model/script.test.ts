// script.test.ts
// What a presentation says, scene by scene, in French: one fact a sentence, only what the map does not already write (essai J2), a fact unknown leaving its sentence out; the passages, the opening and the closing.

import i18next from 'i18next';
import { describe, expect, it } from 'vitest';
import { flowOf, laneOf, mapOf, stepOf, streamOf } from '../../../../test/maps.ts';
import type { Schema } from '../../../../api/client.ts';
import { scenesOf, scriptOf, type Names } from './script.ts';
import { tourOf } from './tour.ts';

const t = i18next.getFixedT('fr');

const stepStream = (stepId: string, work: number | null, lead: number | null): Schema<'StepStream'> => ({
  step_id: stepId, throughput: null, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: work,
  work_origin: work === null ? 'none' : 'sheet', lead_hours: lead, lead_origin: lead === null ? 'none' : 'sheet', expected_lead_hours: null, work_per_case_minutes: null,
  charge_hours: null, fte: null, human: true, from_draft: false,
});

/** A start, a check by visits carrying documents to accounting, a decision, an end in accounting. */
function sampleOfSales(stream: Partial<Schema<'MapStream'>> = {}) {
  return mapOf({
    lanes: [laneOf('l1', 0, { name: 'Commercial' }), laneOf('l2', 1, { name: 'Comptabilité' })],
    steps: [
      stepOf('s1', 'l1', 0, { type: 'start', label: 'Demande reçue' }),
      stepOf('s2', 'l1', 1, {
        label: 'Vérifier la demande', value_class: 'necessary',
        markers: {
          documents: [
            { document_id: 'd1', name: 'Bon de commande', direction: 'input', medium: 'paper' }, { document_id: 'd2', name: 'Devis_2024-03_v2.xlsx', direction: 'output' },
          ],
          pain_points: [{ pain_point_id: 'p1', code: 'VIS-I01', description: 'Saisie en double' }],
        },
      }),
      stepOf('s3', 'l2', 2, { type: 'decision', label: 'Conforme ?' }),
      stepOf('s4', 'l2', 3, { type: 'end', label: 'Facture émise' }),
      stepOf('s5', 'l1', 3, { type: 'end', label: 'Demande refusée', row: 1 }),
    ],
    flows: [
      flowOf('f1', 's1', 's2'), flowOf('f2', 's2', 's3', { documents: ['Devis'] }),
      flowOf('f3', 's3', 's4', { label: 'oui' }), flowOf('f4', 's3', 's5', { label: 'non' }),
    ],
    value_stream: streamOf({
      steps: { s2: stepStream('s2', 15, 16) },
      flows: { f3: { flow_id: 'f3', share: 0.8, origin: 'entered' }, f4: { flow_id: 'f4', share: 0.2, origin: 'remainder' } },
      main_path: {
        steps: ['s1', 's2', 's3', 's4'], lead_hours: 23, lead_complete: true, work_minutes: 45, work_complete: true, work_ratio: 0.0326,
        segments: [{ step_id: 's2', lead_hours: 16, work_minutes: 15 }, { step_id: 's3', lead_hours: 7, work_minutes: 30 }],
      },
      handoffs: { flows: ['f2'], detailed_steps: {}, count: 1, per_case: null },
      ...stream,
    }),
  });
}

/** The names of the map as the layout gives them, in the language shown: here the texts as entered. */
function namesOf(map: Schema<'MapView'>): Names {
  return {
    step: (id) => {
      const step = map.steps.find((candidate) => candidate.id === id);
      return step === undefined ? null : { label: step.label, laneId: step.lane_id, code: step.code };
    },
    lane: (id) => {
      const index = map.lanes.findIndex((lane) => lane.id === id);
      return index < 0 ? null : { name: map.lanes[index]?.name ?? '', tint: index + 1 };
    },
    flow: (id) => {
      const flow = map.flows.find((candidate) => candidate.id === id);
      return flow === undefined ? null : { text: flow.label ?? '', documents: flow.documents };
    },
  };
}

function scriptsOf(map: Schema<'MapView'>, from: string | null = null) {
  const tour = tourOf(map, from);
  const context = { map, tour, names: namesOf(map), t, language: 'fr' };
  // The numbers and the quotes keep their no-break spaces on the screen; the tests read them as spaces.
  const plain = (text: string) => text.split('').map((sign) => ([160, 8239].includes(sign.charCodeAt(0)) ? ' ' : sign)).join('');
  return scenesOf(tour).map((scene) => {
    const script = scriptOf(scene, context);
    return { scene, script, lines: script.lines.map(plain) };
  });
}

describe('the scenes of a presentation', () => {
  it('open on the whole map, show each stop and the passage to the next, and close on the whole map', () => {
    const tour = tourOf(sampleOfSales(), null);
    expect(scenesOf(tour).map((scene) => (scene.kind === 'step' || scene.kind === 'passage' ? `${scene.kind}${String(scene.stop)}` : scene.kind))).toEqual([
      // The decision by « non » to its own end, back to it, then by « oui » on the main path (lot J10).
      'opening', 'step0', 'passage0', 'step1', 'passage1', 'step2', 'passage2', 'step3', 'passage3', 'step4', 'passage4', 'step5', 'closing',
    ]);
    expect(scenesOf({ stops: [] })).toEqual([]);
  });
});

describe('what a presentation says (ADR 097)', () => {
  const scripts = scriptsOf(sampleOfSales());

  it('opens with the steps of the map, each counted once, the actors and the lead time end to end', () => {
    expect(scripts[0]?.lines).toEqual(['Le parcours compte 5 étapes.', '2 acteurs y prennent part.', 'Délai de bout en bout : 3,3 jours.', 'Suivons le dossier.']);
  });

  it('names no step nor actor: the box lit on the map says them (essai J2)', () => {
    expect(scripts.map(({ script }) => Object.keys(script))).toEqual(scripts.map(() => ['lines']));
  });

  it('says the facts of a task one a sentence: work and lead time, value, documents received and produced, pain points', () => {
    expect(scripts[3]?.lines).toEqual([
      '15 minutes de travail, pour 2,3 jours de délai.', 'Classe de valeur : Nécessaire sans valeur.', 'Reçoit un document papier : Bon de commande.', 'Produit un tableur Excel : Devis.',
      'Irritant : Saisie en double.',
    ]);
  });

  it('leaves out the work its box shows, a step done by sessions too: its lead time, then the work by case its counter adds', () => {
    const boxed = (map: Schema<'MapView'>) => ({ ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sheet: { ...step.sheet, duration_minutes: 15 } } : step)) });
    expect(scriptsOf(boxed(sampleOfSales()))[3]?.lines[0]).toBe('2,3 jours de délai.');
    const sessions = sampleOfSales({ steps: { s2: { ...stepStream('s2', 15, 7.5), basis: 'cadence', work_per_case_minutes: 1.68 } } });
    expect(scriptsOf(boxed(sessions))[3]?.lines.slice(0, 2)).toEqual(['1,1 jour de délai.', 'Soit 1,7 minute de travail par dossier.']);
  });

  it('says nothing the map already writes: a start, an end, a join, a wait or a detail its shape tells, an exit its arrow names', () => {
    expect(scripts[1]?.lines).toEqual([]);
    expect(scripts[7]?.lines).toEqual([]);
    const detailed = scriptsOf(mapOf({ lanes: [laneOf('l1', 0)], steps: [stepOf('w', 'l1', 0, { type: 'wait' }), stepOf('m', 'l1', 1, { sub_map_id: 'sub', sub_map_empty: false })], flows: [flowOf('f', 'w', 'm')] }));
    expect([detailed[1]?.lines, detailed[3]?.lines]).toEqual([[], []]);
    // A task without any figure, value, document nor pain point says nothing.
    const bare = scriptsOf(mapOf({ lanes: [laneOf('l1', 0)], steps: [stepOf('t', 'l1', 0)] }));
    expect(bare[1]?.lines).toEqual([]);
  });

  it('says at a decision its exits with the share of the cases, and the one the story takes: the other exit first; shown again, the exit it then takes only (lot J10)', () => {
    expect(scripts[5]?.lines).toEqual(['« oui » : 80 % des dossiers.', '« non » : 20 % des dossiers.', 'Le récit suit « non ».']);
    expect(scripts[9]?.lines).toEqual(['Le récit suit « oui ».']);
    // Without the shares of the core, the exits are read on their arrows: only the one taken is said.
    const unshared = scriptsOf(sampleOfSales({ flows: {} }));
    expect([unshared[5]?.lines, unshared[9]?.lines]).toEqual([['Le récit suit « non ».'], ['Le récit suit « oui ».']]);
  });

});

describe('what a presentation says of a map that divides (ADR 097, lot J10)', () => {
  it('says the entries of a map that has several, and that the work splits at a step with several exits and no gateway (lot J10)', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [
        stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('s2', 'l2', 0, { type: 'start' }), stepOf('t', 'l1', 1), stepOf('u', 'l1', 2), stepOf('v', 'l2', 2),
      ],
      flows: [flowOf('f1', 's1', 't'), flowOf('f2', 's2', 't'), flowOf('f3', 't', 'u'), flowOf('f4', 't', 'v')],
      value_stream: streamOf({ handoffs: { flows: ['f2', 'f4'], detailed_steps: {}, count: 2, per_case: null } }),
    });
    const said = scriptsOf(map);
    expect(said[0]?.lines.slice(0, 2)).toEqual(['Le parcours compte 5 étapes.', 'Points de départ : 2.']);
    const lines = (stepId: string, kind: 'step' | 'passage') => said.find(({ scene }) => scene.kind === kind && tourOf(map, null).stops[scene.stop]?.stepId === stepId)?.lines;
    expect(lines('t', 'step')).toEqual(['Le travail se partage en 2 branches menées en même temps.']);
    // Both exits leave together: the handoff among them is said on that passage, whichever flow leads to the next stop.
    expect(lines('t', 'passage')).toEqual(['Relais : Couloir l1 → Couloir l2.']);
  });

});

describe('what a presentation says on its way and at its end (ADR 097)', () => {
  const scripts = scriptsOf(sampleOfSales());

  it('says on a passage the handoff the core counts, the actors in the language shown; what it carries flies beside the light', () => {
    expect(scripts[4]?.lines).toEqual(['Relais : Commercial → Comptabilité.']);
    expect(scripts[2]?.lines).toEqual([]);
    // The handoff is the one the core counts (across the levels too), not a change of lane read by the interface.
    const uncounted = scriptsOf(sampleOfSales({ handoffs: { flows: [], detailed_steps: {}, count: 0, per_case: null } }));
    expect(uncounted[4]?.lines).toEqual([]);
  });

  it('closes with the lead time for the work end to end, the share of the work, the handoffs and the open pain points', () => {
    const closing = scriptsOf(sampleOfSales({
      totals: { ...streamOf().totals, open_pain_points: 2, lost_hours_per_month: 12 },
    })).at(-1);
    expect(closing?.lines).toEqual([
      'Fin du parcours.', 'De bout en bout : 3,3 jours de délai pour 45 minutes de travail.', 'Le travail représente 3,3 % du délai.', '1 passage de relais.',
      '2 irritants ouverts.', 'Temps perdu : 12 heures par mois.',
    ]);
  });

  it('says a lead time incomplete nowhere, and lists four documents as three and the others', () => {
    const map = sampleOfSales({ main_path: { steps: ['s1', 's2', 's3', 's4'], segments: [], lead_hours: 23, lead_complete: false, work_minutes: 45, work_complete: true } });
    const step = map.steps[1] as Schema<'StepView'>;
    const documents = ['Devis', 'Facture.pdf', 'Bordereau', 'Contrat', 'Avenant'].map((name, index) => ({ document_id: `d${String(index)}`, name, direction: 'output' as const }));
    const scripts2 = scriptsOf({ ...map, steps: map.steps.map((candidate) => (candidate === step ? { ...step, markers: { documents, pain_points: [] } } : candidate)) });
    expect(scripts2[0]?.lines).not.toContain('Délai de bout en bout : 3,3 jours.');
    expect(scripts2.at(-1)?.lines.some((line) => line.startsWith('De bout en bout'))).toBe(false);
    expect(scripts2[3]?.lines).toContain('Produit Devis, un PDF : Facture, Bordereau et 2 autres.');
  });

  it('says that the work splits at a parallel gateway, not that it joins again: the shape says it', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [stepOf('p', 'l1', 0, { type: 'parallel' }), stepOf('b1', 'l1', 1), stepOf('b2', 'l2', 1), stepOf('j', 'l1', 2, { type: 'parallel' })],
      flows: [flowOf('f1', 'p', 'b1'), flowOf('f2', 'p', 'b2'), flowOf('f3', 'b1', 'j'), flowOf('f4', 'b2', 'j')],
    });
    const said = scriptsOf(map);
    expect(said[1]?.lines).toEqual(['Le travail se partage en 2 branches menées en même temps.']);
    expect(said.find((script) => script.scene.kind === 'step' && script.scene.stop === 2)?.lines).toEqual([]);
  });
});

describe('the figures a presentation says, the same as its counters (ADR 097)', () => {
  it('closes on the tour shown and on the main path when they differ: a presentation from a step, its counters and its sentence the same', () => {
    const map = sampleOfSales({ steps: { s2: stepStream('s2', 15, 16), s4: { ...stepStream('s4', 0, 0), work_per_case_minutes: 0 } } });
    expect(scriptsOf(map, 's3').at(-1)?.lines.slice(0, 4)).toEqual([
      'Fin du parcours.', 'Ce parcours : 1 jour de délai pour 30 minutes de travail.',
      'Le chemin principal : 3,3 jours de délai pour 45 minutes de travail.', 'Le travail représente 3,3 % du délai.',
    ]);
    // A tour that is the main path says it once.
    expect(scriptsOf(map).at(-1)?.lines[1]).toBe('De bout en bout : 3,3 jours de délai pour 45 minutes de travail.');
  });

  it('says the work by case of a step done by sessions, the figure its counter adds, after its work by execution', () => {
    const map = sampleOfSales({ steps: { s2: { ...stepStream('s2', 15.5, 7.5), basis: 'cadence', work_per_case_minutes: 1.68 } } });
    expect(scriptsOf(map)[3]?.lines.slice(0, 2)).toEqual(['16 minutes de travail, pour 1,1 jour de délai.', 'Soit 1,7 minute de travail par dossier.']);
    // The same figure by case and by execution is said once.
    expect(scriptsOf(sampleOfSales({ steps: { s2: { ...stepStream('s2', 15, 16), work_per_case_minutes: 15 } } }))[3]?.lines[1]).toBe('Classe de valeur : Nécessaire sans valeur.');
  });
});

describe('the comment of presentation of a step (ADR 099)', () => {
  const comment = (stepId: string, text: string, language = 'fr'): Schema<'PresentationCommentView'> =>
    ({ step_id: stepId, text, language, source: language === 'fr' ? 'manual' : 'assistant', version: 1 });
  const said = (comments: Readonly<Record<string, readonly Schema<'PresentationCommentView'>[]>>, language = 'fr') => {
    const map = sampleOfSales();
    const tour = tourOf(map, null);
    const context = { map, tour, names: namesOf(map), t, language, comments };
    return scenesOf(tour).map((scene) => scriptOf(scene, context).lines);
  };

  it('is said instead of the sentences of its step, one line written a sentence, the empty lines left out', () => {
    const lines = said({ s2: [comment('s2', 'Ici le commercial vérifie la demande.\n\n  Il la renvoie si elle est incomplète.  ')] });
    expect(lines[3]).toEqual(['Ici le commercial vérifie la demande.', 'Il la renvoie si elle est incomplète.']);
    // The other scenes say what they said.
    expect(lines.filter((_, index) => index !== 3)).toEqual(said({}).filter((_, index) => index !== 3));
  });

  it('replaces the exits of a decision too, and a long line is cut into subtitles as any sentence', () => {
    const long = 'Le contrôle porte sur le montant, le client et la date de livraison, puis la demande part en comptabilité pour être facturée.';
    const lines = said({ s3: [comment('s3', long)] });
    expect(lines[5]).toEqual(['Le contrôle porte sur le montant, le client et la date de livraison,', 'puis la demande part en comptabilité pour être facturée.']);
  });

  it('without a line in the language shown, leaves the step its own sentences', () => {
    const english = { s2: [comment('s2', 'Visits checks the request.', 'en')] };
    expect(said(english)[3]).toEqual(said({})[3]);
    expect(said(english, 'en')[3]).toEqual(['Visits checks the request.']);
  });

  it('says the line of the language shown, the original or its translation', () => {
    const both = { s2: [comment('s2', 'Le commercial vérifie la demande.'), comment('s2', 'Visits checks the request.', 'en')] };
    expect([said(both)[3], said(both, 'en')[3]]).toEqual([['Le commercial vérifie la demande.'], ['Visits checks the request.']]);
    // A third language has no line: the sentences of the step.
    expect(said(both, 'de')[3]).toEqual(said({}, 'de')[3]);
  });
});

describe('what a presentation says of gateways shown again and of flows lit together (lot J10, independent review)', () => {
  it('says the rule of an inclusive gateway once, then the exit each of its stops takes (M2)', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [stepOf('o', 'l1', 0, { type: 'inclusive' }), stepOf('b1', 'l1', 1), stepOf('b2', 'l2', 1), stepOf('j', 'l1', 2, { type: 'inclusive' })],
      flows: [flowOf('fa', 'o', 'b1', { label: 'a' }), flowOf('fb', 'o', 'b2', { label: 'b' }), flowOf('f3', 'b1', 'j'), flowOf('f4', 'b2', 'j')],
      value_stream: streamOf({ main_path: { steps: ['o', 'b1', 'j'], segments: [], lead_hours: 0, lead_complete: false, work_minutes: 0, work_complete: false } }),
    });
    const stops = scriptsOf(map).filter(({ scene }) => scene.kind === 'step').map(({ lines }) => lines);
    // The gateway by « b », its branch, the gateway again by « a », the step of the path, the join: which says nothing.
    expect(stops).toEqual([['Une ou plusieurs branches partent d’ici, selon le dossier.', 'Le récit suit « b ».'], [], ['Le récit suit « a ».'], [], []]);
  });

  it('says once the handoff of two flows lit together between the same two actors (M3)', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [stepOf('t', 'l1', 0), stepOf('u', 'l2', 1), stepOf('v', 'l2', 1, { row: 1 })],
      flows: [flowOf('f1', 't', 'u'), flowOf('f2', 't', 'v')],
      value_stream: streamOf({ handoffs: { flows: ['f1', 'f2'], detailed_steps: {}, count: 2, per_case: null } }),
    });
    expect(scriptsOf(map).find(({ scene }) => scene.kind === 'passage')?.lines).toEqual(['Relais : Couloir l1 → Couloir l2.']);
  });

  it('says the comment of a decision at its first stop only: shown again, the decision says the exit it takes', () => {
    const map = sampleOfSales();
    const tour = tourOf(map, null);
    const comments: Readonly<Record<string, readonly Schema<'PresentationCommentView'>[]>> = {
      s3: [{ step_id: 's3', text: 'Le contrôle porte sur le montant.', language: 'fr', source: 'manual', version: 1 }],
    };
    const lines = scenesOf(tour).map((scene) => scriptOf(scene, { map, tour, names: namesOf(map), t, language: 'fr', comments }).lines);
    expect([lines[5], lines[9]?.map((line) => line.replaceAll('\u00a0', ' '))]).toEqual([['Le contrôle porte sur le montant.'], ['Le récit suit « oui ».']]);
  });
});
