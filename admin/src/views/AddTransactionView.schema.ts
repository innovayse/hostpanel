/**
 * Skeleton schema of the add transaction screen: a header over the one card of the ledger entry fields.
 *
 * The card is the form both the add and the edit transaction screens draw (`TransactionForm`); the edit screen's schema
 * reuses its columns, so the two cannot drift apart.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema, SkeletonSection } from '@/utils/ui/skeletonSchema'

/** The card of a ledger entry: date and client, the amounts, what it is for and how it was paid. */
export const TRANSACTION_SECTION: SkeletonSection = {
  key: 'transaction',
  fields: [
    { key: 'date', label: 'shared.date' },
    { key: 'clientId', label: 'billing.addTransaction.relatedClient' },
    { key: 'amountIn', label: 'shared.amountIn' },
    { key: 'amountOut', label: 'shared.amountOut' },
    { key: 'description', label: 'shared.description' },
    { key: 'fees', label: 'shared.fees' },
    { key: 'transactionId', label: 'shared.transactionId' },
    { key: 'paymentMethod', label: 'shared.paymentMethod' },
    { key: 'invoiceId', label: 'billing.addTransaction.invoiceIdsCommaSeparated', span: SkeletonSpan.Full },
    { key: 'addToCredit', label: 'billing.addTransaction.addToClientSCredit', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
  ],
}

/** What the add transaction screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: 'billing.addTransaction.createTransaction',
    subtitle: 'billing.addTransaction.recordANewPaymentTransaction',
  },
  columns: [[TRANSACTION_SECTION]],
}

export default schema
