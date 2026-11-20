/**
 * What the pending bank transfers list is made of: a header and a paged table. `PendingBankTransfersView` draws its table
 * head from these columns and `UiSkeletonOf` draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The pending bank transfers list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingBankTransfers), subtitle: 'billing.bankTransfers.subtitle' },
  table: {
    pagination: true,
    columns: [
      { key: 'invoice', label: 'shared.invoice', size: UiColumnSize.Compact },
      { key: 'client', label: 'shared.client', size: UiColumnSize.Grow },
      { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
      { key: 'reference', label: 'billing.bankTransfers.reference' },
      { key: 'requested', label: 'billing.bankTransfers.requested', size: UiColumnSize.Compact },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
