/**
 * What the credit notes list is made of: a header, a search box with the client, status and method filters, and a paged
 * table. `CreditNotesListView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from the
 * same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The credit notes list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingCreditNotes), subtitle: 'billing.creditNotes.subtitle' },
  toolbar: { search: true, filters: ['shared.client', 'shared.status', 'billing.creditNotes.methodLabel'] },
  table: {
    pagination: true,
    columns: [
      { key: 'number', label: 'billing.creditNotes.number', kind: UiColumnKind.Id },
      { key: 'invoice', label: 'billing.creditNotes.invoice', kind: UiColumnKind.Id },
      { key: 'client', label: 'billing.creditNotes.client', size: UiColumnSize.Grow },
      { key: 'status', label: 'billing.creditNotes.statusLabel', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'method', label: 'billing.creditNotes.methodLabel' },
      { key: 'total', label: 'billing.creditNotes.total', numeric: true },
      { key: 'issuedAt', label: 'billing.creditNotes.issuedAt', kind: UiColumnKind.Date },
    ],
  },
}

export default schema
