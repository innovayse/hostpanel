/**
 * One figure of the stat tile row at the top of a report.
 */
import type { UiTone } from '@/utils/ui/tone'

/** A tile of `ReportStatRow`. */
export interface ReportStat {
  /** Identifies the tile; also the suffix of its `data-testid`. */
  key: string
  /** What the figure measures (already translated). */
  label: string
  /** The figure, already formatted. */
  value: string | number
  /** Line under the figure (already translated). */
  hint?: string
  /** Colour of the figure. */
  tone?: UiTone
}
