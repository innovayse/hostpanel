import type { Invoice } from '@/types/invoice'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import type { ServerDto } from '@/types/server'
import type { TicketListItem } from '@/types/ticket'

/** The lists the dashboard panels read besides the headline statistics; each is empty until loaded. */
export interface DashboardOverview {
  /** Income per month of the current year, January first. */
  income: ReportMonthIncome[]
  /** The latest invoices, newest first. */
  recentInvoices: Invoice[]
  /** Number of overdue invoices, null until loaded. */
  overdueInvoices: number | null
  /** Number of pending orders waiting for staff, null until loaded. */
  pendingOrders: number | null
  /** Number of domains expiring within 30 days, null until loaded. */
  expiringDomains: number | null
  /** Open tickets of high priority. */
  urgentTickets: TicketListItem[]
  /** Servers with their capacity and last test result. */
  servers: ServerDto[]
}
