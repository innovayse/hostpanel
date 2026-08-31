/**
 * Shapes for the chart.js datasets the report screens build by hand.
 *
 * These views declare their datasets once and then refill `datasets[n].data` on every load.
 * Typed as a plain array, `datasets[n]` reads as possibly `undefined` under
 * `noUncheckedIndexedAccess`, even though the number of series is fixed at the declaration.
 * Declaring the datasets as a **tuple** of this type is what makes each index a definite
 * read, and it documents how many series the chart has.
 */

/** One line series on a report chart. */
export interface ReportLineSeries {
  /** Legend label for the series. */
  label: string
  /** The plotted values, refilled on each load. */
  data: number[]
  /** Stroke colour. */
  borderColor: string
  /** Fill colour under the line. */
  backgroundColor: string
  /** Whether the area under the line is filled. */
  fill: boolean
  /** Bezier tension applied to the line. */
  tension: number
}
