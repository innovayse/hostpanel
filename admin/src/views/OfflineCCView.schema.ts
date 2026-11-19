/**
 * Skeleton schema of the offline credit card screen: a header over the one card of the payment fields.
 *
 * The fields are the ones the payment is recorded with (invoice, date, amount, fees, method and gateway reference);
 * `OfflineCCView` binds each control in its `field-<key>` slot and `UiSkeletonOf` draws the same card with bars.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the offline credit card screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: navTitleKey(AppRoute.BillingOfflineCc),
    subtitle: 'billing.offlineCC.processOfflineCreditCardPayments',
  },
  columns: [
    [
      {
        key: 'payment',
        fields: [
          { key: 'invoiceId', label: 'shared.invoice' },
          { key: 'date', label: 'shared.date' },
          { key: 'amount', label: 'shared.amount' },
          { key: 'fees', label: 'shared.transactionFees' },
          { key: 'gateway', label: 'shared.paymentMethod' },
          { key: 'transactionId', label: 'shared.transactionId', required: true },
        ],
      },
    ],
  ],
}

export default schema
