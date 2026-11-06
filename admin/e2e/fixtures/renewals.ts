/**
 * Fixtures for the upcoming renewals screen: one service, one domain and one claimed domain renewal line.
 */
import type { DomainRegistration } from '../../src/types/domain'
import { DomainStatus } from '../../src/types/domain'
import type { PaginatedList } from '../../src/types/paginatedlist'
import { BillingCycle } from '../../src/types/service'
import type { ClaimedDomainRenewal, UpcomingServiceRenewal } from '../../src/types/renewal'

/** Wraps rows in the single page shape every list endpoint answers with. */
const pageOf = <T>(items: T[]): PaginatedList<T> => ({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

/** `GET /api/services/renewals/upcoming`: one service renewing soon. */
export const upcomingServicesPage = pageOf<UpcomingServiceRenewal>([
  {
    id: 4, clientId: 9, clientName: 'Ada Lovelace', productName: 'Starter', domain: null, amount: 5,
    priceCurrency: 'USD', billingCycle: BillingCycle.Monthly, nextRenewalAt: '2027-01-10T00:00:00Z', autoRenew: true,
  },
])

/** `GET /api/domains/renewals/upcoming`: one domain expiring soon. */
export const upcomingDomainsPage = pageOf<DomainRegistration>([
  {
    id: 7, clientId: 10, clientName: 'Grace Hopper', name: 'example.test', regPeriodYears: 1, registrar: null,
    recurringAmount: 12, priceCurrency: 'USD', nextDueDate: '2027-01-12T00:00:00Z', expiresAt: '2027-01-12T00:00:00Z',
    status: DomainStatus.Active, autoRenew: false,
  },
])

/** `GET /api/billing/renewal-claims`: one line left claimed. */
export const claimedLines: ClaimedDomainRenewal[] = [
  {
    invoiceId: 5, itemId: 11, clientId: 9, clientName: 'Ada Lovelace', registeredDomainId: 7, description: 'example.test renewal',
    periodStart: '2027-01-12T00:00:00Z', periodEnd: '2028-01-12T00:00:00Z', claimedAt: '2027-01-01T10:00:00Z',
  },
]
