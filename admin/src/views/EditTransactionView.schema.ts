/**
 * Skeleton schema of the edit transaction screen: the add screen's card of fields under the edit header.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'
import addSchema from '@/views/AddTransactionView.schema'

/** What the edit transaction screen is made of: the same fields as the add screen, a header of its own. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: 'billing.editTransaction.editTransaction',
    subtitle: 'billing.editTransaction.updatePaymentTransactionDetails',
  },
  columns: addSchema.columns,
}

export default schema
