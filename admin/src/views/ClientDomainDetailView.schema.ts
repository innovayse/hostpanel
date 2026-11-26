/**
 * Skeleton schema of the client domain screen: a header of data (the domain and its status) with Cancel and Save, over the
 * cards of the tab the screen opens on, the domain info and the dates and billing.
 *
 * The screen has a tab strip, and the other two tabs hold their own cards: {@link REGISTRAR_COLUMNS} and
 * {@link MANAGEMENT_COLUMNS}. The screen draws every tab through the same `UiDetailCards`, so the cards of a tab are
 * listed once, here. The tab strip itself is not part of the schema: a detail screen has no tab vocabulary.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema, SkeletonDetailCard } from '@/utils/ui/skeletonSchema'

/** Closed set of the keys of the cards, the suffixes of their `card-<key>` slots. */
export const DomainCard = {
  /** The domain info and the first billing fields. */
  Info: 'info',
  /** The term, the dates, the payment method and the status. */
  Dates: 'dates',
  /** The nameserver fields. */
  Nameservers: 'nameservers',
  /** The registrar commands. */
  Commands: 'commands',
  /** The management tool switches. */
  Tools: 'tools',
  /** The admin notes. */
  Notes: 'notes',
} as const

/** Locale keys of the captions of the nameserver fields, in order; the screen draws one field for each. */
export const NAMESERVER_LABELS: readonly string[] = [
  'clients.clientDomainDetail.nameserver1',
  'clients.clientDomainDetail.nameserver2',
  'clients.clientDomainDetail.nameserver3',
  'clients.clientDomainDetail.nameserver4',
  'clients.clientDomainDetail.nameserver5',
]

/** The cards of the details tab: the domain info beside the dates and billing. */
const DETAILS_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [
  [
    {
      key: DomainCard.Info,
      title: 'clients.clientDomainDetail.domainInfo',
      rows: [
        'clients.clientDomainDetail.firstPaymentAmount',
        'clients.clientDomainDetail.recurringAmount',
        'clients.clientDomainDetail.promotionCode',
        'clients.clientDomainDetail.subscriptionId',
      ],
    },
  ],
  [
    {
      key: DomainCard.Dates,
      title: 'clients.clientDomainDetail.datesBilling',
      rows: [
        'clients.clientDomainDetail.registrationPeriod',
        'shared.registrationDate',
        'shared.expiryDate',
        'shared.nextDueDate',
        'shared.paymentMethod',
        'shared.status',
      ],
    },
  ],
]

/** The cards of the registrar tab: the nameservers beside the registrar commands. */
export const REGISTRAR_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [
  [
    {
      key: DomainCard.Nameservers,
      title: 'clients.clientDomainDetail.nameservers',
      rows: NAMESERVER_LABELS,
    },
  ],
  [{ key: DomainCard.Commands, title: 'clients.clientDomainDetail.registrarCommands' }],
]

/** The cards of the management tab: the tool switches beside the admin notes. */
export const MANAGEMENT_COLUMNS: readonly (readonly SkeletonDetailCard[])[] = [
  [
    {
      key: DomainCard.Tools,
      title: 'clients.clientDomainDetail.managementTools',
      rows: [
        'clients.clientDomainDetail.dnsManagement',
        'clients.clientDomainDetail.emailForwarding',
        'clients.clientDomainDetail.idProtection',
        'clients.clientDomainDetail.autoRenew',
        'clients.clientDomainDetail.registrarLock',
      ],
    },
  ],
  [{ key: DomainCard.Notes, title: 'shared.adminNotes' }],
]

/** What the client domain screen is made of, as it opens (on the details tab). */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { crumbs: true, actions: 2, primaryAction: 1 },
  columns: DETAILS_COLUMNS,
}

export default schema
