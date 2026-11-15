// mermaidRender.test.ts
// The rendering of the diagrams of the exports, drawn in a real Chromium with the version of Mermaid of the tests in a page styled as the preview of the reader of reference (plan 3.1, rendu Mermaid): on the hard cases, no title cut by its box, nothing that covers another thing.

import { frenchText } from '../../../test/frenchTexts.ts';
import { flowOf, laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { withDrawings, type Drawing } from '../../../test/mermaidRender.ts';
import { hardReport, sampleReport } from '../../../test/reports.ts';
import { flowchartOf, journeyOf, mermaidInit } from './mermaid.ts';
import { reportMarkdown } from './report.ts';

const LONG = 'Vérifier la complétude du dossier de demande et des pièces justificatives jointes avant la validation par le responsable hiérarchique direct';

/** Ten lanes of two steps, labels of up to 150 characters, decisions and flows back with long labels. */
function hardMap() {
  const lanes = Array.from({ length: 10 }, (_, index) => laneOf(`l${String(index)}`, index, { name: `Équipe ${String(index + 1)} : ${LONG.slice(0, 20 + index * 3)}` }));
  const steps = lanes.flatMap((lane, index) => [
    stepOf(`a${String(index)}`, lane.id, index * 2, { label: LONG.slice(0, 150 - index * 7) }),
    stepOf(`b${String(index)}`, lane.id, index * 2 + 1, { type: index % 3 === 0 ? 'decision' : 'task', label: `${LONG.slice(0, 40 + index * 9)} ?` }),
  ]);
  const flows = steps.slice(1).map((step, index) => flowOf(`f${String(index)}`, steps[index]?.id ?? '', step.id, index % 4 === 0 ? { label: 'si le dossier est complet et signé' } : {}));
  const back = [flowOf('back1', 'b3', 'a1', { label: 'non : compléter le dossier avant de le renvoyer' }), flowOf('back2', 'b6', 'a4', { label: 'à reprendre' })];
  return mapOf({ lanes, steps, flows: [...flows, ...back] });
}

const clean = (drawing: Drawing) => ({ cut: drawing.cut, overlaps: drawing.overlaps });

describe('the diagrams drawn (rendu Mermaid)', () => {
  it('a flowchart of a map: no label cut, no node, flow label or lane title on another', async () => {
    await withDrawings(async (draw) => {
      expect(clean(await draw(flowchartOf(sampleMap(), 'fr')))).toEqual({ cut: [], overlaps: [] });
      expect(clean(await draw(flowchartOf(hardMap(), 'fr')))).toEqual({ cut: [], overlaps: [] });
    });
  }, 120_000);

  it('every diagram of a report, the hard one too: something measured in each, no title cut, nothing on another thing', async () => {
    const facts = { title: 'VIS', breadcrumb: '', classification: 'Interne', date: 'aujourd’hui', model: 'm', footer: 'f' };
    await withDrawings(async (draw) => {
      for (const report of [sampleReport(), hardReport()]) {
        const text = reportMarkdown(sampleMap(), 'fr', frenchText, facts, report, { problem: null });
        for (const [, diagram] of text.matchAll(/```mermaid\n([\s\S]*?)\n```/g)) {
          const drawing = await draw(diagram ?? '');
          expect([drawing.kind, drawing.measured > 0, clean(drawing)]).toEqual([drawing.kind, true, { cut: [], overlaps: [] }]);
        }
      }
    });
  }, 180_000);

  it('the measure finds what it looks for: the titles of the lanes covered with the margin of one line, labels shown larger than measured', async () => {
    await withDrawings(async (draw) => {
      const [, ...body] = flowchartOf(hardMap(), 'fr').split('\n');
      expect((await draw([mermaidInit(1), ...body].join('\n'))).overlaps.some((overlap) => overlap.startsWith('lane title'))).toBe(true);
      expect((await draw(flowchartOf(sampleMap(), 'fr'), true)).cut).not.toEqual([]);
      // Under the lines 1.6 high of the preview, the 16 px of the other diagrams leave the two lines of a step out of its box
      // (the journey of the report, as the reader of reference showed it).
      const [, ...journey] = journeyOf('', [{ title: 'Extraction des visites', tasks: [{ title: 'Déclenchement quotidien', score: 3 }] }]).split('\n');
      expect((await draw([mermaidInit(), ...journey].join('\n'))).cut).not.toEqual([]);
    });
  }, 120_000);
});
