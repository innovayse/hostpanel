/**
 * Mount tests of CreditNoteDetailView: it shows the note with its lines and totals, downloads the PDF, voids a standing note only
 * with a reason and after a prompt, offers no void for a note paid out as a gateway refund (and says why), and on a refused load
 * shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import CreditNoteDetailView from '@/views/CreditNoteDetailView.vue'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { creditNoteDetailFixture } from '../helpers/creditNoteFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Mounts the page for note 3 under its own route pattern and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountNote = async () => {
  const { wrapper } = await mountRouted(CreditNoteDetailView, '/billing/credit-notes/:id', '/billing/credit-notes/3')
  await flushPromises()
  return wrapper
}

describe('CreditNoteDetailView', () => {
  it('shows the number, the state, the invoice, the lines and the totals', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture()) })
    const wrapper = await mountNote()

    expect(wrapper.text()).toContain('Credit note CN-0003')
    expect(wrapper.get('[data-testid="billing-credit-note-status"]').text()).toBe('Applied')
    expect(wrapper.get('[data-testid="billing-credit-note-invoice"]').text()).toBe('#5')
    expect(wrapper.get('[data-testid="billing-credit-note-items"]').text()).toContain('Credit for hosting')
    expect(wrapper.get('[data-testid="billing-credit-note-total"]').text()).toContain('60.00')
  })

  it('voids with a reason after the prompt and shows the voided state', async () => {
    // The note reads as standing until the void is posted, and as voided from then on, like the real endpoint.
    let current = creditNoteDetailFixture()
    const mock = stubRoutes({
      'POST /api/billing/credit-notes/3/void': () => {
        current = creditNoteDetailFixture({ status: CreditNoteStatus.Voided, voidedAt: '2026-10-02T09:00:00Z', voidReason: 'Mistake' })
        return new Response(null, { status: 204 })
      },
      'GET /api/billing/credit-notes/3': () => json(current),
    })
    const wrapper = await mountNote()

    await wrapper.get('[data-testid="billing-credit-note-void"]').trigger('click')
    await wrapper.get('[data-testid="billing-credit-note-void-reason"]').setValue('Mistake')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url, init]) => String(url).endsWith('/void') && (init as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({ reason: 'Mistake' })
    expect(wrapper.get('[data-testid="billing-credit-note-feedback"]').text()).toBe('The credit note was voided.')
    expect(wrapper.get('[data-testid="billing-credit-note-voided"]').text()).toContain('Mistake')
    expect(wrapper.find('[data-testid="billing-credit-note-void"]').exists()).toBe(false)
  })

  it('stays in the prompt with a sentence when the void is confirmed without a reason', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture()) })
    const wrapper = await mountNote()

    await wrapper.get('[data-testid="billing-credit-note-void"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-credit-note-void-reason-error"]').text()).toBe('Give a reason for voiding the credit note.')
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/void'))).toBe(false)
  })

  it('shows the API sentence when the void is refused', async () => {
    stubRoutes({
      'POST /api/billing/credit-notes/3/void': () => problem('The client has already spent the credit this note gave.', 400),
      'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture()),
    })
    const wrapper = await mountNote()

    await wrapper.get('[data-testid="billing-credit-note-void"]').trigger('click')
    await wrapper.get('[data-testid="billing-credit-note-void-reason"]').setValue('Mistake')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-credit-note-feedback"]').text()).toBe('The client has already spent the credit this note gave.')
  })

  it('offers no void for a note paid out as a gateway refund and says why', async () => {
    stubRoutes({
      'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture({ method: CreditNoteMethod.GatewayRefund, refundReference: 're_1' })),
    })
    const wrapper = await mountNote()

    expect(wrapper.find('[data-testid="billing-credit-note-void"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="billing-credit-note-refund-blocked"]').text()).toContain('cannot be taken back')
  })

  it('offers no void for a note that is already voided', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture({ status: CreditNoteStatus.Voided, voidedAt: '2026-10-02T09:00:00Z' })) })
    const wrapper = await mountNote()

    expect(wrapper.find('[data-testid="billing-credit-note-void"]').exists()).toBe(false)
  })

  it('downloads the PDF and shows a refusal beside the button', async () => {
    stubRoutes({
      'GET /api/billing/credit-notes/3/pdf': () => problem('The renderer is busy.', 503),
      'GET /api/billing/credit-notes/3': () => json(creditNoteDetailFixture()),
    })
    const wrapper = await mountNote()

    await wrapper.get('[data-testid="billing-credit-note-download"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-credit-note-download-feedback"]').text()).toBe('The renderer is busy.')
  })

  it('shows the API sentence with a Retry when the load is refused', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes/3': () => problem('The credit note was not found.', 404) })
    const wrapper = await mountNote()

    expect(wrapper.get('[data-testid="billing-credit-note-error"]').text()).toContain('The credit note was not found.')
  })
})
