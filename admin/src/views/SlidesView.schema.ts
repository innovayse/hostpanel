/**
 * What the slides list is made of: a header with its add button and a table of the homepage hero slides, a drag handle
 * column first. `SlidesView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from the
 * same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The slides list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsSlides), subtitle: 'settings.slides.manageHomepageHeroSlidesDrag', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'image', label: 'settings.slides.image', size: UiColumnSize.Compact },
      { key: 'title', label: 'shared.title', size: UiColumnSize.Grow },
      { key: 'audience', label: 'settings.slides.audience', size: UiColumnSize.Compact },
      { key: 'status', label: 'shared.status', size: UiColumnSize.Compact, cardRole: UiCardRole.Badge },
      { key: 'visibility', label: 'settings.slides.visibility', size: UiColumnSize.Compact },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
