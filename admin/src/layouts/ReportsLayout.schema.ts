/**
 * Skeleton schema of the reports layout: the report menu as a rail beside the routed report.
 *
 * The entries come from the report registry the sidebar, the narrow-screen strip and the router are made of, one per
 * report with its short menu caption, so the skeleton shown while the layout downloads lists the real reports.
 */
import { REPORTS } from '@/lib/reports'
import { SkeletonKind, SkeletonTabsOrientation } from '@/utils/ui/skeletonSchema'
import type { TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The reports layout: one rail entry per report, a tab strip over the report below `md`. */
const schema: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  orientation: SkeletonTabsOrientation.Vertical,
  stripBelowMd: true,
  tabs: REPORTS.map(report => ({ key: report.slug, label: report.menuKey })),
}

export default schema
