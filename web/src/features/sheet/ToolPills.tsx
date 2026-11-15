// ToolPills.tsx
// The tools of a step as pills (essai J10): each one in its tint, read at a glance in the head of its section, folded or not, and in the sheet read.

import { toolTint } from './model/tools.ts';

/** The pills of the tools, in the order of the field; nothing drawn without a tool. */
export function ToolPills({ tools }: { tools: readonly string[] }) {
  if (tools.length === 0) return null;
  return (
    <span className="tool-pills" data-testid="sheet-tool-pills">
      {tools.map((tool, index) => <span key={`${String(index)}-${tool}`} className={`tool-pill is-tint-${String(toolTint(tool))}`}>{tool}</span>)}
    </span>
  );
}
