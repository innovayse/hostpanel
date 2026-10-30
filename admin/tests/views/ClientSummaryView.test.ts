/**
 * Mount tests for ClientSummaryView: it renders against the real kit, settles on an API refusal
 * with a Retry, and saving the admin notes reports its outcome under the Save button.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientSummaryView from '@/views/ClientSummaryView.vue'
import { useClientsStore } from '@/stores/clients'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import type { ClientSummaryData } from '@/types/client'

/** Client 7 as the detail endpoint returns it. */
const client = () =>
  json({
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.com',
    status: 'Active',
    createdAt: '2026-01-01T00:00:00Z',
    contacts: [],
    address: {},
    preferences: { adminNotes: '' },
    notifications: { general: true, invoice: true, support: true, product: true, domain: true, affiliate: true },
    settings: {
      overdueNotices: true,
      taxExempt: false,
      separateInvoices: false,
      disableCcProcessing: false,
      marketingOptIn: false,
      statusUpdate: true,
      allowSso: true,
    },
  })

/** No invoices of a status: the count and the sum. */
const none = { count: 0, total: 0 }

/** The summary figures of client 7 as `GET /api/clients/7/summary` returns them. */
const summary: ClientSummaryData = {
  invoices: {
    draft: none,
    unpaid: none,
    paid: { count: 2, total: 40 },
    overdue: none,
    cancelled: none,
    refunded: none,
    partiallyRefunded: { count: 1, total: 15 },
  },
  transactions: { grossRevenue: 40, clientExpenses: 0, totalFees: 0, netIncome: 40 },
  creditBalance: 0,
  counts: {
    activeServicesCount: 1,
    totalServicesCount: 1,
    totalDomainsCount: 0,
    acceptedQuotesCount: 0,
    totalQuotesCount: 0,
    openTicketsCount: 0,
    totalTicketsCount: 0,
  },
  recentEmails: [],
}

/**
 * Routes of the summary screen beside the notes save: the summary figures before the client
 * detail, whose path is a prefix of the summary's.
 *
 * @param save - Answer to the notes save.
 * @returns The route table for `stubRoutes`.
 */
const summaryRoutes = (save: () => Response): Record<string, () => Response> => ({
  'PUT /api/clients/7': save,
  'GET /api/clients/7/summary': () => json(summary),
  'GET /api/clients/7': client,
})

/**
 * Mounts the summary of client 7 with the client loaded, the way the client layout leaves it.
 *
 * @returns The wrapper.
 */
const mountSummary = async () => {
  const { wrapper } = await mountRouted(ClientSummaryView, '/clients/:id/summary', '/clients/7/summary')
  await useClientsStore().fetchById(7)
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientSummaryView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientSummaryView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientSummaryView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientSummaryView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-summary-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('lists the partially refunded invoices on their own row of the billing card', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    const billing = wrapper.get('[data-testid="client-summary-billing"]').text()
    expect(billing).toContain('Partially Refunded')
    expect(billing).toMatch(/Partially Refunded\s*1/)
  })

  it('reports saved notes under the Save button', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-summary-notes-input"]').setValue('Prefers mail')
    await wrapper.get('[data-testid="client-summary-notes-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-summary-notes-feedback"]').text()).toBe('Notes saved.')
  })

  it('shows the API sentence under the Save button when the notes are refused', async () => {
    stubRoutes(summaryRoutes(() => problem('Notes are too long.', 400)))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-summary-notes-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-summary-notes-feedback"]').text()).toBe('Notes are too long.')
  })
})
