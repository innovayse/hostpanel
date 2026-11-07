/**
 * Credit note flows: issuing a note from the Credit notes tab of a paid invoice (the form totals the lines at the invoice rate,
 * posts them and lists the new note), a note above what the invoice can take keeps the form open with the API sentence, the list
 * filters by status, and the page of a note voids it only with a reason and shows it voided.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { appliedNote, noteDetail, notesPage, overCreditRefusal, overCreditSentence, paidInvoiceDetail, voidedNote } from './fixtures/creditNotes'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('issuing a note from a paid invoice totals the lines at the invoice rate, posts them and lists the note', async ({ page }) => {
  let notes = notesPage([])
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/billing/1', paidInvoiceDetail),
      { method: HttpMethod.Get, path: '/api/billing/credit-notes', respond: () => ({ body: notes }) },
      {
        method: HttpMethod.Post,
        path: '/api/billing/1/credit-notes',
        respond: () => {
          notes = notesPage([appliedNote])
          return { status: 201, body: appliedNote.id }
        },
      },
    ],
  })

  await page.goto('/billing/invoices/1/edit')
  await page.getByTestId('billing-invoice-tab-credit-notes').click()
  await expect(page.getByTestId('billing-invoice-credit-notes-explain')).toContainText('not invoice credit')
  await expect(page.getByTestId('billing-invoice-creditable')).toContainText('120.00')

  await page.getByTestId('billing-invoice-credit-note-issue').click()
  await fieldOf(page, 'billing-credit-note-line-0-description').fill('Outage')
  await fieldOf(page, 'billing-credit-note-line-0-price').fill('50')
  await expect(page.getByTestId('billing-credit-note-total')).toContainText('60.00')
  await fieldOf(page, 'billing-credit-note-reason').fill('Service was down for two days')
  await page.getByTestId('billing-credit-note-submit').click()

  await expect(page.getByTestId('billing-invoice-credit-notes-feedback')).toContainText('issued')
  await expect(page.getByTestId('billing-invoice-credit-note-3')).toContainText('CN-0003')
  expect(api.callsTo(HttpMethod.Post, '/api/billing/1/credit-notes')[0]?.body).toEqual({
    items: [{ description: 'Outage', unitPrice: 50, quantity: 1 }],
    method: 'AccountCredit',
    reason: 'Service was down for two days',
  })
})

test('a note above what the invoice can take keeps the form open with the API sentence', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/billing/1', paidInvoiceDetail),
      json(HttpMethod.Get, '/api/billing/credit-notes', notesPage([])),
      { method: HttpMethod.Post, path: '/api/billing/1/credit-notes', respond: () => ({ status: 400, body: overCreditRefusal }) },
    ],
  })

  await page.goto('/billing/invoices/1/edit')
  await page.getByTestId('billing-invoice-tab-credit-notes').click()
  await page.getByTestId('billing-invoice-credit-note-issue').click()
  await page.getByTestId('billing-credit-note-use-invoice-lines').click()
  await fieldOf(page, 'billing-credit-note-reason').fill('Whole invoice')
  await page.getByTestId('billing-credit-note-submit').click()

  await expect(page.getByTestId('billing-credit-note-dialog-error')).toContainText(overCreditSentence)
  await expect(page.getByTestId('billing-credit-note-dialog')).toBeVisible()
})

test('a form that credits more than the invoice can take is stopped before the call', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/billing/1', { ...paidInvoiceDetail, creditableAmount: 30 }),
      json(HttpMethod.Get, '/api/billing/credit-notes', notesPage([])),
    ],
  })

  await page.goto('/billing/invoices/1/edit')
  await page.getByTestId('billing-invoice-tab-credit-notes').click()
  await page.getByTestId('billing-invoice-credit-note-issue').click()
  await fieldOf(page, 'billing-credit-note-line-0-description').fill('Outage')
  await fieldOf(page, 'billing-credit-note-line-0-price').fill('50')
  await fieldOf(page, 'billing-credit-note-reason').fill('Too much')
  await page.getByTestId('billing-credit-note-submit').click()

  await expect(page.getByTestId('billing-credit-note-over')).toBeVisible()
  expect(api.callsTo(HttpMethod.Post, '/api/billing/1/credit-notes')).toHaveLength(0)
})

test('the list filters by status and opens a note', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/billing/credit-notes',
        respond: (call) => ({ body: notesPage(call.search.includes('status=Voided') ? [voidedNote] : [appliedNote, { ...voidedNote, id: 4, number: 'CN-0004' }]) }),
      },
      json(HttpMethod.Get, '/api/billing/credit-notes/3', noteDetail(appliedNote)),
    ],
  })

  await page.goto('/billing/credit-notes')
  await expect(page.getByTestId('billing-credit-notes-explain')).toContainText('not invoice credit')
  await expect(page.getByTestId('billing-credit-notes-table')).toContainText('CN-0003')
  await expect(page.getByTestId('billing-credit-notes-table')).toContainText('CN-0004')

  await page.getByTestId('billing-credit-notes-status-filter').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Voided' }).click()
  await expect(page.getByTestId('billing-credit-notes-table')).not.toContainText('Applied')
  expect(api.callsTo(HttpMethod.Get, '/api/billing/credit-notes').some((call) => call.search.includes('status=Voided'))).toBe(true)

  await page.goto('/billing/credit-notes/3')
  await expect(page.getByTestId('billing-credit-note-view')).toContainText('Credit note CN-0003')
  await expect(page.getByTestId('billing-credit-note-items')).toContainText('Credit for hosting')
})

test('voiding a note asks for a reason, posts it and shows the note voided', async ({ page }) => {
  let current = noteDetail(appliedNote)
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/billing/credit-notes/3', respond: () => ({ body: current }) },
      {
        method: HttpMethod.Post,
        path: '/api/billing/credit-notes/3/void',
        respond: () => {
          current = noteDetail(voidedNote)
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/billing/credit-notes/3')
  await page.getByTestId('billing-credit-note-void').click()
  await page.getByTestId('dialog-confirm').click()
  await expect(page.getByTestId('billing-credit-note-void-reason-error')).toBeVisible()
  await fieldOf(page, 'billing-credit-note-void-reason').fill('Issued to the wrong invoice')
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId('billing-credit-note-feedback')).toContainText('voided')
  await expect(page.getByTestId('billing-credit-note-voided')).toContainText('Issued to the wrong invoice')
  await expect(page.getByTestId('billing-credit-note-void')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Post, '/api/billing/credit-notes/3/void')[0]?.body).toEqual({ reason: 'Issued to the wrong invoice' })
})
