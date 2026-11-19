/**
 * Mount tests for InvoiceNotesTab: the read-only notes and their empty warning, and saving the
 * edited notes through the store, with the outcome shown under the editor.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceNotesTab from '@/components/billing/InvoiceNotesTab.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'
import { useToastStore } from '@/stores/toast'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceNotesTab', () => {
  it('shows the notes read-only', () => {
    const wrapper = mountWithApp(InvoiceNotesTab, {
      props: { invoice: invoiceFixture({ notes: 'Call the client' }), readonly: true },
    })

    expect(wrapper.get('[data-testid="billing-invoice-notes-text"]').text()).toBe('Call the client')
    expect(wrapper.find('[data-testid="billing-invoice-notes-input"]').exists()).toBe(false)
  })

  it('warns that there are no notes when read-only and empty', () => {
    const wrapper = mountWithApp(InvoiceNotesTab, { props: { invoice: invoiceFixture(), readonly: true } })

    expect(wrapper.get('[data-testid="billing-invoice-notes-empty"]').text()).toContain('no notes')
  })

  it('saves the edited notes and confirms it under the editor', async () => {
    const mock = stubRoutes({
      'PUT /api/billing/1/notes': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceNotesTab, { props: { invoice: invoiceFixture({ notes: 'Old' }) } })
    await flushPromises()

    expect((wrapper.get('[data-testid="billing-invoice-notes-input"]').element as HTMLTextAreaElement).value).toBe('Old')
    await wrapper.get('[data-testid="billing-invoice-notes-input"]').setValue('New notes')
    await wrapper.get('[data-testid="billing-invoice-notes-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/1/notes'))
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toEqual({ notes: 'New notes' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Changes saved successfully.'])
  })

  it('shows the API sentence under the editor when the save is refused', async () => {
    stubRoutes({ 'PUT /api/billing/1/notes': () => problem('Notes are too long.', 400) })
    const wrapper = mountWithApp(InvoiceNotesTab, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-notes-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-notes-feedback"]').text()).toBe('Notes are too long.')
  })
})
