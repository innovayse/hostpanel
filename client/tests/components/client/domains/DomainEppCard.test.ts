/**
 * Unit tests for DomainEppCard: the lock warning, the request and the code or refusal shown. The
 * store, the API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainEppCard from '~/components/client/domains/DomainEppCard.vue'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { refusal } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers the code request with; a test changes it before it clicks. */
let reply: StubReply

beforeEach(async () => {
  reply = { body: { eppCode: 'AUTH-123' } }
  stub = await startTransport(() => reply)
})

afterEach(async () => {
  await stub.close()
})

describe('DomainEppCard', () => {
  it('warns that a locked domain cannot be transferred', () => {
    const locked = mountClient(DomainEppCard, { props: { domainId: DOMAIN_ID, lockEnabled: true } })
    const unlocked = mountClient(DomainEppCard, { props: { domainId: DOMAIN_ID, lockEnabled: false } })

    expect(locked.find('[data-testid="domain-epp-lock-warning"]').exists()).toBe(true)
    expect(unlocked.find('[data-testid="domain-epp-lock-warning"]').exists()).toBe(false)
  })

  it('requests the code for this domain', async () => {
    const wrapper = mountClient(DomainEppCard, { props: { domainId: DOMAIN_ID, lockEnabled: false } })

    await wrapper.find('[data-testid="domain-epp-request"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="domain-epp-message"]').exists()).toBe(true))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Post)
    expect(stub.requests[0]?.url).toBe(domainPath(DomainAction.Epp))
    expect(wrapper.find('[data-testid="domain-epp-message"]').text()).toBe('AUTH-123')
  })

  it('shows the refusal in the error colour', async () => {
    reply = refusal('Not allowed.')
    const wrapper = mountClient(DomainEppCard, { props: { domainId: DOMAIN_ID, lockEnabled: false } })

    await wrapper.find('[data-testid="domain-epp-request"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="domain-epp-message"]').exists()).toBe(true))
    expect(wrapper.find('[data-testid="domain-epp-message"]').text()).toBe('Not allowed.')
    expect(wrapper.find('[data-testid="domain-epp-message"]').classes()).toContain('bg-red-50')
  })
})
