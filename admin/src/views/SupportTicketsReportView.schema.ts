/**
 * Skeleton schema of the support ticket replies report: month and year filters and the table of staff members with
 * their reply total, the average per day and the strip of the days of the month.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.titles.reports.supportTicketReplies', subtitle: 'reports.supportTicketsReport.thisReportShowsABreakdown' },
  filters: [
    { key: 'month', label: 'shared.month' },
    { key: 'year', label: 'shared.year' },
  ],
  table: {
    columns: [
      { key: 'adminName', label: 'reports.supportTicketsReport.admin', size: UiColumnSize.Grow },
      { key: 'total', label: 'shared.total', align: UiAlign.Right },
      { key: 'averagePerDay', label: 'reports.supportTicketsReport.averagePerDay', align: UiAlign.Right },
      { key: 'dailyCounts', label: 'reports.supportTicketsReport.byDay' },
    ],
  },
  emptyText: 'shared.noDataFoundForThis',
}

export default schema
