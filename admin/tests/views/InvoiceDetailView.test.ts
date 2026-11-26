/**
 * Mount tests for InvoiceDetailView: the invoice loads by id into the tabs, the Options tab
 * exists only while managing, each tab reports its own outcome (never the page), the PDF
 * download saves the file or toasts its refusal, and a refused load shows the
 * API sentence with Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceDetailView from '@/views/InvoiceDetailView.vue'
import schema from '@/views/InvoiceDetailView.schema'
import { fulfilmentFixture, invoiceFixture } from '../helpers/billingFixtures'
import { captureDownloads } from '../helpers/captureDownloads'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'
import { toastMessages } from '../helpers/toasts'
import { expectOnlyActivePanel } from '../helpers/tabPanels'

/** Route pattern of the screen, with the optional mode segment. */
const PATTERN = '/billing/:id/:action?'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('InvoiceDetailView', () => {
  it('draws the cards of its schema as placeholders, without tabs, while the invoice loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')

    const cards = schema.columns.flat()
    expect(wrapper.findAll('[data-testid^="ui-detail-card-"]')).toHaveLength(cards.length)
    expect(wrapper.findAll('[data-testid="ui-detail-row"]')).toHaveLength(cards.reduce((sum, card) => sum + (card.rows?.length ?? 0), 0))
    expect(wrapper.text()).toContain('Invoice Items')
    expect(wrapper.find('[data-testid="billing-invoice-summary"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="billing-invoice-tab-summary"]').exists()).toBe(false)
  })

  it('loads the invoice and shows its number and the summary tab', async () => {
    stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    expect(wrapper.get('[data-testid="invoice-detail-title"]').text()).toContain('Invoice #1')
    expect(wrapper.find('[data-testid="billing-invoice-summary"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-mark-paid"]').exists()).toBe(false)
  })

  it('shows the Options tab and the actions only after switching to manage mode', async () => {
    stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-invoice-tab-options"]').exists()).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-toggle-mode"]').trigger('click')

    expect(wrapper.find('[data-testid="billing-invoice-tab-options"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-mark-paid"]').exists()).toBe(true)
  })

  it('draws only the open tab: the items and transactions sections belong to the summary alone', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => json([]),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()
    const summary = 'billing-invoice-tab-summary'

    await expectOnlyActivePanel(
      wrapper,
      [
        { tab: summary, panel: 'billing-invoice-summary' },
        { tab: 'billing-invoice-tab-payment', panel: 'billing-invoice-payment' },
        { tab: 'billing-invoice-tab-options', panel: 'billing-invoice-options' },
        { tab: 'billing-invoice-tab-credit', panel: 'billing-invoice-credit' },
        { tab: 'billing-invoice-tab-refund', panel: 'billing-invoice-refund' },
        { tab: 'billing-invoice-tab-credit-notes', panel: 'billing-invoice-credit-notes' },
        { tab: 'billing-invoice-tab-fulfilment', panel: 'billing-invoice-fulfilment' },
        { tab: 'billing-invoice-tab-notes', panel: 'billing-invoice-notes' },
      ],
      [
        { owner: summary, testid: 'billing-invoice-items' },
        { owner: summary, testid: 'billing-invoice-transactions' },
      ],
    )
  })

  it('opens in manage mode on the edit route and switches tabs', async () => {
    stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-tab-notes"]').trigger('click')

    expect(wrapper.find('[data-testid="billing-invoice-notes"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-notes-input"]').exists()).toBe(true)
  })

  it('saves the notes, reloads the invoice and confirms it in the tab', async () => {
    let notes = ''
    const mock = stubRoutes({
      'PUT /api/billing/1/notes': () => {
        notes = 'Saved note'
        return new Response(null, { status: 204 })
      },
      'GET /api/billing/1': () => json(invoiceFixture({ notes })),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-tab-notes"]').trigger('click')

    await wrapper.get('[data-testid="billing-invoice-notes-input"]').setValue('Saved note')
    await wrapper.get('[data-testid="billing-invoice-notes-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toEqual({ notes: 'Saved note' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Changes saved successfully.'])
    expect((wrapper.get('[data-testid="billing-invoice-notes-input"]').element as HTMLTextAreaElement).value)
      .toBe('Saved note')
  })

  it('opens the Fulfilment tab and loads the records of the invoice', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-tab-fulfilment"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-table"]').text()).toContain('provisioning-demo')
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(true)
  })

  it('keeps a failed fulfilment load in its tab, not at the top', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => problem('Fulfilment is unavailable.', 500),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-tab-fulfilment"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-error"]').text()).toContain('Fulfilment is unavailable.')
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('keeps a refused fulfilment retry in its tab, not at the top', async () => {
    stubRoutes({
      'POST /api/billing/1/fulfilment/31/retry': () => problem('This record cannot be retried.', 409),
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-tab-fulfilment"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('This record cannot be retried.')
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('shows a refused save in its tab only, not again at the top', async () => {
    stubRoutes({
      'PUT /api/billing/1/notes': () => problem('Notes are too long.', 400),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-tab-notes"]').trigger('click')

    await wrapper.get('[data-testid="billing-invoice-notes-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Notes are too long.')
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('shows a refused summary action beside its button only, not again at the top', async () => {
    stubRoutes({
      'POST /api/billing/1/cancel': () => problem('Cannot cancel.', 409),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1/edit')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-cancel"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(toastMessages().join(' ')).toContain('Cannot cancel.')
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('downloads the PDF as a blob under the name the API gives', async () => {
    const saved = captureDownloads()
    stubRoutes({
      'GET /api/billing/1/pdf': () =>
        new Response('%PDF', {
          status: 200,
          headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="invoice-1.pdf"' },
        }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    const button = wrapper.get('[data-testid="billing-invoice-download"]')
    await button.trigger('click')
    await flushPromises()

    expect(button.text()).toBe('PDF')
    expect(saved).toHaveLength(1)
    expect(saved[0]?.fileName).toBe('invoice-1.pdf')
    expect(toastMessages()).toEqual([])
  })

  it('falls back to a default file name when the API sends none', async () => {
    const saved = captureDownloads()
    stubRoutes({
      'GET /api/billing/1/pdf': () => new Response('%PDF', { status: 200 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-download"]').trigger('click')
    await flushPromises()

    expect(saved).toHaveLength(1)
    expect(saved[0]?.fileName).toBe('invoice-1.pdf')
  })

  it('toasts a refused PDF download and saves nothing', async () => {
    const saved = captureDownloads()
    stubRoutes({
      'GET /api/billing/1/pdf': () => problem('The invoice has too many lines for a PDF.', 409),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-download"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toEqual(['The invoice has too many lines for a PDF.'])
    expect(saved).toHaveLength(0)
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('shows the API sentence with Retry when the invoice cannot be loaded, and loads it again', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/billing/1': () => {
        calls += 1
        return calls === 1 ? problem('Invoice not found.', 404) : json(invoiceFixture())
      },
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-error"]').text()).toContain('Invoice not found.')
    expect(wrapper.find('[data-testid="billing-invoice-summary"]').exists()).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="billing-invoice-summary"]').exists()).toBe(true)
  })

  it('draws the trail above the title, the invoice itself last and no back link', async () => {
    stubRoutes({ 'GET /api/billing/1': () => json(invoiceFixture()) })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()
    const trail = wrapper.get('[data-testid="ui-breadcrumb"]')

    expect(trail.findAll('a').map((link) => link.attributes('href'))).toContain('/billing')
    expect(trail.get('[data-testid="ui-page-header-crumb-current"]').text()).toBe(wrapper.get('h1').text())
    expect(wrapper.find('[data-testid="billing-invoice-back"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    expect(trail.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
})
