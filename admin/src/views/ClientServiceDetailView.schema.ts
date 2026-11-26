/**
 * Skeleton schema of the client service screen: a header of data (the service and its product) with three buttons, over
 * the cards of the tab the screen opens on, the service info and the module commands.
 *
 * The screen has a tab strip, and the other two tabs hold their own cards: {@link BILLING_COLUMNS} and
 * {@link NOTES_COLUMNS}. The screen draws every tab through the same `UiDetailCards`, so the cards of a tab are listed
 * once, here. The tab strip itself is not part of the schema: a detail screen has no tab vocabulary.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema, SkeletonDetailCard } from '@/utils/ui/skeletonSchema'

/** Closed set of the keys of the cards, the suffixes of their `card-<key>` slots. */
export const ServiceCard = {
  /** The hosting account fields. */
  Hosting: 'hosting',
  /** The status and the module commands. */
  Commands: 'commands',
  /** The billing terms. */
  Billing: 'billing',
  /** The automation switches. */
  Automation: 'automation',
  /** The addons of the service. */
  Addons: 'addons',
  /** The admin notes. */
  Notes: 'notes',
} as const

/** The cards of the account tab: the hosting account beside the module commands. */
const ACCOUNT_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [
  [
    {
      key: ServiceCard.Hosting,
      title: 'clients.clientServiceDetail.serviceInfo',
      rows: [
        'clients.clientServiceDetail.order',
        'clients.clientServiceDetail.server',
        'shared.domain',
        'clients.clientServiceDetail.provisioningRef',
        'clients.clientServiceDetail.dedicatedIp',
        'shared.username',
        'shared.password',
      ],
    },
  ],
  [{ key: ServiceCard.Commands, title: 'clients.clientServiceDetail.moduleCommands', rows: ['shared.status'] }],
]

/** The cards of the billing tab: the billing terms over the addons, beside the automation switches. */
export const BILLING_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [
  [
    {
      key: ServiceCard.Billing,
      title: 'clients.clientServiceDetail.billing',
      rows: [
        'shared.productService',
        'clients.clientServiceDetail.quantity',
        'shared.registrationDate',
        'clients.clientServiceDetail.firstPaymentAmount',
        'clients.clientServiceDetail.recurringAmount',
        'shared.nextDueDate',
        'shared.billingCycle',
        'shared.paymentMethod',
        'clients.clientServiceDetail.promotionCode',
        'clients.clientServiceDetail.subscriptionId',
      ],
    },
    { key: ServiceCard.Addons, title: 'clients.clientServiceDetail.addons' },
  ],
  [
    {
      key: ServiceCard.Automation,
      title: 'clients.clientServiceDetail.autoManagement',
      rows: [
        'clients.clientServiceDetail.autoRenew',
        'clients.clientServiceDetail.overrideAutoSuspend',
        'clients.clientServiceDetail.autoTerminateEndOfCycle',
      ],
    },
  ],
]

/** The cards of the notes tab: the admin notes. */
export const NOTES_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [[{ key: ServiceCard.Notes, title: 'shared.adminNotes' }]]

/** What the client service screen is made of, as it opens (on the account tab). */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { crumbs: true, actions: 3 },
  columns: ACCOUNT_COLUMNS,
}

export default schema
