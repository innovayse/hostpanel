/**
 * Flow: the client's credit notes.
 *
 * The Credit notes page lists the notes issued to the client, newest first, with where the value went and the state, and says plainly
 * that a credit note is not the credit balance. Each note downloads as a PDF named after its number. The navigation reaches the page
 * next to Invoices and Credit.
 */
import { expect, test } from './support/test'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('the page lists the notes issued to the client and says a credit note is not the credit balance', async ({ page }) => {
  await page.goto('/client/credit-notes')
  await waitForHydration(page)

  await expect(page.getByTestId('credit-notes-explain')).toContainText('not the same as your credit balance')
  await expect(page.getByTestId('credit-note-number-41')).toHaveText('CN-0041')
  await expect(page.getByTestId('credit-note-method-41')).toContainText('credit balance')
  await expect(page.getByTestId('credit-note-total-41')).toContainText('60.00')
  await expect(page.getByTestId('credit-note-status-40')).toContainText('Voided')
  await expect(page.getByTestId('credit-note-method-40')).toContainText('payment method')
  await expect(page.getByTestId('credit-note-invoice-41')).toHaveAttribute('href', '/client/invoices/12')
})

test('a note downloads as a PDF named after its number', async ({ page, request }) => {
  await page.goto('/client/credit-notes')
  await waitForHydration(page)

  const download = page.waitForEvent('download')
  await page.getByTestId('credit-note-download-41').click()

  expect((await download).suggestedFilename()).toBe('credit-note-CN-0041.pdf')
  const asked = (await stubRequests(request)).filter(r => r.method === 'GET' && r.path === '/api/me/credit-notes/41/pdf')
  expect(asked).toHaveLength(1)
})

test('the navigation reaches the page from the client area', async ({ page }) => {
  await page.goto('/client/dashboard')
  await waitForHydration(page)

  await page.getByTestId('nav-client-credit-notes').click()

  await expect(page).toHaveURL(/\/client\/credit-notes$/)
  await expect(page.getByTestId('client-credit-notes')).toBeVisible()
})
