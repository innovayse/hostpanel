/**
 * What the announcements list is made of: a header with the add button, a search box with the published filter, and a
 * paged table. `AnnouncementsListView` draws its table head from these columns and `UiSkeletonOf` draws the whole list
 * from the same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The announcements list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SupportAnnouncements), subtitle: 'support.announcementsList.manageClientFacingAnnouncements', actions: 1 },
  toolbar: { search: true, filters: ['shared.published'] },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'title', label: 'shared.title', size: UiColumnSize.Grow },
      { key: 'published', label: 'shared.published', kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
