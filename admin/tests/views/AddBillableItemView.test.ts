/**
 * Mount tests for AddBillableItemView: the one-time form posts what was typed with the day it is
 * invoiced and returns to the list, a recurring charge also posts its period, the due date label
 * follows the type, and a refusal shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AddBillableItemView from '@/views/AddBillableItemView.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { toIsoDay } from '@/lib/format'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The clients endpoint answer: nobody to pick. */
const noClients = () => json({ items: [], totalCount: 0 })

/** The list answer the store reloads after a create. */
const emptyItems = () => json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })

/**
 * Mounts the form and waits for the client list.
 *
 * @returns The wrapper and the router.
 */
const mountForm = async () => {
  const mounted = await mountRouted(AddBillableItemView, '/billing/billable-items/add', '/billing/billable-items/add')
  await flushPromises()
  return mounted
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AddBillableItemView', () => {
  it('posts a one-time item with the day it is invoiced and returns to the list', async () => {
    const mock = stubRoutes({
      'GET /api/clients': noClients,
      'POST /api/billing/billable-items': () => new Response(null, { status: 204 }),
      'GET /api/billing/billable-items': emptyItems,
    })
    const { wrapper, router } = await mountForm()
    expect(wrapper.get('[data-testid="billing-add-billable-item-next-due-date-field"]').text()).toContain('Invoice on')

    await wrapper.get('[data-testid="billing-add-billable-item-description"]').setValue('Domain renewal')
    await wrapper.get('[data-testid="billing-add-billable-item-amount"] input').setValue('12')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')
    const body = JSON.parse(String((post?.[1] as RequestInit | undefined)?.body)) as Record<string, unknown>
    expect(body).toMatchObject({
      description: 'Domain renewal',
      amount: 12,
      currency: 'USD',
      type: BillableItemType.OneTime,
      nextDueDate: toIsoDay(new Date()),
    })
    expect(body.recurringPeriod).toBeUndefined()
    expect(router.currentRoute.value.path).toBe('/billing/billable-items')
  })

  it('shows the recurrence fields and posts the period and next due date for a recurring item', async () => {
    const mock = stubRoutes({
      'GET /api/clients': noClients,
      'POST /api/billing/billable-items': () => new Response(null, { status: 204 }),
      'GET /api/billing/billable-items': emptyItems,
    })
    const { wrapper } = await mountForm()
    expect(wrapper.find('[data-testid="billing-add-billable-item-recurring-period"]').exists()).toBe(false)

    wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === 'billing-add-billable-item-type')?.vm.$emit('update:modelValue', BillableItemType.Recurring)
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-add-billable-item-recurring-period"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="billing-add-billable-item-next-due-date-field"]').text()).toContain('Next due date')

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toMatchObject({
      type: BillableItemType.Recurring,
      recurringPeriod: RecurringPeriod.Monthly,
      nextDueDate: toIsoDay(new Date()),
    })
  })

  it('shows the API sentence when the item is refused', async () => {
    stubRoutes({ 'GET /api/clients': noClients, 'POST /api/billing/billable-items': () => problem('Client is required.', 400) })
    const { wrapper } = await mountForm()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-add-billable-item-error"]').text()).toContain('Client is required.')
  })
})
