/**
 * What the storefront FAQ list is made of: a header with its add button and a draggable table of the questions, a drag
 * handle column first. `FaqsView` takes its captions from these columns and `UiSkeletonOf` draws the whole list from the
 * same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The storefront FAQ list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.StorefrontFaqs), subtitle: 'storefront.faqs.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'question', label: 'storefront.faqs.question', size: UiColumnSize.Grow },
      { key: 'category', label: 'storefront.faqs.category' },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
