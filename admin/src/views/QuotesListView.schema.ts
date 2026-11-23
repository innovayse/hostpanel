/**
 * What the quotes list is made of: a header with its create button, a search box with the client, stage and validity
 * filters, and a paged table. `QuotesListView` draws its table head from these columns and `UiSkeletonOf` draws the whole
 * list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiCardRole, UiColumnSize } from '@/utils/ui/table'

/** The quotes list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingQuotes), subtitle: 'billing.quotesList.customerQuotesAndProposals', actions: 1 },
  toolbar: { search: true, filters: ['shared.client', 'shared.stage', 'billing.quotesList.validityPeriod'] },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'client', label: 'shared.clientName', cardRole: UiCardRole.Subtitle },
      { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'total', label: 'shared.total', numeric: true, size: UiColumnSize.Compact },
      { key: 'expiry', label: 'shared.expiryDate', kind: UiColumnKind.Date },
      { key: 'created', label: 'billing.quotesList.created', kind: UiColumnKind.Date },
    ],
  },
}

export default schema
