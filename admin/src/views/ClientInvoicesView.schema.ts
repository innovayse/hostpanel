/**
 * What the client invoices tab is made of: a header with the create button, a search box and the two filters of its
 * dialog (status and invoice date) over a paged, selectable table of the client's invoices. `ClientInvoicesView` draws its
 * table head from these columns and `UiSkeletonOf` draws the whole tab from the same data while the view's chunk is still
 * downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The client invoices tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'shared.invoices', actions: 1 },
  toolbar: {
    search: true,
    filters: ['shared.status', 'shared.invoiceDate'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'invoiceDate', label: 'shared.invoiceDate', kind: UiColumnKind.Date },
      { key: 'dueDate', label: 'shared.dueDate', kind: UiColumnKind.Date },
      { key: 'datePaid', label: 'shared.datePaid', kind: UiColumnKind.Date },
      { key: 'total', label: 'shared.total', numeric: true, size: UiColumnSize.Compact },
      { key: 'paymentMethod', label: 'shared.paymentMethod' },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
