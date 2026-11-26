/**
 * What the invoices list is made of: a header with its help link, three summary tiles, a search box with the client,
 * status and date filters, and a paged, selectable table. `InvoicesListView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize, UiHideBelow } from '@/utils/ui/table'

/** The invoices list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingInvoices), subtitle: 'billing.invoicesList.manageBillingInvoices', actions: 1 },
  stats: [
    { key: 'paid', label: 'clients.clientSummary.paid' },
    { key: 'due', label: 'billing.invoicesList.due' },
    { key: 'overdue', label: 'clients.clientSummary.overdue' },
  ],
  toolbar: { search: true, filters: ['shared.client', 'shared.status', 'shared.invoiceDate'] },
  table: {
    pagination: true,
    columns: [
      { key: 'invoice', label: 'shared.invoice', cardRole: UiCardRole.Title, kind: UiColumnKind.Id },
      { key: 'client', label: 'shared.clientName', hideBelow: UiHideBelow.Sm, size: UiColumnSize.Grow, cardRole: UiCardRole.Subtitle },
      { key: 'date', label: 'shared.invoiceDate', hideBelow: UiHideBelow.Md, kind: UiColumnKind.Date },
      { key: 'dueDate', label: 'shared.dueDate', hideBelow: UiHideBelow.TwoXl, kind: UiColumnKind.Date },
      { key: 'total', label: 'shared.total', numeric: true, hideBelow: UiHideBelow.Sm },
      { key: 'paymentMethod', label: 'shared.paymentMethod', hideBelow: UiHideBelow.TwoXl },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge, kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
