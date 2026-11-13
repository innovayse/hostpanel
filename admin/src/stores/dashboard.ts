/**
 * Admin dashboard store: the headline statistics and the lists the panels under them read.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useDashboardApi } from '@/composables/apis/useDashboardApi'
import { useInvoicesApi } from '@/composables/apis/useInvoicesApi'
import { useOrdersApi } from '@/composables/apis/useOrdersApi'
import { useRenewalsApi } from '@/composables/apis/useRenewalsApi'
import { useReportsApi } from '@/composables/apis/useReportsApi'
import { useServersApi } from '@/composables/apis/useServersApi'
import { useSupportApi } from '@/composables/apis/useSupportApi'
import { currentYear } from '@/lib/reportsPeriod'
import { reportPath } from '@/lib/reportsQuery'
import { runAction } from '@/lib/runAction'
import type { DashboardOverview } from '@/types/dashboardoverview'
import type { DashboardStats } from '@/types/dashboardstats'
import { InvoiceStatus } from '@/types/invoice'
import { OrderStatus } from '@/types/order'
import { RenewalWindow } from '@/types/renewal'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import { TicketPriority, TicketStatus } from '@/types/ticket'

/** Rows the latest-invoices list shows: the four of the concept, which keep the dashboard within one screen at 1440x1000. */
const RECENT_PAGE_SIZE = 4

/** Rows an attention request asks for: the list shows only the total count, so one row is enough. */
const COUNT_PAGE_SIZE = 1

/** Open tickets scanned for high priority ones. */
const TICKET_SCAN_SIZE = 50

/**
 * Pinia store for the admin dashboard.
 *
 * Fetches and caches the statistics and the panel lists through the api composables; a refusal
 * shows the API's own sentence.
 */
export const useDashboardStore = defineStore('dashboard', () => {
  const api = useDashboardApi()
  const invoicesApi = useInvoicesApi()
  const ordersApi = useOrdersApi()
  const renewalsApi = useRenewalsApi()
  const reportsApi = useReportsApi()
  const serversApi = useServersApi()
  const supportApi = useSupportApi()

  /** Dashboard statistics, null until loaded. */
  const stats = ref<DashboardStats | null>(null)

  /** True while stats are being fetched. */
  const loading = ref(false)

  /** Sentence for the last failed call, null when no error. */
  const error = ref<string | null>(null)

  /** The lists the panels under the tiles read; each section fills as soon as its request answers. */
  const overview = ref<DashboardOverview>({
    income: [],
    recentInvoices: [],
    overdueInvoices: null,
    pendingOrders: null,
    expiringDomains: null,
    urgentTickets: [],
    servers: [],
  })

  /** True while the panel lists are being fetched. */
  const overviewLoading = ref(false)

  /** Sentence for the first panel list that failed to load, null when all loaded. */
  const overviewError = ref<string | null>(null)

  /**
   * Loads dashboard statistics from the backend.
   *
   * @returns Promise that resolves when the attempt is over (see `error`).
   */
  const fetchStats = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.fetchStats())
    if (result.ok) {
      stats.value = result.value
    }
    loading.value = false
  }

  /**
   * Runs one panel list request and hands its answer to `apply`; a failure leaves the section
   * empty and keeps the first sentence in `overviewError`.
   *
   * @param task - The request.
   * @param apply - Stores the answer in `overview`.
   * @returns Promise that resolves when the section is settled.
   */
  const loadSection = async <T>(task: () => Promise<T>, apply: (value: T) => void): Promise<void> => {
    /** Sentence of this section's failure; kept apart so parallel sections never clear each other's. */
    const failure = ref<string | null>(null)
    const result = await runAction(failure, task)
    if (result.ok) {
      apply(result.value)
    } else {
      overviewError.value ??= failure.value
    }
  }

  /**
   * Loads every panel list in parallel; the sections fill independently.
   *
   * @returns Promise that resolves when every section has answered (see `overviewError`).
   */
  const fetchOverview = async (): Promise<void> => {
    overviewLoading.value = true
    overviewError.value = null
    await Promise.all([
      loadSection(
        () => reportsApi.fetchReport<ReportMonthIncome[]>(reportPath('/reports/annual-income', { year: currentYear() })),
        (value) => { overview.value.income = value },
      ),
      loadSection(
        () => invoicesApi.list(1, RECENT_PAGE_SIZE),
        (value) => { overview.value.recentInvoices = value.items },
      ),
      loadSection(
        () => invoicesApi.list(1, COUNT_PAGE_SIZE, { status: InvoiceStatus.Overdue }),
        (value) => { overview.value.overdueInvoices = value.totalCount },
      ),
      loadSection(
        () => ordersApi.listOrders({ page: 1, pageSize: COUNT_PAGE_SIZE, status: OrderStatus.Pending }),
        (value) => { overview.value.pendingOrders = value.totalCount },
      ),
      loadSection(
        () => renewalsApi.listDomains(RenewalWindow.Next30Days, 1, COUNT_PAGE_SIZE),
        (value) => { overview.value.expiringDomains = value.totalCount },
      ),
      loadSection(
        () => supportApi.listTickets({ page: 1, pageSize: TICKET_SCAN_SIZE, status: TicketStatus.Open }),
        (value) => { overview.value.urgentTickets = value.items.filter((ticket) => ticket.priority === TicketPriority.High) },
      ),
      loadSection(
        () => serversApi.listServers(),
        (value) => { overview.value.servers = value },
      ),
    ])
    overviewLoading.value = false
  }

  return { stats, loading, error, fetchStats, overview, overviewLoading, overviewError, fetchOverview }
})
