/**
 * Skeleton schema of the upcoming renewals screen: a header over the strip of the three windows, then one card each for
 * the services renewing, the domains expiring and the renewal lines left claimed, every one holding its table.
 *
 * The screen is a composite. `UpcomingRenewalsView` draws the tab strip, the cards and the heads of their tables from this
 * data (the cards hold the real tables and their pagers), and `UiSkeletonOf` draws the same blocks while the view chunk is
 * downloading. The claims card also holds a line of explanation that is page text of the real card and not drawn here.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { RenewalWindow } from '@/types/renewal'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, SkeletonTable, TabsSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** Key of the card of the services table, the suffix of its `card-<key>` slot. */
export const RENEWALS_SERVICES_CARD = 'services'

/** Key of the card of the domains table. */
export const RENEWALS_DOMAINS_CARD = 'domains'

/** Key of the card of the claimed lines table. */
export const RENEWALS_CLAIMS_CARD = 'claims'

/** The strip of the three windows, one tab per {@link RenewalWindow}. */
export const RENEWALS_WINDOW_TABS_PART: TabsSkeletonPart = {
  kind: SkeletonPartKind.Tabs,
  tabs: [
    { key: RenewalWindow.Next30Days, label: 'billing.renewals.window.Next30Days' },
    { key: RenewalWindow.Next60Days, label: 'billing.renewals.window.Next60Days' },
    { key: RenewalWindow.Next90Days, label: 'billing.renewals.window.Next90Days' },
  ],
}

/** The services table: client, product, renewal date, cycle, amount, the auto renew switch and the row action. */
export const RENEWALS_SERVICES_TABLE: SkeletonTable = {
  pagination: true,
  columns: [
    { key: 'client', label: 'shared.client' },
    { key: 'product', label: 'billing.renewals.product', size: UiColumnSize.Grow },
    { key: 'renewsOn', label: 'billing.renewals.renewsOn', kind: UiColumnKind.Date },
    { key: 'billingCycle', label: 'shared.billingCycle' },
    { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
    { key: 'autoRenew', label: 'shared.autoRenew', kind: UiColumnKind.Status },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The domains table: client, domain, expiry date, amount, the auto renew switch and the row action. */
export const RENEWALS_DOMAINS_TABLE: SkeletonTable = {
  pagination: true,
  columns: [
    { key: 'client', label: 'shared.client' },
    { key: 'domain', label: 'shared.domain', size: UiColumnSize.Grow },
    { key: 'expires', label: 'billing.renewals.expires', kind: UiColumnKind.Date },
    { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
    { key: 'autoRenew', label: 'shared.autoRenew', kind: UiColumnKind.Status },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The claimed lines table: invoice, client, the line, when it was claimed and the row action. */
export const RENEWALS_CLAIMS_TABLE: SkeletonTable = {
  columns: [
    { key: 'invoice', label: 'shared.invoice', kind: UiColumnKind.Id },
    { key: 'client', label: 'shared.client' },
    { key: 'line', label: 'billing.renewals.line', size: UiColumnSize.Grow },
    { key: 'claimedAt', label: 'billing.renewals.claimedAt', kind: UiColumnKind.Date },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The card of the services table. */
export const RENEWALS_SERVICES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: RENEWALS_SERVICES_CARD, title: 'billing.renewals.servicesHeading', table: RENEWALS_SERVICES_TABLE }]],
}

/** The card of the domains table. */
export const RENEWALS_DOMAINS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: RENEWALS_DOMAINS_CARD, title: 'billing.renewals.domainsHeading', table: RENEWALS_DOMAINS_TABLE }]],
}

/** The card of the claimed lines table. */
export const RENEWALS_CLAIMS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: RENEWALS_CLAIMS_CARD, title: 'billing.renewals.claimsHeading', table: RENEWALS_CLAIMS_TABLE }]],
}

/** What the upcoming renewals screen is made of. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.BillingRenewals), subtitle: 'billing.renewals.subtitle' },
  parts: [RENEWALS_WINDOW_TABS_PART, RENEWALS_SERVICES_PART, RENEWALS_DOMAINS_PART, RENEWALS_CLAIMS_PART],
}

export default schema
