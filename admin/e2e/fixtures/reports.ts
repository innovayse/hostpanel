/**
 * Fixtures for the reports: the ranked clients `GET /api/reports/top-clients-by-income` returns and the months
 * `GET /api/reports/annual-income` returns.
 */
import type { ReportMonthIncome } from '../../src/types/reportmonthincome'
import type { ReportTopClient } from '../../src/types/reporttopclient'

/** Two clients ranked by income. */
export const topClients: ReportTopClient[] = [
  { clientId: 7, clientName: 'Ada Lovelace', totalAmountIn: 1200, totalFees: 30, totalAmountOut: 100, balance: 1070, currency: 'USD' },
  { clientId: 8, clientName: 'Grace Hopper', totalAmountIn: 800, totalFees: 20, totalAmountOut: 0, balance: 780, currency: 'USD' },
]

/** Three months of income, enough for the annual income report to draw its chart and table. */
export const annualIncome: ReportMonthIncome[] = [
  { month: 'January', amount: 1200, currency: 'USD' },
  { month: 'February', amount: 900, currency: 'USD' },
  { month: 'March', amount: 1500, currency: 'USD' },
]
