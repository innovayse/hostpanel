/**
 * What the held payment claims list is made of: a header and a paged, read-only table of claims. `PaymentClaimsView`
 * draws its table head from these columns and `UiSkeletonOf` draws the whole list from the same data while the view's
 * chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The held payment claims list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingPaymentClaims), subtitle: 'billing.paymentClaims.subtitle' },
  table: {
    pagination: true,
    columns: [
      { key: 'invoice', label: 'shared.invoice', kind: UiColumnKind.Id },
      { key: 'client', label: 'shared.client', size: UiColumnSize.Grow },
      { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
      { key: 'gateway', label: 'billing.paymentClaims.gatewayModule' },
      { key: 'created', label: 'billing.paymentClaims.created', kind: UiColumnKind.Date },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge, kind: UiColumnKind.Status },
    ],
  },
}

export default schema
