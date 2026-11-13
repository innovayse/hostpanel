/**
 * Dashboard flow: the headline numbers the API returns appear on their tiles, and a refused load
 * shows the API's own sentence instead of the grid.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { dashboardStats } from './fixtures/dashboard'
import { json, stubApi } from './support/stubApi'

test('the dashboard shows each headline number on its tile', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats)] })

  await page.goto('/dashboard')

  await expect(page.getByTestId('dashboard-stats')).toBeVisible()
  await expect(page.getByTestId('dashboard-stat-activeServices')).toContainText(String(dashboardStats.activeServices))
  await expect(page.getByTestId('dashboard-stat-overdueInvoices')).toContainText(String(dashboardStats.overdueInvoices))
  await expect(page.getByTestId('dashboard-stat-openTickets')).toContainText(String(dashboardStats.openTickets))
  await expect(page.getByTestId('nav-clients-count')).toHaveText(String(dashboardStats.totalClients))
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
