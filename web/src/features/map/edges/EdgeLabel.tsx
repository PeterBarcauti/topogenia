// EdgeLabel.tsx
// The label of a flow in the box the routing placed: its text, shown to translate when it is not written in the language shown (ADR 083), the share of its cases under the value stream layer (spec 14.5), the document icon with the count, their names on hover (B-3.4-35 to B-3.4-39).

import { EdgeLabelRenderer } from '@xyflow/react';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { EDGE_LABEL } from '../model/geometry/constants.ts';
import type { Rect } from '../model/geometry/grid.ts';
import type { FlowShareText } from '../model/valueStream.ts';

/**
 * The share after the text, in the same line as the routing measured it (« non · 15 % », `labelWithShare`): the remainder
 * of a decision in italics, an unknown share in the warning colour.
 */
function Share({ share, after }: { share: FlowShareText; after: boolean }) {
  const { t } = useTranslation();
  const help = t(share.unknown ? 'stream.share.unknown' : share.remainder ? 'stream.share.remainder' : 'stream.share.entered');
  const classes = ['edge-share', share.remainder && 'is-remainder', share.unknown && 'is-unknown'].filter(Boolean).join(' ');
  return <>{after && ' \u00b7 '}<span className={classes} title={help} data-testid="edge-share">{share.text}</span></>;
}

export function EdgeLabel({ id, box, text, untranslated = false, documents, share = null }: {
  id: string; box: Rect; text: string; untranslated?: boolean; documents: readonly string[]; share?: FlowShareText | null;
}) {
  const { t } = useTranslation();
  const names = t('map.flow_documents', { count: documents.length, names: documents.join(', ') });
  return (
    <EdgeLabelRenderer>
      <div className="edge-label nodrag nopan" style={{ transform: `translate(${String(box.x)}px, ${String(box.y)}px)`, width: box.width, height: box.height }}
        data-testid={`edge-label-${id}`}>
        {(text !== '' || share !== null) && (
          <span className="edge-label-text">
            {text !== '' && <span className={untranslated ? 'untranslated' : undefined}>{text}</span>}
            {share !== null && <Share share={share} after={text !== ''} />}
          </span>
        )}
        {documents.length > 0 && (
          <span className="edge-documents" role="img" aria-label={names} title={names} data-testid="edge-documents">
            <Icon icon={FileText} size={EDGE_LABEL.iconSize} strokeWidth={2.25} />
            {documents.length}
          </span>
        )}
      </div>
    </EdgeLabelRenderer>
  );
}
