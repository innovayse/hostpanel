/**
 * Mount tests for AddTransactionModal: form fields, save request and refusal.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AddTransactionModal from '@/components/clients/AddTransactionModal.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

/** The client's invoices, as the client invoice list answers them. */
const INVOICES = {
  items: [
    { id: 41, clientId: 9, clientName: 'Ada Lovelace', status: 'Unpaid', total: 12.5, currency: 'USD' },
    { id: 42, clientId: 9, clientName: 'Ada Lovelace', status: 'Paid', total: 30, currency: 'USD' },
  ],
  totalCount: 2,
}

/**
 * Mounts the modal for client 9.
 *
 * @returns The wrapper.
 */
const mountModal = () =>
  mount(AddTransactionModal, {
    props: { clientId: '9', saving: false },
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })

describe('AddTransactionModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('posts the typed description and emits saved', async () => {
    const fetchMock = stubFetch(json(INVOICES), json({ id: 1 }))
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-add-transaction-description"]').setValue('Wire')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const body = callOf(fetchMock, 1).body as { description: string; clientId: number; invoiceId: number | null }
    expect(callOf(fetchMock, 1).method).toBe('POST')
    expect(body.invoiceId).toBeNull()
    expect(body.description).toBe('Wire')
    expect(body.clientId).toBe(9)
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('offers the invoices of the client and posts the chosen id', async () => {
    const fetchMock = stubFetch(json(INVOICES), json({ id: 1 }))
    const wrapper = mountModal()
    await flushPromises()

    expect(callOf(fetchMock).url).toBe('/api/billing/client/9?page=1&pageSize=100')
    const picker = wrapper
      .findAllComponents(UiSelect)
      .find(c => c.attributes('data-testid') === 'clients-add-transaction-invoice-id')
    const values = ((picker?.props() as { options: { value: number | null }[] } | undefined)?.options ?? []).map(option => option.value)
    expect(values).toEqual([null, 41, 42])

    picker?.vm.$emit('update:modelValue', 42)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect((callOf(fetchMock, 1).body as { invoiceId: number }).invoiceId).toBe(42)
  })

  it('shows the refusal sentence and stays open', async () => {
    stubFetch(json(INVOICES), problem('Amount is invalid'))
    const wrapper = mountModal()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="clients-add-transaction-error"]').text()).toContain('Amount is invalid')
    expect(wrapper.emitted('saved')).toBeUndefined()
  })

  it('emits close from cancel', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-add-transaction-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
