// QuickAdd.tsx
// The four round « + » of a step shown on hover: a new step after or before it in the flow, or handed over to the lane below or above (B-2.3-41 to B-2.3-45).

import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { QuickAdd as Offered } from '../canvas/flowElements.ts';
import { insertStep, type InsertPosition } from '../state/actions/steps.ts';
import { useEditor } from '../state/EditorContext.tsx';

const POSITIONS: readonly InsertPosition[] = ['after', 'before', 'below', 'above'];

export function QuickAdd({ stepId, offered }: { stepId: string; offered: Offered }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const shown = POSITIONS.filter((position) => (position === 'below' ? offered.below : position === 'above' ? offered.above : true));
  return (
    <>
      {/* A bridge the pointer crosses to reach the « + »: they stay shown on the way (B-2.3-45). */}
      <span className="quick-add-bridge" aria-hidden="true" />
      {shown.map((position) => (
        <button key={position} type="button" className={`quick-add quick-add--${position} nodrag nopan`} aria-label={t(`map.quick_add.${position}`)}
          title={t(`map.quick_add.${position}`)} data-testid={`quick-add-${position}`}
          onPointerDown={(event) => {
            event.stopPropagation();
          }}
          onDoubleClick={(event) => {
            event.stopPropagation();
          }}
          onClick={(event) => {
            event.stopPropagation();
            void insertStep(editor, stepId, position);
          }}>
          <Icon icon={Plus} size={12} strokeWidth={2.5} />
        </button>
      ))}
    </>
  );
}
