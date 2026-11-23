/**
 * Skeleton schema of the ticket ratings reviewer: a rating and a date range filter and the table of rated replies.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.ticketRatingsReviewer.supportTicketRatingsReviewer', subtitle: 'reports.ticketRatingsReviewer.thisReportIsShowingAll' },
  filters: [
    { key: 'minRating', label: 'reports.ticketRatingsReviewer.rating' },
    { key: 'dateRange', label: 'shared.dateRange' },
  ],
  table: {
    columns: [
      { key: 'ticketId', label: 'reports.ticketRatingsReviewer.ticket', kind: UiColumnKind.Id },
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'message', label: 'shared.message', size: UiColumnSize.Grow },
      { key: 'adminName', label: 'reports.ticketRatingsReviewer.admin' },
      { key: 'rating', label: 'reports.ticketRatingsReviewer.rating', align: UiAlign.Center, kind: UiColumnKind.Status },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
