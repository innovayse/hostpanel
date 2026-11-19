/**
 * Skeleton schema of the support overview: a header with the period select over five figure tiles and two cards.
 *
 * The tiles are the period's ticket figures and the average first reply time; the cards hold the average first reply time
 * and the tickets-by-hour chart. A tile key is also the suffix of the tile's test id.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { PANEL_CHARTS, PanelChart } from '@/lib/panelCharts'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, DashboardSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The one row of panels: the average card and the hours chart card, side by side. */
export const SUPPORT_PANELS_ROW: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [{ key: 'average', title: 'support.supportOverview.averageFirstReplyTime' }],
    [{ key: 'hours', title: 'support.supportOverview.ticketsSubmittedByHour', chart: PANEL_CHARTS[PanelChart.TicketsByHour] }],
  ],
}

/** The support overview: tiles, then the average card and the hours chart card. */
const schema: DashboardSkeletonSchema = {
  kind: SkeletonKind.Dashboard,
  header: {
    title: navTitleKey(AppRoute.SupportOverview),
    subtitle: 'support.supportOverview.keyMetricsForTheSelected',
    actions: 1,
  },
  stats: [
    { key: 'new-tickets', label: 'support.supportOverview.newTickets' },
    { key: 'client-replies', label: 'support.supportOverview.clientReplies' },
    { key: 'staff-replies', label: 'support.supportOverview.staffReplies' },
    { key: 'without-reply', label: 'support.supportOverview.ticketsWithoutReply' },
    { key: 'average-first-response', label: 'support.supportOverview.averageFirstResponse' },
  ],
  panels: [SUPPORT_PANELS_ROW],
}

export default schema
