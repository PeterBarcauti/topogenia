// DropPreview.tsx
// Where the steps dragged would go, while the pointer holds them: the box of each cell dashed over the map, in the coordinates of the canvas (lot G of the plan 3.1).

import type { DropPreview } from '../model/moves.ts';

export function DropPreviewView({ preview }: { preview: DropPreview }) {
  // Two steps dropped on one cell are each drawn: the host shifts the second (RG-C04).
  return preview.boxes.map((box, index) => (
    <div key={`${String(box.x)}:${String(box.y)}:${String(index)}`} className="drop-box" data-testid="drop-box"
      style={{ transform: `translate(${String(box.x)}px, ${String(box.y)}px)`, width: box.width, height: box.height }} />
  ));
}
