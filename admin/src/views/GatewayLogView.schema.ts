/**
 * What the gateway transaction log is made of: a header, a search box with the result and period filters, and a paged
 * table of the recorded interactions. `GatewayLogView` draws its table head from these columns and `UiSkeletonOf` draws
 * the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The gateway transaction log. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingGatewayLog), subtitle: 'billing.gatewayLog.viewPaymentGatewayTransactionLogs' },
  toolbar: { search: true, filters: ['billing.gatewayLog.result', 'shared.dateRange'] },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date' },
      { key: 'gateway', label: 'billing.gatewayLog.gateway' },
      { key: 'debugData', label: 'billing.gatewayLog.debugData', size: UiColumnSize.Grow },
      { key: 'result', label: 'billing.gatewayLog.result', size: UiColumnSize.Compact },
    ],
  },
}

export default schema
