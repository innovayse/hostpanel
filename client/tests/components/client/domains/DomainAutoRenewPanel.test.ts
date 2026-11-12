/**
 * Unit tests for DomainAutoRenewPanel: the switch moves only when the API accepts the change. The
 * store, the API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainAutoRenewPanel from '~/components/client/domains/DomainAutoRenewPanel.vue'
import { useDomainsStore } from '~/stores/domains'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { refusal } from '~/tests/helpers/stubRoutes'
import { bodyOf } from '~/tests/helpers/requestBody'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers the auto-renew change with; a test changes it before it clicks. */
let reply: StubReply

beforeEach(async () => {
  reply = { body: {} }
  stub = await startTransport(() => reply)
})

afterEach(async () => {
  await stub.close()
})

describe('DomainAutoRenewPanel', () => {
  it('shows the current state', () => {
    const on = mountClient(DomainAutoRenewPanel, { props: { enabled: true, domainId: DOMAIN_ID } })
    const off = mountClient(DomainAutoRenewPanel, { props: { enabled: false, domainId: DOMAIN_ID } })

    expect(on.text()).toContain('client.domains.autoRenewOn')
    expect(off.text()).toContain('client.domains.autoRenewOff')
  })

  it('asks the API and moves the switch when it accepts', async () => {
    const wrapper = mountClient(DomainAutoRenewPanel, { props: { enabled: false, domainId: DOMAIN_ID } })

    await wrapper.find('[data-testid="domain-autorenew-row-toggle"]').trigger('click')

    await waitUntil(() => expect(wrapper.emitted('update:enabled')).toEqual([[true]]))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
    expect(stub.requests[0]?.url).toBe(domainPath(DomainAction.AutoRenew))
    expect(bodyOf(stub.requests[0])).toEqual({ value: true })
  })

  it('leaves the switch where it was and shows the refusal when the API refuses', async () => {
    reply = refusal('Registrar refused.')
    const wrapper = mountClient(DomainAutoRenewPanel, { props: { enabled: false, domainId: DOMAIN_ID } })

    await wrapper.find('[data-testid="domain-autorenew-row-toggle"]').trigger('click')
    await waitUntil(() => expect(useDomainsStore().toggleError).toBe('Registrar refused.'))
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:enabled')).toBeUndefined()
    expect(wrapper.find('[data-testid="domain-toggle-error"]').text()).toBe('Registrar refused.')
  })
})
