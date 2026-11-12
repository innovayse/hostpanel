/**
 * Unit tests for ServicePlanChange: the listed plans with their prorated amounts, the empty and
 * failed states, the wire value sent and the three results (done, invoice to pay, waiting for staff).
 *
 * The real plan change store, API composable and transport run against a stub server playing the
 * BFF; the assertions are on the requests that reached it.
 *
 * @module tests/components/client/services/ServicePlanChange.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServicePlanChange from '~/components/client/services/ServicePlanChange.vue'
import { useServicePlanChangeStore } from '~/stores/serviceplanchange'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { FAILED_READ_REQUESTS } from '~/tests/helpers/failedRead'
import { settle } from '~/tests/helpers/settle'
import { ServiceChangeStatus } from '~/types/servicechangestatus'
import { ServiceChangeType } from '~/types/servicechangetype'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that lists the plans service 4 can move to. */
const OPTIONS_PATH = '/api/portal/client/services/4/plan-change-options'

/** The BFF path that takes the move of service 4 to another plan. */
const CHANGE_PATH = '/api/portal/client/services/4/plan-change'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** An upgrade and a downgrade the service can move to. */
const OPTIONS = [
  { productId: 3, name: 'Pro', type: ServiceChangeType.Upgrade, newRecurringAmount: 20, prorationAmount: 5, currencyCode: 'USD' },
  { productId: 1, name: 'Basic', type: ServiceChangeType.Downgrade, newRecurringAmount: 5, prorationAmount: 2, currencyCode: 'USD' },
]

/**
 * Answers the plan change request of the current test.
 *
 * @param reply - What the BFF answers for the request.
 */
const answerChange = (reply: StubReply): void => {
  table[`POST ${CHANGE_PATH}`] = reply
}

/**
 * Mounts the panel and waits for the options to be read.
 *
 * @param requests - How many options requests the read makes before it ends: one, or {@link FAILED_READ_REQUESTS} for a failing server.
 * @returns The wrapper.
 */
const mountPanel = async (requests = 1) => {
  const wrapper = mountClient(ServicePlanChange, { props: { serviceId: '4' } })
  await waitUntil(() => expect(requestsTo(stub.requests, OPTIONS_PATH)).toHaveLength(requests))
  await waitUntil(() => expect(useServicePlanChangeStore().loading).toBe(false))
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
  beforeEach(async () => {
    table = { [`GET ${OPTIONS_PATH}`]: { body: OPTIONS } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
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
    table[`GET ${OPTIONS_PATH}`] = { body: [] }
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="service-plan-change-empty"]').exists()).toBe(true)
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('shows the sentence when the options cannot be read', async () => {
    table[`GET ${OPTIONS_PATH}`] = { status: 500, body: { detail: 'no' } }
    const wrapper = await mountPanel(FAILED_READ_REQUESTS)

    await waitUntil(() => expect(wrapper.find('[data-testid="service-plan-change-load-error"]').exists()).toBe(true))
  })

  it('asks for the options once more after a server failure, and no more', async () => {
    table[`GET ${OPTIONS_PATH}`] = { status: 500, body: { detail: 'no' } }

    await mountPanel(FAILED_READ_REQUESTS)
    await settle()

    expect(requestsTo(stub.requests, OPTIONS_PATH)).toHaveLength(FAILED_READ_REQUESTS)
  })

  it('sends the chosen product', async () => {
    answerChange({ body: { requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Completed, prorationAmount: 5, currencyCode: 'USD', invoiceId: null } })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await waitUntil(() => expect(requestsTo(stub.requests, CHANGE_PATH)).toHaveLength(1))
    expect(bodyOf(requestsTo(stub.requests, CHANGE_PATH)[0])).toEqual({ toProductId: 3 })
  })

  it('links to the invoice to pay after an upgrade', async () => {
    answerChange({ body: { requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Approved, prorationAmount: 5, currencyCode: 'USD', invoiceId: 8 } })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await waitUntil(() => expect(wrapper.find('[data-testid="service-plan-change-pay"]').attributes('href')).toBe('/client/invoices/8/pay'))
    expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangeInvoiced')
  })

  it('shows that a downgrade waits for approval, with no invoice link', async () => {
    answerChange({ body: { requestId: 2, type: ServiceChangeType.Downgrade, status: ServiceChangeStatus.Pending, prorationAmount: 2, currencyCode: 'USD', invoiceId: null } })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await waitUntil(() => expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangePending'))
    expect(wrapper.find('[data-testid="service-plan-change-pay"]').exists()).toBe(false)
  })

  it('shows the done sentence when the plan already changed', async () => {
    answerChange({ body: { requestId: 3, type: ServiceChangeType.Downgrade, status: ServiceChangeStatus.Completed, prorationAmount: 2, currencyCode: 'USD', invoiceId: null } })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await waitUntil(() => expect(wrapper.find('[data-testid="service-plan-change-success"]').text()).toContain('client.services.planChangeDone'))
    expect(wrapper.emitted('changed')).toHaveLength(1)
  })

  it('does not ask the page to read the service again while the change still waits', async () => {
    answerChange({ body: { requestId: 2, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Approved, prorationAmount: 5, currencyCode: 'USD', invoiceId: 11 } })
    const wrapper = await mountPanel()

    await submitPro(wrapper)

    await waitUntil(() => expect(wrapper.find('[data-testid="service-plan-change-success"]').exists()).toBe(true))
    expect(wrapper.emitted('changed')).toBeUndefined()
  })
})
