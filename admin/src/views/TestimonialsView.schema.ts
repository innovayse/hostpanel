/**
 * What the storefront testimonials list is made of: a header with its add button and a draggable table of the customer
 * quotes, a drag handle column first. `TestimonialsView` takes its captions from these columns and `UiSkeletonOf` draws
 * the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The storefront testimonials list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.StorefrontTestimonials), subtitle: 'storefront.testimonials.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'author', label: 'storefront.testimonials.author', size: UiColumnSize.Grow },
      { key: 'quote', label: 'storefront.testimonials.quote' },
      { key: 'rating', label: 'storefront.testimonials.rating' },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
