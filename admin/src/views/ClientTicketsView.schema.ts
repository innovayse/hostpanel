/**
 * What the client tickets tab is made of: a header with the open ticket link, the four opened-tickets tiles, a search box
 * and the three filters of its dialog (status, priority, department) over a paged table of the client's tickets.
 * `ClientTicketsView` draws its table head from these columns and `UiSkeletonOf` draws the whole tab from the same data
 * while the view's chunk is still downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema, SkeletonStat } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The four opened-tickets tiles. */
export const CLIENT_TICKETS_STATS: readonly SkeletonStat[] = [
  { key: 'openedThisMonth', label: 'clients.clientTickets.openedThisMonth' },
  { key: 'openedLastMonth', label: 'clients.clientTickets.openedLastMonth' },
  { key: 'openedThisYear', label: 'clients.clientTickets.openedThisYear' },
  { key: 'openedLastYear', label: 'clients.clientTickets.openedLastYear' },
]

/** The client tickets tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'clients.clientTickets.tickets', actions: 1 },
  stats: CLIENT_TICKETS_STATS,
  toolbar: {
    search: true,
    filters: ['shared.status', 'shared.priority', 'shared.department'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'dateOpened', label: 'clients.clientTickets.dateOpened', kind: UiColumnKind.Date },
      { key: 'department', label: 'shared.department' },
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'priority', label: 'shared.priority', kind: UiColumnKind.Status },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'lastReply', label: 'shared.lastReply', kind: UiColumnKind.Date },
    ],
  },
}

export default schema
