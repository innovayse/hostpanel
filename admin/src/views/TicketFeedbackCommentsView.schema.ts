/**
 * Skeleton schema of the ticket feedback comments report: staff member and date range filters and the table of
 * the comments with their rating.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.ticketFeedbackComments.ticketFeedbackComments', subtitle: 'reports.ticketFeedbackComments.thisReportAllowsYouTo' },
  filters: [
    { key: 'staffName', label: 'reports.ticketFeedbackComments.staffName' },
    { key: 'dateRange', label: 'shared.dateRange' },
  ],
  table: {
    columns: [
      { key: 'ticketId', label: 'reports.ticketFeedbackComments.ticketId', kind: UiColumnKind.Id },
      { key: 'staffName', label: 'reports.ticketFeedbackComments.staffName' },
      { key: 'subject', label: 'shared.subject' },
      { key: 'feedbackLeftBy', label: 'reports.ticketFeedbackComments.feedbackLeft' },
      { key: 'rating', label: 'reports.ticketFeedbackComments.rating', align: UiAlign.Center, kind: UiColumnKind.Status },
      { key: 'feedbackComment', label: 'reports.ticketFeedbackComments.comments', size: UiColumnSize.Grow },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
