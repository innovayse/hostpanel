/**
 * What the email campaigns list is made of: a header with the new campaign button, a search box with the status filter,
 * and a paged table of the campaigns. `EmailCampaignsListView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The email campaigns list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SupportEmailCampaigns), subtitle: 'emailCampaigns.list.subtitle', actions: 1 },
  toolbar: { search: true, filters: ['shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'scheduledAt', label: 'emailCampaigns.list.scheduledAt', kind: UiColumnKind.Date },
      { key: 'sentAt', label: 'emailCampaigns.list.sentAt', kind: UiColumnKind.Date },
      { key: 'delivery', label: 'emailCampaigns.list.delivery' },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
