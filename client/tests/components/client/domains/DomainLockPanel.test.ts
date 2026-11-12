/**
 * Unit tests for DomainLockPanel: both switches move only when the API accepts the change. The
 * store, the API composables and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainLockPanel from '~/components/client/domains/DomainLockPanel.vue'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { refusal } from '~/tests/helpers/stubRoutes'
import { bodyOf } from '~/tests/helpers/requestBody'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers the switch change with; a test changes it before it clicks. */
let reply: StubReply

/**
 * Mounts the panel with both switches off.
 *
 * @returns The wrapper.
 */
const mountPanel = () => mountClient(DomainLockPanel, { props: { domainId: DOMAIN_ID, lockEnabled: false, idProtectEnabled: false } })

beforeEach(async () => {
  reply = { body: {} }
  stub = await startTransport(() => reply)
})

afterEach(async () => {
  await stub.close()
})

describe('DomainLockPanel', () => {
  it('shows the lock, the ID protection and the EPP card', () => {
    const wrapper = mountPanel()

    expect(wrapper.find('[data-testid="domain-lock-row"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-idprotect-row"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-epp"]').exists()).toBe(true)
  })

  it('turns the lock on when the API accepts', async () => {
    const wrapper = mountPanel()

    await wrapper.find('[data-testid="domain-lock-row-toggle"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.emitted('update:lockEnabled')).toEqual([[true]]))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
    expect(stub.requests[0]?.url).toBe(domainPath(DomainAction.Lock))
    expect(bodyOf(stub.requests[0])).toEqual({ value: true })
  })

  it('turns ID protection on when the API accepts', async () => {
    const wrapper = mountPanel()

    await wrapper.find('[data-testid="domain-idprotect-row-toggle"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.emitted('update:idProtectEnabled')).toEqual([[true]]))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
    expect(stub.requests[0]?.url).toBe(domainPath(DomainAction.IdProtect))
    expect(bodyOf(stub.requests[0])).toEqual({ value: true })
  })

  it('leaves the lock off and shows the refusal when the API refuses', async () => {
    reply = refusal('Registrar refused.')
    const wrapper = mountPanel()

    await wrapper.find('[data-testid="domain-lock-row-toggle"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="domain-toggle-error"]').exists()).toBe(true))
    expect(wrapper.emitted('update:lockEnabled')).toBeUndefined()
  })
})
