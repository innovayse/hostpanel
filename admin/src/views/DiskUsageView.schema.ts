/**
 * Skeleton schema of the disk usage report: the header, the notice of the servers that could not be read and the table of
 * hosting accounts. The notice only shows when the last refresh failed on a server, so the schema holds its place and the
 * skeleton draws a placeholder of it. The report draws one such table per server once it is read; until then the screen
 * draws this one, so the skeleton has the real columns.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the disk usage report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.diskUsage.diskSpaceBandwidthUsageSummary', subtitle: 'reports.diskUsage.thisReportShowsTheDisk' },
  alert: true,
  table: {
    columns: [
      { key: 'clientName', label: 'reports.diskUsage.clientNameDomain', size: UiColumnSize.Grow },
      { key: 'diskUsage', label: 'reports.diskUsage.diskUsage', align: UiAlign.Right },
      { key: 'diskLimit', label: 'reports.diskUsage.diskLimit', align: UiAlign.Right },
      { key: 'diskPercent', label: 'reports.diskUsage.used', align: UiAlign.Right },
      { key: 'bwUsage', label: 'reports.diskUsage.bwUsage', align: UiAlign.Right },
      { key: 'bwLimit', label: 'reports.diskUsage.bwLimit', align: UiAlign.Right },
      { key: 'bwPercent', label: 'reports.diskUsage.used', align: UiAlign.Right },
    ],
  },
}

export default schema
