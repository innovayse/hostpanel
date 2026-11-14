/**
 * Dashboard flow: the headline numbers the API returns appear on their tiles, the 30 and 90 day switch reloads the
 * charts and the revenue tile, and a refused load shows the API's own sentence instead of the grid.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { dashboardCharts, dashboardStats } from './fixtures/dashboard'
import { json, stubApi } from './support/stubApi'
import { DashboardPeriod } from '../src/types/dashboardcharts'

test('the dashboard shows each headline number on its tile', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats)] })

  await page.goto('/dashboard')

  await expect(page.getByTestId('dashboard-stats')).toBeVisible()
  await expect(page.getByTestId('dashboard-stat-activeServices')).toContainText(String(dashboardStats.activeServices))
  await expect(page.getByTestId('dashboard-stat-overdueInvoices')).toContainText(String(dashboardStats.overdueInvoices))
  await expect(page.getByTestId('dashboard-stat-openTickets')).toContainText(String(dashboardStats.openTickets))
  await expect(page.getByTestId('nav-clients-count')).toHaveText(String(dashboardStats.totalClients))
})

test('the 30 and 90 day switch reloads the charts and the revenue tile', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats),
      {
        method: HttpMethod.Get,
        path: '/api/admin/dashboard/charts',
        respond: (call) => ({ body: dashboardCharts[call.search.includes(DashboardPeriod.Days90) ? DashboardPeriod.Days90 : DashboardPeriod.Days30] }),
      },
    ],
  })

  await page.goto('/dashboard')

  await expect(page.getByTestId('dashboard-stat-periodRevenue')).toContainText('$1,200')
  await expect(page.getByTestId('dashboard-revenue-total')).toContainText('$1,200')
  await expect(page.getByTestId('dashboard-invoice-status-Paid')).toContainText('4')
  await expect(page.getByTestId('dashboard-tickets')).toBeVisible()

  await page.getByTestId('dashboard-period-90').click()

  await expect(page.getByTestId('dashboard-stat-periodRevenue')).toContainText('$3,600')
  await expect(page.getByTestId('dashboard-revenue-total')).toContainText('$3,600')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/dashboard/charts').map((call) => call.search)).toEqual(['?period=Days30', '?period=Days90'])
})

test('a refused load shows the API sentence instead of the tiles', async ({ page }) => {
  const sentence = 'You are not allowed to see the dashboard.'
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/dashboard/stats', { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: sentence }, 403)],
  })

  await page.goto('/dashboard')

  await expect(page.getByTestId('dashboard-error')).toContainText(sentence)
  await expect(page.getByTestId('dashboard-stats')).toHaveCount(0)
})
