/**
 * Skeleton schema of the server revenue forecasts report: the refresh action over the table of servers with
 * their active services and what those renew for, monthly and over a year, and the note naming the base currency.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.serverRevenueForecasts.serverRevenueForecasts', subtitle: 'reports.serverRevenueForecasts.thisReportShowsWhatTheActive' },
  table: {
    columns: [
      { key: 'serverName', label: 'reports.serverRevenueForecasts.server', size: UiColumnSize.Grow },
      { key: 'hostname', label: 'reports.serverRevenueForecasts.hostname' },
      { key: 'activeServices', label: 'reports.serverRevenueForecasts.activeServices', align: UiAlign.Right },
      { key: 'monthlyRevenue', label: 'reports.serverRevenueForecasts.monthlyRevenue', align: UiAlign.Right },
      { key: 'annualRevenue', label: 'reports.serverRevenueForecasts.annualRevenue', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Recurring,
  emptyText: 'reports.serverRevenueForecasts.noServers',
}

export default schema
