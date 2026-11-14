/**
 * Fixture for the dashboard tests: the chart series `GET /api/admin/dashboard/charts` answers with.
 */
import type { DashboardCharts } from '@/types/dashboardcharts'
import { InvoiceStatus } from '@/types/invoice'
import { TicketPriority } from '@/types/ticket'

/**
 * Builds the chart series of a three day period, with distinct small numbers so a card reading the wrong field is caught.
 *
 * @returns The series, the way the endpoint answers.
 */
export const dashboardCharts = (): DashboardCharts => ({
  currency: 'USD',
  income: {
    days: [
      { date: '2026-03-10', amount: 0 },
      { date: '2026-03-11', amount: 300 },
      { date: '2026-03-12', amount: 150 },
    ],
    total: 450,
    previousTotal: 300,
    changePercent: 50,
    dailyAverage: 150,
  },
  newOrders: [
    { date: '2026-03-10', count: 0 },
    { date: '2026-03-11', count: 2 },
    { date: '2026-03-12', count: 1 },
  ],
  newClients: [
    { date: '2026-03-10', count: 4 },
    { date: '2026-03-11', count: 0 },
    { date: '2026-03-12', count: 1 },
  ],
  invoicesByStatus: [
    { status: InvoiceStatus.Paid, count: 7, amount: 700 },
    { status: InvoiceStatus.Unpaid, count: 3, amount: 300 },
    { status: InvoiceStatus.Overdue, count: 2, amount: 200 },
    { status: InvoiceStatus.Cancelled, count: 1, amount: 100 },
  ],
  openTickets: [
    { priority: TicketPriority.High, count: 2 },
    { priority: TicketPriority.Medium, count: 5 },
    { priority: TicketPriority.Low, count: 0 },
  ],
})
