/**
 * What the client activity log tab is made of: a header, a search box and the date range filter of its dialog over a paged
 * table of the log entries. `ClientLogView` draws its table head from these columns and `UiSkeletonOf` draws the whole tab
 * from the same data while the view's chunk is still downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The client activity log tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'clients.clientLog.activityLog' },
  toolbar: {
    search: true,
    filters: ['shared.dateRange'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'entry', label: 'clients.clientLog.logEntry', size: UiColumnSize.Grow },
      { key: 'user', label: 'clients.clientLog.user' },
      { key: 'ipAddress', label: 'shared.ipAddress', size: UiColumnSize.Compact },
    ],
  },
}

export default schema
