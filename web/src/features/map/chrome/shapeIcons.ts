// shapeIcons.ts
// The small shapes of the ribbon and the menus, drawn as icons of the set (spec 9.5): step, wait, start, end, the three diamonds with their markers; the icons of the lane types (validated mock-up).

import { Building2, createLucideIcon, Server, User, type LucideIcon, type LucideIconNode } from 'lucide-react';
import type { Schema } from '../../../api/client.ts';

const DIAMOND: LucideIconNode = ['path', { d: 'M12 3 21 12 12 21 3 12Z', key: 'diamond' }];

/** The icon of each type of lane: person, department, system (B-2.12-06, spec 9.3), in its header and in its menu. */
export const LANE_ICONS: Readonly<Record<Schema<'LaneType'>, LucideIcon>> = { person: User, department: Building2, system: Server };

/** The value stream layer: the line of time of a value stream map, up for the lead time, down for the work (spec 14.5, validated mockup). */
export const VALUE_STREAM_ICON: LucideIcon = createLucideIcon('value-stream', [['path', { d: 'M2 8h5v8h5V8h5v8h5', key: 'ladder' }]]);

/** One icon a type of step, as the ribbon of the mock-up shows them. */
export const SHAPE_ICONS: Readonly<Record<Schema<'StepType'>, LucideIcon>> = {
  task: createLucideIcon('shape-task', [['rect', { x: '3', y: '6', width: '18', height: '12', rx: '3', key: 'box' }]]),
  wait: createLucideIcon('shape-wait', [['rect', { x: '3', y: '6', width: '18', height: '12', rx: '3', strokeDasharray: '3 2.4', key: 'box' }]]),
  start: createLucideIcon('shape-start', [['rect', { x: '2.5', y: '7', width: '19', height: '10', rx: '5', key: 'pill' }]]),
  end: createLucideIcon('shape-end', [
    ['rect', { x: '2', y: '6.5', width: '20', height: '11', rx: '5.5', key: 'pill' }],
    ['rect', { x: '4.75', y: '9.25', width: '14.5', height: '5.5', rx: '2.75', strokeWidth: '1.25', key: 'inner' }],
  ]),
  decision: createLucideIcon('shape-decision', [DIAMOND, ['path', { d: 'm9.75 9.75 4.5 4.5m0-4.5-4.5 4.5', key: 'cross' }]]),
  parallel: createLucideIcon('shape-parallel', [DIAMOND, ['path', { d: 'M12 8.75v6.5M8.75 12h6.5', key: 'plus' }]]),
  inclusive: createLucideIcon('shape-inclusive', [DIAMOND, ['circle', { cx: '12', cy: '12', r: '3', key: 'ring' }]]),
};
