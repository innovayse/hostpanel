/**
 * Skeleton schema of the client statement: the print action, the client and date range filters, the opening balance
 * lines, the table of the statement lines and the closing balance lines. The balance lines are one per currency, which
 * the data decides, so the schema holds their place and the skeleton draws a placeholder of them.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the client statement is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Print],
  header: { title: 'reports.clientStatement.clientAccountRegisterBalance', subtitle: 'reports.clientStatement.thisReportProvidesAStatement' },
  filters: [
    { key: 'client', label: 'reports.clientStatement.selectClient' },
    { key: 'dateRange', label: 'shared.dateRange' },
  ],
  summary: true,
  table: {
    columns: [
      { key: 'type', label: 'shared.type', size: UiColumnSize.Compact },
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'currency', label: 'shared.currency', size: UiColumnSize.Compact },
      { key: 'debit', label: 'reports.clientStatement.debit', align: UiAlign.Right },
      { key: 'credit', label: 'reports.directDebit.credit', align: UiAlign.Right },
      { key: 'balance', label: 'shared.balance', align: UiAlign.Right },
    ],
  },
  closing: true,
}

export default schema
