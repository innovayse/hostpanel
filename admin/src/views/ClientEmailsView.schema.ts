/**
 * What the client emails tab is made of: a header, a search box and the two filters of its dialog (outcome and date range)
 * over a paged table of the emails sent to the client. `ClientEmailsView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole tab from the same data while the view's chunk is still downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The client emails tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'clients.clientEmails.emails' },
  toolbar: {
    search: true,
    filters: ['clients.clientEmails.status', 'shared.dateRange'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date', size: UiColumnSize.Compact },
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'status', label: 'clients.clientEmails.status', size: UiColumnSize.Compact },
    ],
  },
}

export default schema
