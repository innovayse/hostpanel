/**
 * Skeleton schema of the order screen: the header (its title is the order number, its actions are accept, cancel and
 * delete) over the two cards of the overview tab, the order info and the linked invoice.
 *
 * The items and notes tabs are reached only once the order is read, so the overview is what a skeleton draws. The real
 * screen puts its bodies in the `card-info` and `card-invoice` slots of `UiDetailCards`.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the order screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { crumbs: true, actions: 3 },
  columns: [
    [
      {
        key: 'info',
        title: 'orders.orderDetail.orderInfo',
        rows: [
          'shared.client',
          'shared.paymentMethod',
          'shared.ipAddress',
          'orders.orderDetail.created',
          'orders.orderDetail.linesSubtotal',
        ],
      },
    ],
    [
      {
        key: 'invoice',
        title: 'orders.orderDetail.invoice',
        rows: ['shared.invoiceId', 'shared.status', 'orders.orderDetail.invoiceTotal'],
      },
    ],
  ],
}

export default schema
