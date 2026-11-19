/**
 * The data of a report's chart: what the report page needs, beside the chart of its schema, to draw it.
 */
import type { UiChartSeries } from '@/utils/ui/chart'

/** The values a report plots; the kind and height of the chart are its schema's. */
export interface ReportChartData {
  /** Caption above the chart (already translated); none when omitted. */
  title?: string
  /** Category labels (or slice names for a donut). */
  labels: string[]
  /** Data series. */
  series: UiChartSeries[]
  /** Formats numbers in tooltips and on the value axis. */
  valueFormatter?: (value: number) => string
  /** Shows the legend; by default only when it says something. */
  legend?: boolean
}
