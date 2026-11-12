/**
 * Unit tests for ServiceCancelForm: the default type, the confirmation before sending, the wire
 * value sent and the success state.
 *
 * The real service management store, API composable and transport run against a stub server
 * playing the BFF; the assertions are on the request that reached it.
 *
 * @module tests/components/client/services/ServiceCancelForm.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceCancelForm from '~/components/client/services/ServiceCancelForm.vue'
import { CancellationType } from '~/types/cancellationtype'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that takes the cancellation of service 4. */
const CANCEL_PATH = '/api/portal/client/services/4/cancel'

/** The stub playing the BFF. */
let stub: StubServer

/**
 * The cancellation requests the stub received.
 *
 * @returns The requests, oldest first.
 */
const cancellations = () => requestsTo(stub.requests, CANCEL_PATH)

describe('ServiceCancelForm', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable({ [`POST ${CANCEL_PATH}`]: { body: {} } }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('preselects the end of the billing period', () => {
    const wrapper = mountClient(ServiceCancelForm, { props: { serviceId: '4' } })

    expect(wrapper.find('[data-testid="service-cancel-type-end"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="service-cancel-type-immediate"]').attributes('aria-checked')).toBe('false')
  })

  it('does not send anything until the dialog is confirmed', async () => {
    const wrapper = mountClient(ServiceCancelForm, { props: { serviceId: '4' } })

    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-testid="ui-confirm-dialog"]').exists()).toBe(true)
    expect(stub.requests).toHaveLength(0)
  })

  it('sends the chosen wire value and the reason once confirmed', async () => {
    const wrapper = mountClient(ServiceCancelForm, { props: { serviceId: '4' } })

    await wrapper.find('[data-testid="service-cancel-type-immediate"]').trigger('click')
    await wrapper.find('[data-testid="service-cancel-reason"]').setValue('Moving away')
    await wrapper.find('form').trigger('submit')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await waitUntil(() => expect(cancellations()).toHaveLength(1))
    expect(bodyOf(cancellations()[0])).toEqual({ type: CancellationType.Immediate, reason: 'Moving away' })
  })

  it('shows the success state after the request is accepted', async () => {
    const wrapper = mountClient(ServiceCancelForm, { props: { serviceId: '4' } })

    await wrapper.find('form').trigger('submit')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="service-cancel-success"]').exists()).toBe(true))
  })

  it('cancels the confirmation without sending', async () => {
    const wrapper = mountClient(ServiceCancelForm, { props: { serviceId: '4' } })

    await wrapper.find('form').trigger('submit')
    await wrapper.find('[data-testid="ui-confirm-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-confirm-dialog"]').exists()).toBe(false)
    expect(stub.requests).toHaveLength(0)
  })
})
