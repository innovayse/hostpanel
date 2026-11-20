/**
 * Skeleton schema of the admin dashboard: a header with the period toggle over the four headline tiles and the panels.
 *
 * The greeting is the title and depends on the hour, so a bar stands in for it while the subtitle is real. The revenue tile's
 * caption names its period, so the schema holds the 30 day caption and the screen swaps in the chosen period's one (see
 * {@link PERIOD_REVENUE_STAT_KEY}). The panels are the cards of the screen in three rows, as `DashboardView` lays them out:
 * the revenue chart beside the attention list (wide main), the three small charts side by side, and the server load beside
 * the recent invoices (wide aside). Each row is exported, so the screen takes its ratio and column count from the same
 * definition the skeleton draws; each panel draws its own body.
 */
import { PANEL_CHARTS, PanelChart } from '@/lib/panelCharts'
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, DashboardSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Key of the revenue tile, the one whose caption and figure follow the chosen period. */
export const PERIOD_REVENUE_STAT_KEY = 'periodRevenue'

/** The first row: the revenue chart beside the list of what needs attention. */
export const DASHBOARD_MAIN_ROW: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  ratio: UiSplitRatio.WideMain,
  columns: [
    [{ key: 'revenue', title: 'dashboard.revenue.title', chart: PANEL_CHARTS[PanelChart.Revenue] }],
    [
      {
        key: 'attention',
        title: 'dashboard.attention.title',
        rows: [
          'dashboard.attention.kind.overdueInvoice',
          'dashboard.attention.kind.pendingOrder',
          'dashboard.attention.kind.expiringDomain',
          'dashboard.attention.kind.urgentTicket',
        ],
      },
    ],
  ],
}

/** The second row: the growth, invoice status and ticket priority charts side by side. */
export const DASHBOARD_CHARTS_ROW: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [{ key: 'growth', title: 'dashboard.growth.title', chart: PANEL_CHARTS[PanelChart.Growth] }],
    [{ key: 'invoiceStatus', title: 'dashboard.invoiceStatus.title', chart: PANEL_CHARTS[PanelChart.InvoiceStatus] }],
    [{ key: 'tickets', title: 'dashboard.tickets.title', chart: PANEL_CHARTS[PanelChart.TicketPriority] }],
  ],
}

/** The third row: the server load beside the recent invoices. */
export const DASHBOARD_PANELS_ROW: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  ratio: UiSplitRatio.WideAside,
  columns: [[{ key: 'serverLoad', title: 'dashboard.serverLoad.title' }], [{ key: 'recentInvoices', title: 'dashboard.recentInvoices' }]],
}

/** The dashboard: the greeting header with one action (the period toggle), four tiles and seven panels in three rows. */
const schema: DashboardSkeletonSchema = {
  kind: SkeletonKind.Dashboard,
  header: { subtitle: 'dashboard.subtitle', actions: 1 },
  stats: [
    { key: PERIOD_REVENUE_STAT_KEY, label: 'dashboard.stats.revenue.Days30' },
    { key: 'activeServices', label: 'dashboard.stats.activeServices' },
    { key: 'overdueInvoices', label: 'dashboard.stats.overdueInvoices' },
    { key: 'openTickets', label: 'dashboard.stats.openTickets' },
  ],
  panels: [DASHBOARD_MAIN_ROW, DASHBOARD_CHARTS_ROW, DASHBOARD_PANELS_ROW],
}

export default schema
