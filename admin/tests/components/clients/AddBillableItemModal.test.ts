/**
 * Mount tests for AddBillableItemModal: a one time item and a recurring item each post exactly the
 * API's create command with the day they are invoiced, the due date label follows the type, the
 * API's sentence is toasted and its field messages appear under their fields, a reopened modal starts without them, and cancel
 * closes.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import AddBillableItemModal from '@/components/clients/AddBillableItemModal.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { toIsoDay } from '@/lib/format'
import { useClientsStore } from '@/stores/clients'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import type { ConfiguredCurrency } from '@/types/client'
import { createTestI18n } from '../../helpers/i18n'
import { invalid, json } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'
import { toastMessages } from '../../helpers/toasts'

/** Request line of the create call the modal makes for client 3. */
const CREATE_LINE = 'POST /api/clients/3/billable-items'

/** The admin currency configuration: two enabled currencies and one switched off. */
const configuredCurrencies: ConfiguredCurrency[] = [
  { code: 'USD', prefix: '$', suffix: '', isEnabled: true },
  { code: 'EUR', prefix: '', suffix: ' EUR', isEnabled: true },
  { code: 'GBP', prefix: '', suffix: ' GBP', isEnabled: false },
]

/** The Pinia the modal and the test share, so a store loaded by the test is the modal's. */
let pinia: Pinia

/**
 * Mounts the modal for client 3 and waits for the currencies.
 *
 * @returns The wrapper.
 */
const mountModal = async (): Promise<VueWrapper> => {
  const wrapper = mount(AddBillableItemModal, {
    props: { clientId: '3' },
    global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } },
  })
  await flushPromises()
  return wrapper
}

/**
 * Finds one of the modal's selects by its `data-testid`.
 *
 * @param wrapper - The mounted modal.
 * @param testid - The select's `data-testid`.
 * @returns The select's wrapper.
 */
const selectOf = (wrapper: VueWrapper, testid: string): VueWrapper => {
  const select = wrapper.findAllComponents(UiSelect).find(candidate => candidate.attributes('data-testid') === testid)
  if (select === undefined) {
    throw new Error(`No select ${testid}`)
  }
  return select
}

/**
 * Reads the JSON body of the create call.
 *
 * @param mock - The stubbed `fetch`.
 * @returns The parsed body, undefined when the modal made no create call.
 */
const createBody = (mock: ReturnType<typeof vi.fn>): unknown => {
  const call = mock.mock.calls.find(
    ([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${String(url)}` === CREATE_LINE,
  )
  return call === undefined ? undefined : JSON.parse(String((call[1] as RequestInit).body))
}

describe('AddBillableItemModal', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts a one time item in the client currency with the day it is invoiced, without a period', async () => {
    const mock = stubRoutes({
      'GET /api/clients/3': () => json({ id: 3, preferences: { currency: 'EUR' } }),
      'GET /api/admin/currencies': () => json(configuredCurrencies),
      [CREATE_LINE]: () => json(7, 201),
    })
    await useClientsStore().fetchById('3')
    const wrapper = await mountModal()

    const currency = selectOf(wrapper, 'clients-add-billable-item-currency')
    await currency.get('[data-testid="ui-select-trigger"]').trigger('click')
    expect(currency.findAll('[data-testid="ui-select-option"]').map(option => option.text())).toEqual(['USD', 'EUR'])
    expect(currency.get('[aria-selected="true"]').text()).toBe('EUR')
    expect(wrapper.find('[data-testid="clients-add-billable-item-recurrence"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="clients-add-billable-item-next-due-date-field"]').text()).toContain('Invoice on')

    await wrapper.get('[data-testid="clients-add-billable-item-description"]').setValue('Setup fee')
    await wrapper.get('[data-testid="clients-add-billable-item-amount"] input').setValue('12.5')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toEqual({
      description: 'Setup fee',
      amount: 12.5,
      currency: 'EUR',
      type: BillableItemType.OneTime,
      nextDueDate: toIsoDay(new Date()),
    })
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('posts a recurring item with its period and next due date', async () => {
    const mock = stubRoutes({
      'GET /api/admin/currencies': () => json(configuredCurrencies),
      [CREATE_LINE]: () => json(8, 201),
    })
    const wrapper = await mountModal()

    await wrapper.get('[data-testid="clients-add-billable-item-description"]').setValue('Managed backup')
    await wrapper.get('[data-testid="clients-add-billable-item-amount"] input').setValue('20')
    selectOf(wrapper, 'clients-add-billable-item-type').vm.$emit('update:modelValue', BillableItemType.Recurring)
    await flushPromises()
    expect(wrapper.find('[data-testid="clients-add-billable-item-recurrence"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="clients-add-billable-item-next-due-date-field"]').text()).toContain('Next due date')

    selectOf(wrapper, 'clients-add-billable-item-recurring-period').vm.$emit('update:modelValue', RecurringPeriod.Quarterly)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toEqual({
      description: 'Managed backup',
      amount: 20,
      currency: 'USD',
      type: BillableItemType.Recurring,
      recurringPeriod: RecurringPeriod.Quarterly,
      nextDueDate: toIsoDay(new Date()),
    })
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('shows the API field messages under their fields and toasts the sentence', async () => {
    stubRoutes({
      'GET /api/admin/currencies': () => json(configuredCurrencies),
      [CREATE_LINE]: () =>
        invalid('Some fields are not valid.', {
          Description: ['Enter a description of at most 500 characters.'],
          Amount: ['The amount cannot be negative.'],
        }),
    })
    const wrapper = await mountModal()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(toastMessages()).toEqual(['Some fields are not valid.'])
    expect(wrapper.get('[data-testid="clients-add-billable-item-description-error"]').text()).toBe(
      'Enter a description of at most 500 characters.',
    )
    expect(wrapper.get('[data-testid="clients-add-billable-item-amount-error"]').text()).toBe('The amount cannot be negative.')
    expect(wrapper.emitted('saved')).toBeUndefined()
  })

  it('opens again without the field messages of the last refused add', async () => {
    stubRoutes({
      'GET /api/admin/currencies': () => json(configuredCurrencies),
      [CREATE_LINE]: () =>
        invalid('Some fields are not valid.', { Description: ['Enter a description of at most 500 characters.'] }),
    })
    const first = await mountModal()
    await first.get('form').trigger('submit')
    await flushPromises()
    expect(first.find('[data-testid="clients-add-billable-item-description-error"]').exists()).toBe(true)
    first.unmount()

    const reopened = await mountModal()

    expect(reopened.find('[data-testid="clients-add-billable-item-description-error"]').exists()).toBe(false)
  })

  it('emits close from cancel', async () => {
    stubRoutes({ 'GET /api/admin/currencies': () => json(configuredCurrencies) })
    const wrapper = await mountModal()

    await wrapper.get('[data-testid="clients-add-billable-item-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
