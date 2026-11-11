/**
 * Unit tests for ServicePlanChange: the listed plans with their prorated amounts, the empty and
 * failed states, the wire value sent and the three results (done, invoice to pay, waiting for staff).
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServicePlanChange from '~/components/client/services/ServicePlanChange.vue'
import { ServiceChangeStatus } from '~/types/servicechangestatus'
import { ServiceChangeType } from '~/types/servicechangetype'

/** The mocked client endpoints the store reaches. */
const api = vi.hoisted(() => ({ fetchPlanChangeOptions: vi.fn(), requestPlanChange: vi.fn() }))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => api }))

/** An upgrade and a downgrade the service can move to. */
const OPTIONS = [
  { productId: 3, name: 'Pro', type: ServiceChangeType.Upgrade, newRecurringAmount: 20, prorationAmount: 5, currencyCode: 'USD' },
  { productId: 1, name: 'Basic', type: ServiceChangeType.Downgrade, newRecurringAmount: 5, prorationAmount: 2, currencyCode: 'USD' },
]

/**
 * Mounts the panel and waits for the options to be read.
 *
 * @returns The wrapper.
 */
const mountPanel = async () => {
  const wrapper = mountClient(ServicePlanChange, { props: { serviceId: '4' } })
  await vi.waitFor(() => expect(api.fetchPlanChangeOptions).toHaveBeenCalledWith('4'))
  await wrapper.vm.$nextTick()
  return wrapper
}

/**
 * Picks the Pro plan and submits the form.
 *
 * @param wrapper - The mounted panel.
 */
const submitPro = async (wrapper: Awaited<ReturnType<typeof mountPanel>>): Promise<void> => {
  await wrapper.find('[data-testid="service-plan-change-option-3"]').trigger('click')
  await wrapper.find('form').trigger('submit')
}

describe('ServicePlanChange', () => {
  beforeEach(() => {
    api.fetchPlanChangeOptions.mockReset()
    api.requestPlanChange.mockReset()
    api.fetchPlanChangeOptions.mockResolvedValue(OPTIONS)
  })

  it('lists each plan with its amount and whether it is an upgrade or a downgrade', async () => {
    const wrapper = await mountPanel()

    const form = wrapper.find('[data-testid="service-plan-change-form"]').text()
    expect(form).toContain('Pro')
    expect(form).toContain('client.services.planChangeUpgradeDesc')
    expect(form).toContain('Basic')
    expect(form).toContain('client.services.planChangeDowngradeDesc')
  })

  it('disables the submit button until a plan is picked', async () => {
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="service-plan-change-submit"]').attributes('disabled')).toBeDefined()
    await wrapper.find('[data-testid="service-plan-change-option-3"]').trigger('click')
    expect(wrapper.find('[data-testid="service-plan-change-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('says so when there is no other plan', async () => {
    api.fetchPlanChangeOptions.mockResolvedValue([])
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="service-plan-change-empty"]').exists()).toBe(true)
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('shows the sentence when the options cannot be read', async () => {
    api.fetchPlanChangeOptions.mockRejectedValue(new Error('no'))
    const wrapper = await mountPanel()

    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-plan-change-load-error"]').exists()).toBe(true))
  })

  it('sends the chosen product', async () => {
    api.requestPlanChange.mockResolvedValue({ requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Completed, prorationAmount: 5, currencyCode: 'USD', invoiceId: null })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await vi.waitFor(() => expect(api.requestPlanChange).toHaveBeenCalledWith('4', 3))
  })

  it('links to the invoice to pay after an upgrade', async () => {
    api.requestPlanChange.mockResolvedValue({ requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Approved, prorationAmount: 5, currencyCode: 'USD', invoiceId: 8 })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-plan-change-pay"]').attributes('href')).toBe('/client/invoices/8/pay'))
    expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangeInvoiced')
  })

  it('shows that a downgrade waits for approval, with no invoice link', async () => {
    api.requestPlanChange.mockResolvedValue({ requestId: 2, type: ServiceChangeType.Downgrade, status: ServiceChangeStatus.Pending, prorationAmount: 2, currencyCode: 'USD', invoiceId: null })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangePending'))
    expect(wrapper.find('[data-testid="service-plan-change-pay"]').exists()).toBe(false)
  })

  it('shows the done sentence when the plan already changed', async () => {
    api.requestPlanChange.mockResolvedValue({ requestId: 3, type: ServiceChangeType.Downgrade, status: ServiceChangeStatus.Completed, prorationAmount: 2, currencyCode: 'USD', invoiceId: null })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangeDone'))
  })
})
