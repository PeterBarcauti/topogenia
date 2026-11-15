// FrameNode.tsx
// The frame of a dissolution on the canvas: dashes round the steps brought back into the flow, and its label that opens the remainder (annex B 2.9, validated mockup).

import { memo, type MouseEvent } from 'react';
import type { NodeProps } from '@xyflow/react';
import { Inbox } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { FrameNode } from '../canvas/flowElements.ts';
import { useEditor } from '../state/EditorContext.tsx';

/** « Reliquat de CODE · n », or « Remonté de CODE · rien à ventiler » once nothing is left (B-2.9-21 to B-2.9-23). */
export const FrameNodeView = memo(function FrameNodeView({ data }: NodeProps<FrameNode>) {
  const { t } = useTranslation();
  const editor = useEditor();
  const { frame, remainder } = data;
  const open = (event: MouseEvent) => {
    event.stopPropagation();
    editor.floating.getState().open({ kind: 'remainder' });
  };
  return (
    <div className="dissolution-frame" data-testid="dissolution-frame">
      <button type="button" className="dissolution-label nodrag nopan" title={t('frame.help', { code: frame.step_code, label: frame.step_label })} data-testid="dissolution-label"
        onClick={open} onDoubleClick={(event) => { event.stopPropagation(); }}>
        <Icon icon={Inbox} size={12} strokeWidth={2.25} />
        {remainder > 0 ? t('frame.label', { code: frame.step_code, count: remainder }) : t('frame.label_empty', { code: frame.step_code })}
      </button>
    </div>
  );
});
