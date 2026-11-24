/**
 * What the storefront projects list is made of: a header with its add button and a draggable table of the portfolio
 * projects, a drag handle column first. `ProjectsView` takes its captions from these columns and `UiSkeletonOf` draws
 * the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The storefront projects list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.StorefrontProjects), subtitle: 'storefront.projects.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'cover', label: 'storefront.projects.cover', size: UiColumnSize.Grow },
      { key: 'title', label: 'storefront.projects.projectTitle' },
      { key: 'slug', label: 'storefront.projects.slug' },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
