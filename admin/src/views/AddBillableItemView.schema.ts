/**
 * Skeleton schema of the add billable item screen: a header over one card of fields.
 *
 * The card is drawn for a one-time charge. {@link RECURRING_ITEM_SECTION} stands in for it while the charge recurs: the
 * due date is then the next due date, and the recurrence period joins the card.
 */
import { SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema, SkeletonSection } from '@/utils/ui/skeletonSchema'

/** Key of the card of fields, the one the screen swaps for {@link RECURRING_ITEM_SECTION}. */
export const ITEM_SECTION_KEY = 'item'

/** The card of a one-time charge: who is billed, for what, how much, of which kind and the day it is invoiced. */
const ITEM_SECTION: SkeletonSection = {
  key: ITEM_SECTION_KEY,
  fields: [
    { key: 'clientId', label: 'shared.client', span: SkeletonSpan.Full },
    { key: 'description', label: 'shared.description', span: SkeletonSpan.Full },
    { key: 'amount', label: 'shared.amount' },
    { key: 'currency', label: 'shared.currency' },
    { key: 'type', label: 'shared.type' },
    { key: 'nextDueDate', label: 'billing.addBillableItem.invoiceOn' },
  ],
}

/** The card of a recurring charge: the day is its next due date and the recurrence period follows. */
export const RECURRING_ITEM_SECTION: SkeletonSection = {
  ...ITEM_SECTION,
  fields: [
    ...ITEM_SECTION.fields.map(field => (field.key === 'nextDueDate' ? { ...field, label: 'billing.addBillableItem.nextDueDate' } : field)),
    { key: 'recurringPeriod', label: 'billing.addBillableItem.recurringPeriod' },
  ],
}

/** What the add billable item screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: 'shared.addBillableItem',
    subtitle: 'billing.addBillableItem.createANewBillableItem',
  },
  columns: [[ITEM_SECTION]],
}

export default schema
