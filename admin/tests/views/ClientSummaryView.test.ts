/**
 * Mount tests for ClientSummaryView: it renders against the real kit, settles on an API refusal
 * with a Retry, and saving the admin notes reports its outcome under the Save button.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientSummaryView from '@/views/ClientSummaryView.vue'
import { SUMMARY_CARDS_PART, SUMMARY_DOMAINS_TABLE, SUMMARY_QUOTES_TABLE, SUMMARY_SERVICES_TABLE } from '@/views/ClientSummaryView.schema'
import { useClientsStore } from '@/stores/clients'
import { mountClientView } from '../helpers/clientViews'
import { createTestI18n } from '../helpers/i18n'
import { callOf, json, problem } from '../helpers/fetch'
import { quoteRowFixture } from '../helpers/billingFixtures'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { CurrencyCode } from '@/types/currency'
import type { ClientSummaryData } from '@/types/client'
import type { InvoiceStatusCount } from '@/types/invoice'
import type { ClientExternalAccount } from '@/types/clientexternalaccount'
import { ClientVatStatus } from '@/types/clientvat'
import type { ClientVat } from '@/types/clientvat'
import { useToastStore } from '@/stores/toast'
import { toastMessages } from '../helpers/toasts'

/** A client's tax ID with the last completed check of it, as the detail endpoint carries them. */
interface ClientVatCase {
  /** The client's VAT number (Tax ID). */
  taxId: string
  /** The last completed check of it. */
  check: ClientVat
}

/**
 * Client 7 as the detail endpoint returns it.
 *
 * @param externalAccount - The link the client holds, null for none.
 * @param currency - The client's billing currency, null for none.
 * @param vat - The tax ID and the last completed check of it, null for a client with no number.
 * @returns The response.
 */
const client = (externalAccount: ClientExternalAccount | null = null, currency: string | null = null, vat: ClientVatCase | null = null) =>
  json({
    externalAccount,
    vat: vat?.check ?? { status: null, checkedAt: null, name: null, address: null },
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.com',
    status: 'Active',
    createdAt: '2026-01-01T00:00:00Z',
    contacts: [],
    address: {},
    preferences: { adminNotes: '', currency },
    notifications: { general: true, invoice: true, support: true, product: true, domain: true, affiliate: true },
    settings: {
      overdueNotices: true,
      taxExempt: false,
      taxId: vat?.taxId ?? null,
      separateInvoices: false,
      disableCcProcessing: false,
      marketingOptIn: false,
      statusUpdate: true,
      allowSso: true,
    },
  })

/** No invoices of a status: the count and the sum. */
const none: InvoiceStatusCount = { count: 0, totals: [] }

