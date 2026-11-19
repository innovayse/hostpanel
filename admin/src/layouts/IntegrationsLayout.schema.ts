/**
 * Skeleton schema of the integrations layout: the category menu as a rail beside the routed page.
 *
 * The entries come from the category list the sidebar and the narrow-screen strip are made of, one per category with its short menu caption, so the skeleton
 * shown while the layout downloads lists the real categories.
 */
import type { IntegrationCategory } from '@/types/integration'
import { INTEGRATION_CATEGORIES } from '@/types/integrationmeta'
import { SkeletonKind, SkeletonTabsOrientation } from '@/utils/ui/skeletonSchema'
import type { TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The integrations layout: one rail entry per category, a tab strip over the page below `md`. */
const schema: TabsSkeletonSchema<IntegrationCategory> = {
  kind: SkeletonKind.Tabs,
  orientation: SkeletonTabsOrientation.Vertical,
  stripBelowMd: true,
  tabs: INTEGRATION_CATEGORIES.map(category => ({ key: category.key, label: category.menuLabelKey })),
}

export default schema
