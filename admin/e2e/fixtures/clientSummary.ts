/**
 * Fixtures for the client summary page: the rollups `GET /api/clients/:id/summary` answers with.
 */
import type { ClientSummaryData } from '../../src/types/client'

/** One invoice bucket with nothing in it. */
const none = { count: 0, total: 0 }

/** A client with no invoices, services, tickets or emails: every rollup is zero. */
export const emptyClientSummary: ClientSummaryData = {
  invoices: {
    draft: none,
    unpaid: none,
    paid: none,
    overdue: none,
    cancelled: none,
    refunded: none,
    partiallyRefunded: none,
  },
  transactions: {
    grossRevenue: 0,
    clientExpenses: 0,
    totalFees: 0,
    netIncome: 0,
  },
  creditBalance: 0,
  counts: {
    activeServicesCount: 0,
    totalServicesCount: 0,
    totalDomainsCount: 0,
    acceptedQuotesCount: 0,
    totalQuotesCount: 0,
    openTicketsCount: 0,
    totalTicketsCount: 0,
  },
  recentEmails: [],
}
