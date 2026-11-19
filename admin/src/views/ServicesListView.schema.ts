/**
 * What the services list is made of: a header, a search box with the status, product and billing cycle filters, and a
 * paged table. `ServicesListView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from
 * the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnSize } from '@/utils/ui/table'

/** The services list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.ClientsSharedHostings), subtitle: 'services.servicesList.viewAndManageAllClient' },
  toolbar: { search: true, filters: ['shared.status', 'shared.productService', 'shared.billingCycle'] },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', size: UiColumnSize.Compact },
      { key: 'product', label: 'shared.productService', size: UiColumnSize.Grow },
      { key: 'domain', label: 'shared.domain' },
      { key: 'client', label: 'shared.clientName', cardRole: UiCardRole.Subtitle },
      { key: 'price', label: 'shared.price', numeric: true, size: UiColumnSize.Compact },
      { key: 'billingCycle', label: 'shared.billingCycle' },
      { key: 'nextDueDate', label: 'shared.nextDueDate', size: UiColumnSize.Compact },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge, size: UiColumnSize.Compact },
    ],
  },
}

export default schema
