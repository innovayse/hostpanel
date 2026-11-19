/**
 * The charts of the dashboard and support overview panels: the kind and plot height of each, defined once.
 *
 * A panel card draws its chart with these, and the schema of the screen draws the skeleton of the same panel from
 * the same entry, so the placeholder and the chart can never differ in shape.
 */
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import type { SkeletonChart } from '@/utils/ui/skeletonSchema'

/** Closed set of the panel charts. */
export const PanelChart = {
  /** Revenue per day of the dashboard. */
  Revenue: 'revenue',
  /** New orders and new clients per day of the dashboard. */
  Growth: 'growth',
  /** Invoices per status of the dashboard. */
  InvoiceStatus: 'invoiceStatus',
  /** Open tickets per priority of the dashboard. */
  TicketPriority: 'ticketPriority',
  /** Tickets submitted per hour of the day on the support overview. */
  TicketsByHour: 'ticketsByHour',
} as const

/** One of the panel charts. */
export type PanelChart = (typeof PanelChart)[keyof typeof PanelChart]

/** Kind and height of every panel chart. */
export const PANEL_CHARTS: Readonly<Record<PanelChart, SkeletonChart>> = {
  [PanelChart.Revenue]: { kind: ChartKind.GroupedBar, height: UiChartHeight.Short },
  [PanelChart.Growth]: { kind: ChartKind.Line, height: UiChartHeight.Short },
  [PanelChart.InvoiceStatus]: { kind: ChartKind.GroupedBar, height: UiChartHeight.Sm },
  [PanelChart.TicketPriority]: { kind: ChartKind.GroupedBar, height: UiChartHeight.Sm },
  [PanelChart.TicketsByHour]: { kind: ChartKind.HorizontalBar, height: UiChartHeight.Lg },
}
