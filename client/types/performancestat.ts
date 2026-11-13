/**
 * Performance stat type: one figure of the performance section.
 *
 * @module types/performancestat
 */

import type { PerformanceStatKey } from '~/constants/performanceStatKey'

/** A performance figure the operator set. */
export interface PerformanceStat {
  /** Which figure it is. */
  key: PerformanceStatKey
  /** The figure as the operator wrote it, e.g. `99.9%`. */
  value: string
}
