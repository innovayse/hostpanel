/**
 * Skeleton schema of the OSS report: the refresh action, a quarter filter, the line naming the period over the table of member
 * states with their VAT, and the sentence shown when the quarter has no invoice.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.oss.ossReport', subtitle: 'reports.oss.thisReportProvidesTheInformation' },
  filters: [{ key: 'quarter', label: 'reports.oss.selectQuarter' }],
  summary: true,
  table: {
    columns: [
      { key: 'countryName', label: 'reports.oss.countryName', size: UiColumnSize.Grow },
      { key: 'countryCode', label: 'reports.oss.countryCode', kind: UiColumnKind.Id },
      { key: 'vatRate', label: 'reports.oss.vatRate', align: UiAlign.Right },
      { key: 'numberOfInvoices', label: 'reports.oss.numberOfInvoices', align: UiAlign.Right },
      { key: 'totalExclVat', label: 'reports.oss.totalValueInvoicedExclVat', align: UiAlign.Right },
      { key: 'totalVatCollected', label: 'reports.oss.totalVatCollected', align: UiAlign.Right },
      { key: 'currency', label: 'shared.currency', kind: UiColumnKind.Id },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
