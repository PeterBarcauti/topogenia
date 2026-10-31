// classification.ts
// The classification of a project: the three levels of the closed list, from the least to the most protected (spec 4.1, Q5).

import type { Schema } from '../../api/client.ts';

export type Classification = Schema<'Classification'>;

/** The levels in the order of the contract, from the least to the most protected; Internal is the default of a new project. */
export const CLASSIFICATIONS: readonly Classification[] = ['internal', 'confidential', 'strictly_confidential'];