/** The summary figures of client 7 as `GET /api/clients/7/summary` returns them. */
const summary: ClientSummaryData = {
  invoices: {
    draft: none,
    unpaid: none,
    paid: { count: 2, totals: [{ amount: 40, currency: 'USD' }] },
    overdue: none,
    cancelled: none,
    refunded: none,
    partiallyRefunded: { count: 1, totals: [{ amount: 15, currency: 'USD' }] },
  },
  transactions: {
    grossRevenue: [{ amount: 40, currency: 'USD' }],
    clientExpenses: [],
    totalFees: [],
    netIncome: [{ amount: 40, currency: 'USD' }],
  },
  creditBalance: { amount: 0, currency: 'USD' },
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
 * @param link - Answer to the external account save or clear.
 * @param detail - Answer to the client detail read.
 * @returns The route table for `stubRoutes`.
 */
const summaryRoutes = (
  save: () => Response,
  link: () => Response = () => new Response(null, { status: 204 }),
  detail: () => Response = client,
): Record<string, () => Response> => ({
  'PUT /api/clients/7/external-account': link,
  'PUT /api/clients/7': save,
  'GET /api/clients/7/summary': () => json(summary),
  'GET /api/clients/7': detail,
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

/** Every information card of the schema; the three tables are tables of their own, not cards. */
const schemaCards = SUMMARY_CARDS_PART.columns.flat()

/** The English catalogue, to read the titles the schema names. */
const english = createTestI18n()

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Reads back the external account writes a stubbed `fetch` received.
 *
 * @param mock - The mock returned by `stubRoutes`.
 * @returns The decoded PUT calls to the external account endpoint, in order.
 */
const externalAccountPuts = (mock: ReturnType<typeof stubRoutes>) =>
  mock.mock.calls
    .map((_, index) => callOf(mock, index))
    .filter((call) => call.method === 'PUT' && call.url === '/api/clients/7/external-account')

describe('ClientSummaryView', () => {
  it('shows the VAT number card with the status the client holds', async () => {
    const check = { status: ClientVatStatus.Valid, checkedAt: '2026-11-10T09:30:00Z', name: 'Acme GmbH', address: null }
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 }), undefined, () => client(null, null, { taxId: 'DE123456789', check })))
    const wrapper = await mountSummary()

    expect(wrapper.get('[data-testid="client-vat-number"]').text()).toBe('DE123456789')
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Valid')
    expect(wrapper.get('[data-testid="client-vat-name"]').text()).toBe('Acme GmbH')
  })

  it('says there is no VAT number to check for a client without one', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    expect(wrapper.find('[data-testid="client-vat-none"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="client-vat-check"]').attributes('disabled')).toBeDefined()
  })

  it('runs the check again and shows the status the API now holds', async () => {
    let checked = false
    const unchecked = { status: null, checkedAt: null, name: null, address: null }
    const valid = { status: ClientVatStatus.Valid, checkedAt: '2026-11-10T09:30:00Z', name: 'Acme GmbH', address: null }
    const mock = stubRoutes({
      ...summaryRoutes(
        () => new Response(null, { status: 204 }),
        undefined,
        () => client(null, null, { taxId: 'DE123456789', check: checked ? valid : unchecked }),
      ),
      'POST /api/clients/7/vat-check': () => {
        checked = true
        return new Response(null, { status: 204 })
      },
    })
    const wrapper = await mountSummary()
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Not checked')

    await wrapper.get('[data-testid="client-vat-check"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.map((_, index) => callOf(mock, index)).some((call) => call.method === 'POST' && call.url === '/api/clients/7/vat-check')).toBe(true)
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Valid')
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['VAT number checked.'])
  })

  it('toasts the API sentence when the service gave no verdict and keeps the status', async () => {
    const valid = { status: ClientVatStatus.Valid, checkedAt: '2026-11-10T09:30:00Z', name: 'Acme GmbH', address: null }
    stubRoutes({
      ...summaryRoutes(
        () => new Response(null, { status: 204 }),
        undefined,
        () => client(null, null, { taxId: 'DE123456789', check: valid }),
      ),
      'POST /api/clients/7/vat-check': () => problem('The VAT number service is not answering right now.', 503),
    })
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-vat-check"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('The VAT number service is not answering right now.')
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Valid')
  })

  it('draws the cards of the schema with their captions and bars while the summary loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountRouted(ClientSummaryView, '/clients/:id/summary', '/clients/7/summary')

    const cards = schemaCards
    expect(cards.map(card => wrapper.find(`[data-testid="ui-detail-card-${card.key}"]`).exists())).not.toContain(false)
    expect(wrapper.get('[data-testid="ui-detail-card-info"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="ui-detail-card-info"]').text()).toContain('First Name')
    expect(wrapper.get('[data-testid="ui-detail-card-billing"]').text()).toContain('Paid')
    expect(wrapper.find('[data-testid="client-summary-billing-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
  })

  it('draws every card of the schema, with its title, once the summary and the client are loaded', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    for (const card of schemaCards) {
      const drawn = wrapper.get(`[data-testid="ui-detail-card-${card.key}"]`)
      expect(drawn.attributes('aria-busy')).toBeUndefined()
      expect(drawn.text()).toContain(english.global.t(card.title ?? ''))
    }
  })

  it('heads the services, domains and quotes tables with the columns of its schema', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    for (const [testid, table] of [
      ['client-summary-services', SUMMARY_SERVICES_TABLE],
      ['client-summary-domains', SUMMARY_DOMAINS_TABLE],
      ['client-summary-quotes', SUMMARY_QUOTES_TABLE],
    ] as const) {
      expect(wrapper.get(`[data-testid="${testid}"]`).findAll('[role="columnheader"]')).toHaveLength(table.columns.length)
    }
  })

  it('titles the services, domains and quotes tables in their card header, with a link to the whole list', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    for (const [testid, title] of [
      ['client-summary-services', 'Products/Services'],
      ['client-summary-domains', 'Domains'],
      ['client-summary-quotes', 'Current Quotes'],
    ] as const) {
      const card = wrapper.get(`[data-testid="${testid}"]`).element.closest('.overflow-clip')
      expect(card?.textContent).toContain(title)
    }
    expect(wrapper.find('[data-testid="client-summary-services-all"]').exists()).toBe(true)
  })

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

    const billing = wrapper.get('[data-testid="ui-detail-card-billing"]').text()
    expect(billing).toContain('Partially Refunded')
    expect(billing).toMatch(/Partially Refunded\s*1/)
  })

  it('lists the plugin widgets of the client in its slot', async () => {
    stubRoutes({
      ...summaryRoutes(() => new Response(null, { status: 204 })),
      'GET /api/admin/plugins/slots/admin.client.panels?clientId=7': () =>
        json({ slotId: 'admin.client.panels', widgets: [{ pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Site usage', order: 1, contextKeys: ['clientId'] }] }),
    })
    const wrapper = await mountSummary()

    expect(wrapper.get('[data-testid="plugin-slot-admin.client.panels"]').text()).toContain('Site usage')
  })

  it('shows no slot when no plugin offers a widget for the client', async () => {
    stubRoutes({
      ...summaryRoutes(() => new Response(null, { status: 204 })),
      'GET /api/admin/plugins/slots/admin.client.panels?clientId=7': () => json({ slotId: 'admin.client.panels', widgets: [] }),
    })
    const wrapper = await mountSummary()

    expect(wrapper.find('[data-testid^="plugin-slot-"]').exists()).toBe(false)
  })

  it('reports saved notes under the Save button', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-summary-notes-input"]').setValue('Prefers mail')
    await wrapper.get('[data-testid="client-summary-notes-save"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Notes saved.'])
  })

  it('shows the API sentence under the Save button when the notes are refused', async () => {
    stubRoutes(summaryRoutes(() => problem('Notes are too long.', 400)))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-summary-notes-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Notes are too long.')
  })

  it('links the external account and reports it inside its card', async () => {
    const mock = stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('crm')
    await wrapper.get('[data-testid="client-external-account-id"]').setValue('acct-1')
    await wrapper.get('[data-testid="client-external-account-save"]').trigger('click')
    await flushPromises()

    expect(externalAccountPuts(mock)[0]?.body).toEqual({ provider: 'crm', accountId: 'acct-1' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['External account link saved.'])
  })

  it('toasts the conflict sentence of the API when another client holds the pair', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 }), () => problem('Already linked to another client.', 409)))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('crm')
    await wrapper.get('[data-testid="client-external-account-id"]').setValue('acct-1')
    await wrapper.get('[data-testid="client-external-account-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Already linked to another client.')
  })

  it('fills the card from the link the client holds', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 }), undefined, () => client({ provider: 'crm', id: 'acct-9' })))
    const wrapper = await mountSummary()

    expect((wrapper.get('[data-testid="client-external-account-provider"]').element as HTMLInputElement).value).toBe('crm')
    expect((wrapper.get('[data-testid="client-external-account-id"]').element as HTMLInputElement).value).toBe('acct-9')
    expect(wrapper.get('[data-testid="client-external-account-current"]').text()).toBe('Linked to crm, account acct-9.')
  })

  it('offers no Remove link while the client has no link', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 })))
    const wrapper = await mountSummary()

    expect(wrapper.find('[data-testid="client-external-account-clear"]').exists()).toBe(false)
  })

  it('shows the new link after a save refreshed the client', async () => {
    let held: ClientExternalAccount | null = null
    stubRoutes(summaryRoutes(
      () => new Response(null, { status: 204 }),
      () => {
        held = { provider: 'crm', id: 'acct-1' }
        return new Response(null, { status: 204 })
      },
      () => client(held),
    ))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('crm')
    await wrapper.get('[data-testid="client-external-account-id"]').setValue('acct-1')
    await wrapper.get('[data-testid="client-external-account-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-external-account-current"]').text()).toBe('Linked to crm, account acct-1.')
    expect(wrapper.find('[data-testid="client-external-account-clear"]').exists()).toBe(true)
  })

  it('keeps what is typed in the card when the notes are saved', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 }), undefined, () => client({ provider: 'crm', id: 'acct-1' })))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('billing')
    await wrapper.get('[data-testid="client-summary-notes-input"]').setValue('Prefers mail')
    await wrapper.get('[data-testid="client-summary-notes-save"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Notes saved.'])
    expect((wrapper.get('[data-testid="client-external-account-provider"]').element as HTMLInputElement).value).toBe('billing')
  })

  it('clears the link after the confirmation and empties the inputs', async () => {
    let held: ClientExternalAccount | null = { provider: 'crm', id: 'acct-1' }
    const mock = stubRoutes(summaryRoutes(
      () => new Response(null, { status: 204 }),
      () => {
        held = null
        return new Response(null, { status: 204 })
      },
      () => client(held),
    ))
    const wrapper = await mountSummary()

    await wrapper.get('[data-testid="client-external-account-clear"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(externalAccountPuts(mock)[0]?.body).toEqual({ provider: null, accountId: null })
    expect((wrapper.get('[data-testid="client-external-account-provider"]').element as HTMLInputElement).value).toBe('')
    expect(wrapper.find('[data-testid="client-external-account-clear"]').exists()).toBe(false)
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['External account link cleared.'])
  })

  it('prints each figure in the currency the summary names for it, never in an assumed one', async () => {
    stubRoutes({
      ...summaryRoutes(
        () => new Response(null, { status: 204 }),
        undefined,
        () => client(null, CurrencyCode.Amd),
      ),
      'GET /api/clients/7/summary': () => json({ ...summary, creditBalance: { amount: 5000, currency: 'AMD' } }),
    })
    const wrapper = await mountSummary()

    const billing = wrapper.get('[data-testid="ui-detail-card-billing"]').text()
    expect(billing).toContain('5,000.00 AMD')
    expect(billing).toContain('40.00 USD')
    expect(billing).not.toContain('40.00 AMD')
    expect(billing).not.toContain('$')
  })

  it('prints one amount per currency when the client has records in several', async () => {
    const mixed: ClientSummaryData = {
      ...summary,
      invoices: {
        ...summary.invoices,
        paid: {
          count: 3,
          totals: [
            { amount: 20000, currency: 'AMD' },
            { amount: 50, currency: 'USD' },
          ],
        },
      },
      transactions: {
        grossRevenue: [
          { amount: 20000, currency: 'AMD' },
          { amount: 50, currency: 'USD' },
        ],
        clientExpenses: [],
        totalFees: [],
        netIncome: [
          { amount: 19000, currency: 'AMD' },
          { amount: 50, currency: 'USD' },
        ],
      },
    }
    stubRoutes({
      ...summaryRoutes(() => new Response(null, { status: 204 }), undefined, () => client(null, CurrencyCode.Amd)),
      'GET /api/clients/7/summary': () => json(mixed),
    })
    const wrapper = await mountSummary()

    const billing = wrapper.get('[data-testid="ui-detail-card-billing"]').text()
    expect(billing).toContain('20,000.00 AMD')
    expect(billing).toContain('50.00 USD')
    expect(billing).toContain('19,000.00 AMD')
    expect(billing).not.toContain('20050')
  })

  it('prints a zero in the billing currency for a figure without records', async () => {
    stubRoutes(summaryRoutes(() => new Response(null, { status: 204 }), undefined, () => client(null, CurrencyCode.Amd)))
    const wrapper = await mountSummary()

    expect(wrapper.get('[data-testid="ui-detail-card-billing"]').text()).toMatch(/Client Expenses\s*0\.00 AMD/)
  })

  it('prints a quote total in the currency of the quote', async () => {
    stubRoutes({
      'GET /api/billing/quotes/client/7': () =>
        json({ items: [quoteRowFixture({ total: 24000, currency: CurrencyCode.Amd })], pageNumber: 1, pageSize: 5, totalCount: 1, totalPages: 1 }),
      ...summaryRoutes(() => new Response(null, { status: 204 })),
    })
    const wrapper = await mountSummary()

    expect(wrapper.text()).toContain('24,000.00 AMD')
    expect(wrapper.text()).not.toContain('$24,000.00')
  })
})
