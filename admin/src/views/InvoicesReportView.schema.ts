/**
 * Skeleton schema of the invoices report: the print and export actions, the field picker and the three date ranges,
 * and the table with the columns shown before the operator chooses others (those keys are also the screen's default
 * columns), with its pager.
 */
import { REPORT_FIELDS_KEY } from '@/lib/reportsSchema'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'

/** What the invoices report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Export, ReportAction.Print],
  header: { title: 'shared.invoices', subtitle: 'reports.invoicesReport.thisReportCanBeUsed' },
  filters: [
    { key: REPORT_FIELDS_KEY, label: 'shared.fieldsToInclude' },
    { key: 'created', label: 'reports.invoicesReport.creationDate' },
    { key: 'due', label: 'shared.dueDate' },
    { key: 'paid', label: 'shared.datePaid' },
  ],
  table: {
    columns: [
      { key: 'id', label: 'shared.id' },
      { key: 'clientName', label: 'shared.clientName' },
      { key: 'dates.created', label: 'reports.invoicesReport.creationDate' },
      { key: 'amounts.total', label: 'shared.total' },
      { key: 'status', label: 'shared.status' },
    ],
    pagination: true,
  },
  emptyText: 'shared.noInvoicesFound',
}

export default schema
