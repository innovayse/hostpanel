/**
 * Mount tests for InvoiceDetailView: the invoice loads by id into the tabs, the Options tab
 * exists only while managing, each tab reports its own outcome (never the page), the PDF
 * download saves the file or shows its refusal beside the button, and a refused load shows the
 * API sentence with Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceDetailView from '@/views/InvoiceDetailView.vue'
import { saveBlob } from '@/utils/saveBlob'
import { invoiceFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

vi.mock('@/utils/saveBlob', () => ({ saveBlob: vi.fn() }))

/** Route pattern of the screen, with the optional mode segment. */
const PATTERN = '/billing/:id/:action?'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.mocked(saveBlob).mockClear()
})

describe('InvoiceDetailView', () => {
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
    expect(wrapper.get('[data-testid="billing-invoice-notes-feedback"]').text()).toBe('Changes saved successfully.')
    expect((wrapper.get('[data-testid="billing-invoice-notes-input"]').element as HTMLTextAreaElement).value)
      .toBe('Saved note')
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

    expect(wrapper.get('[data-testid="billing-invoice-notes-feedback"]').text()).toBe('Notes are too long.')
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

    expect(wrapper.get('[data-testid="billing-invoice-summary-feedback"]').text()).toContain('Cannot cancel.')
    expect(wrapper.find('[data-testid="billing-invoice-error"]').exists()).toBe(false)
  })

  it('downloads the PDF as a blob under the name the API gives', async () => {
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

    expect(button.text()).toContain('Download PDF')
    expect(saveBlob).toHaveBeenCalledTimes(1)
    expect(vi.mocked(saveBlob).mock.calls[0]?.[1]).toBe('invoice-1.pdf')
    expect(wrapper.get('[data-testid="billing-invoice-download-feedback"]').text()).toBe('')
  })

  it('falls back to a default file name when the API sends none', async () => {
    stubRoutes({
      'GET /api/billing/1/pdf': () => new Response('%PDF', { status: 200 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-download"]').trigger('click')
    await flushPromises()

    expect(vi.mocked(saveBlob).mock.calls[0]?.[1]).toBe('invoice-1.pdf')
  })

  it('shows a refused PDF download beside its button only, and saves nothing', async () => {
    stubRoutes({
      'GET /api/billing/1/pdf': () => problem('The invoice has too many lines for a PDF.', 409),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const { wrapper } = await mountRouted(InvoiceDetailView, PATTERN, '/billing/1')
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-download"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-download-feedback"]').text())
      .toBe('The invoice has too many lines for a PDF.')
    expect(saveBlob).not.toHaveBeenCalled()
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
})
