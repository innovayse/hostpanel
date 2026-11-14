import type { InvoiceStatus } from '@/types/invoice'
import type { TicketPriority } from '@/types/ticket'

/** Closed set of periods the dashboard charts cover; the values are the names the API takes. */
export enum DashboardPeriod {
  /** The last 30 days, today included. */
  Days30 = 'Days30',
  /** The last 90 days, today included. */
  Days90 = 'Days90',
}

/** One day of a money series. */
export interface DashboardDailyAmount {
  /** Calendar day in the host time zone, `yyyy-MM-dd`. */
  date: string
  /** Amount of the day in the base currency, zero for a day with no movement. */
  amount: number
}

/** One day of a count series. */
export interface DashboardDailyCount {
  /** Calendar day in the host time zone, `yyyy-MM-dd`. */
  date: string
  /** How many happened that day, zero for a quiet day. */
  count: number
}

/** Income of the period with its comparison against the period before. */
export interface DashboardIncome {
  /** One amount per day of the period, oldest first, every day present. */
  days: DashboardDailyAmount[]
  /** Income of the whole period in the base currency. */
  total: number
  /** Income of the period of the same length right before it. */
  previousTotal: number
  /** Change of `total` against `previousTotal` in percent; null when the previous period earned nothing. */
  changePercent: number | null
  /** The total spread over the days of the period, the target line of the chart. */
  dailyAverage: number
}

/** The invoices raised in the period that stand in one status. */
export interface DashboardInvoiceStatusCount {
  /** The status; a partially refunded invoice counts as paid. */
  status: InvoiceStatus
  /** How many invoices stand in it. */
  count: number
  /** Their totals added up in the base currency. */
  amount: number
}

/** The open tickets of one priority. */
export interface DashboardTicketPriorityCount {
  /** The priority. */
  priority: TicketPriority
  /** How many open tickets have it. */
  count: number
}

/** Everything the dashboard charts draw for one period (`GET /api/admin/dashboard/charts`). */
export interface DashboardCharts {
  /** ISO 4217 code of the base currency every amount is in. */
  currency: string
  /** Income per day with its total and change. */
  income: DashboardIncome
  /** Orders placed per day, every day present. */
  newOrders: DashboardDailyCount[]
  /** Client accounts created per day, every day present. */
  newClients: DashboardDailyCount[]
  /** Paid, unpaid, overdue and cancelled invoices raised in the period, in that order. */
  invoicesByStatus: DashboardInvoiceStatusCount[]
  /** Open tickets by priority, the highest first. */
  openTickets: DashboardTicketPriorityCount[]
}
