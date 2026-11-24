/**
 * What the support tickets list is made of: a header with the open ticket button, a search box with the status,
 * priority, department and flag filters, and a paged, selectable table. `SupportTicketsView` draws its table head from
 * these columns and `UiSkeletonOf` draws the whole list from the same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The support tickets list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SupportTickets), subtitle: 'support.supportTickets.manageAllClientSupportRequests', actions: 1 },
  toolbar: {
    search: true,
    filters: ['shared.status', 'shared.priority', 'shared.department', 'support.supportTickets.flag'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'flag', label: 'support.supportTickets.flag', actions: true, size: UiColumnSize.Compact },
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'department', label: 'shared.department' },
      { key: 'requestor', label: 'support.supportTickets.requestor', cardRole: UiCardRole.Subtitle },
      { key: 'priority', label: 'shared.priority', kind: UiColumnKind.Status },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge, kind: UiColumnKind.Status },
      { key: 'lastReply', label: 'shared.lastReply', kind: UiColumnKind.Date },
    ],
  },
}

export default schema
