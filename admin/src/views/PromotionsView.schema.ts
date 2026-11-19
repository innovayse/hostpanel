/**
 * Skeleton schema of the promotions report: the refresh action over the table of promotion codes with how many
 * services use each and what the active ones renew for, and the note naming the base currency.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.promotions.promotions', subtitle: 'reports.promotions.thisReportShowsHowOften' },
  table: {
    columns: [
      { key: 'code', label: 'reports.promotions.code', size: UiColumnSize.Grow },
      { key: 'services', label: 'reports.promotions.services', align: UiAlign.Right },
      { key: 'activeServices', label: 'reports.promotions.activeServices', align: UiAlign.Right },
      { key: 'monthlyRevenue', label: 'reports.promotions.monthlyRevenue', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Recurring,
  emptyText: 'reports.promotions.noPromotionCodesUsed',
}

export default schema
