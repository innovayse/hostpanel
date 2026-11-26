/**
 * What the client quotes tab is made of: a header with the create button, a search box and the two filters of its dialog
 * (stage and validity period) over a paged table of the client's quotes. `ClientQuotesView` draws its table head from these
 * columns and `UiSkeletonOf` draws the whole tab from the same data while the view's chunk is still downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The client quotes tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'shared.quotes', actions: 1 },
  toolbar: {
    search: true,
    filters: ['shared.stage', 'billing.quotesList.validityPeriod'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'createDate', label: 'clients.clientQuotes.createDate', kind: UiColumnKind.Date },
      { key: 'validUntil', label: 'shared.validUntil', kind: UiColumnKind.Date },
      { key: 'total', label: 'shared.total', numeric: true },
      { key: 'stage', label: 'shared.stage', kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
