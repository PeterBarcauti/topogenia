// RoutedEdge.test.tsx
// The flows drawn: the path of the routing as it is, the arrow of 18 × 18, the clickable band of 16 px, the label box and its documents (annex B 3.4).

import { screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showMap } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';
import { layoutMap } from '../model/mapLayout.ts';
import { estimateMeasure } from '../model/textSize.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const edgeOf = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__edge[data-id="${id}"]`);

describe('the drawing of the flows (B-3.4-29 to B-3.4-33)', () => {
  it('each flow follows the path of the routing, as the routing gave it', async () => {
    const canvas = await showMap(sampleMap());
    const layout = layoutMap(sampleMap(), 'fr', estimateMeasure);
    for (const flow of layout.flows) expect(edgeOf(canvas, flow.route.id)?.querySelector('path.react-flow__edge-path')?.getAttribute('d')).toBe(flow.route.path);
  });

  it('ends with the closed arrow of the canvas, 18 × 18, scaled by the stroke', async () => {
    const canvas = await showMap(sampleMap());
    expect(edgeOf(canvas, 'f1')?.querySelector('path.react-flow__edge-path')?.getAttribute('marker-end')).toBe("url('#map-arrow')");
    const marker = canvas.querySelector('marker#map-arrow');
    expect([marker?.getAttribute('markerWidth'), marker?.getAttribute('markerHeight'), marker?.getAttribute('markerUnits')]).toEqual(['18', '18', 'strokeWidth']);
  });

  it('can be reached on a band of 16 px around the stroke', async () => {
    const canvas = await showMap(sampleMap());
    expect(edgeOf(canvas, 'f2')?.querySelector('path.react-flow__edge-interaction')?.getAttribute('stroke-width')).toBe('16');
  });

  it('carries the class of the flows, and the kind of the routing as its type', async () => {
    const canvas = await showMap(sampleMap());
    expect(edgeOf(canvas, 'f1')?.querySelector('path.react-flow__edge-path')).toHaveClass('map-edge');
    expect([edgeOf(canvas, 'f1')?.classList.contains('react-flow__edge-flow'), edgeOf(canvas, 'f4')?.classList.contains('react-flow__edge-loop')]).toEqual([true, true]);
  });
});

describe('the labels of the flows (B-3.4-35 to B-3.4-39)', () => {
  it('stand in the box the routing placed, with the text of the flow', async () => {
    await showMap(sampleMap());
    const route = layoutMap(sampleMap(), 'fr', estimateMeasure).flows.find((flow) => flow.route.id === 'f3')?.route;
    const label = screen.getByTestId('edge-label-f3');
    expect(label).toHaveTextContent('oui');
    expect([label.style.transform, label.style.width, label.style.height]).toEqual([
      `translate(${String(route?.label?.x)}px, ${String(route?.label?.y)}px)`, `${String(route?.label?.width)}px`, `${String(route?.label?.height)}px`,
    ]);
    expect(label).toHaveClass('edge-label', 'nodrag', 'nopan');
  });

  it('show the text in the language of the interface, from the language of the source step', async () => {
    await i18next.changeLanguage('en');
    await showMap(sampleMap());
    expect(screen.getByTestId('edge-label-f3')).toHaveTextContent('yes');
    expect(screen.getByTestId('edge-label-f4')).toHaveTextContent('non');
  });

  it('show the document icon with the count of the documents carried, their names on hover', async () => {
    await showMap(sampleMap());
    const documents = within(screen.getByTestId('edge-label-f2')).getByTestId('edge-documents');
    expect(documents).toHaveTextContent('2');
    expect(documents).toHaveAttribute('title', '2 documents : Bon de commande, Devis');
    expect(documents.querySelector('svg.i')).not.toBeNull();
    expect(within(screen.getByTestId('edge-label-f3')).queryByTestId('edge-documents')).toBeNull();
  });

  it('a flow without a text nor documents has no label', async () => {
    await showMap(sampleMap());
    expect(screen.queryByTestId('edge-label-f1')).toBeNull();
  });
});
