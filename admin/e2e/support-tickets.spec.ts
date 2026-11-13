/**
 * Support tickets list flow: the status chosen in the filter dialog is sent to the API and kept in the
 * address, ticking tickets reveals the bulk bar and "close" posts the ticked ids, Shift extends a range and
 * Escape clears it, and a refused load shows the API's sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { billingListed, openListed, ticketsPage } from './fixtures/tickets'
import { json, stubApi } from './support/stubApi'

test('choosing a status in the filter dialog asks the API for that status', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/tickets', ticketsPage)] })

  await page.goto('/support/tickets')
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toContainText(openListed.subject)
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toContainText(openListed.clientName)

  await page.getByTestId('support-tickets-filter').click()
  await page.getByTestId('support-tickets-filter-status').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Closed' }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Closed/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Closed')
  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/tickets').at(-1)?.search ?? '').toContain('status=Closed')
})

test('closing the ticked tickets posts their ids and reloads the list', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/tickets', ticketsPage), { method: HttpMethod.Post, path: '/api/tickets/bulk', respond: () => ({ status: 204 }) }],
  })

  await page.goto('/support/tickets')
  await expect(page.getByTestId('support-tickets-bulk')).toHaveCount(0)

  await page.getByTestId(`support-tickets-select-${billingListed.id}`).check()
  await expect(page.getByTestId('support-tickets-bulk')).toBeVisible()
  await page.getByTestId('support-tickets-bulk-close').click()

  await expect(page.getByTestId('support-tickets-bulk')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Post, '/api/tickets/bulk')[0]?.body).toEqual({ ticketIds: [billingListed.id], action: 'close' })
  expect(api.callsTo(HttpMethod.Get, '/api/tickets').length).toBeGreaterThanOrEqual(2)
})

test('Shift extends the selection to a range, the bar counts it and Escape clears it', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/tickets', ticketsPage)] })

  await page.goto('/support/tickets')
  await page.getByTestId(`support-tickets-select-${openListed.id}`).click()
  await expect(page.getByTestId('ui-bulk-bar-count')).toHaveText('1 selected')
  await expect(page.getByTestId('support-tickets-select-all')).toHaveAttribute('aria-checked', 'mixed')

  await page.getByTestId(`support-tickets-select-${billingListed.id}`).click({ modifiers: ['Shift'] })
  await expect(page.getByTestId('ui-bulk-bar-count')).toHaveText('2 selected')
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByTestId('support-tickets-select-all')).toHaveAttribute('aria-checked', 'true')

  await page.keyboard.press('Escape')
  await expect(page.getByTestId('support-tickets-bulk')).toHaveCount(0)
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toHaveAttribute('aria-selected', 'false')
})

test('a refused load shows the API sentence', async ({ page }) => {
  const sentence = 'You are not allowed to list tickets.'
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/tickets', { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: sentence }, 403)],
  })

  await page.goto('/support/tickets')

  await expect(page.getByTestId('support-tickets-error')).toContainText(sentence)
})
