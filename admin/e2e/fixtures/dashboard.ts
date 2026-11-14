/**
 * Fixtures for the dashboard: the headline numbers `GET /api/admin/dashboard/stats` answers with and the chart series
 * `GET /api/admin/dashboard/charts` answers with for each period.
 */
import type { DashboardCharts } from '../../src/types/dashboardcharts'
import { DashboardPeriod } from '../../src/types/dashboardcharts'
import type { DashboardStats } from '../../src/types/dashboardstats'
import { InvoiceStatus } from '../../src/types/invoice'
import { TicketPriority } from '../../src/types/ticket'

/** Distinct small numbers, so a tile showing the wrong field is caught. */
export const dashboardStats: DashboardStats = {
  activeServices: 37,
  overdueInvoices: 5,
  openTickets: 11,
  totalClients: 64,
}

/**
 * Builds the chart series of one period, with an income total that differs per period so a tile showing the wrong
 * period is caught.
 *
 * @param total - Income of the whole period.
 * @returns The series, the way the endpoint answers.
 */
const chartsWithIncome = (total: number): DashboardCharts => ({
  currency: 'USD',
  income: {
    days: [{ date: '2026-03-12', amount: total }],
    total,
    previousTotal: total / 2,
    changePercent: 100,
    dailyAverage: total,
  },
  newOrders: [{ date: '2026-03-12', count: 3 }],
  newClients: [{ date: '2026-03-12', count: 2 }],
  invoicesByStatus: [
    { status: InvoiceStatus.Paid, count: 4, amount: 400 },
    { status: InvoiceStatus.Unpaid, count: 1, amount: 100 },
    { status: InvoiceStatus.Overdue, count: 1, amount: 50 },
    { status: InvoiceStatus.Cancelled, count: 0, amount: 0 },
  ],
  openTickets: [
    { priority: TicketPriority.High, count: 1 },
    { priority: TicketPriority.Medium, count: 2 },
    { priority: TicketPriority.Low, count: 3 },
  ],
})

/** The chart series per period; the revenue total of each is shown on the revenue tile. */
export const dashboardCharts: Record<DashboardPeriod, DashboardCharts> = {
  [DashboardPeriod.Days30]: chartsWithIncome(1200),
  [DashboardPeriod.Days90]: chartsWithIncome(3600),
}
