/**
 * Unit tests for DomainNameserversPanel: filling the fields from the domain already loaded, the
 * default/custom choice, saving the non-empty fields in order and re-reading the domain after. The
 * store, the API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainNameserversPanel from '~/components/client/domains/DomainNameserversPanel.vue'
import { useDomainsStore } from '~/stores/domains'
import type { DomainDetail } from '~/types/domaindetail'
import type { DomainNameserver } from '~/types/domainnameserver'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it submits. */
let table: StubRouteTable

/**
 * Builds a domain carrying the given nameservers in its management settings.
 *
 * @param hosts - The nameserver hostnames, in order.
 * @returns The domain.
 */
const domainWith = (hosts: string[]): DomainDetail => ({
  id: 7,
  name: 'example.com',
  management: {
    dnsManagement: false,
    emailForwarding: false,
    nameservers: hosts.map((host, index): DomainNameserver => ({ id: index + 1, host })),
    dnsRecords: [],
    emailForwardingRules: []
  }
} as unknown as DomainDetail)

/**
 * Mounts the panel with the domains store already holding the domain, as the page leaves it.
 *
 * @param hosts - The nameservers the loaded domain has.
 * @returns The mounted panel.
 */
const mountPanel = (hosts: string[]): ReturnType<typeof mountClient> => {
  const pinia = createPinia()
  setActivePinia(pinia)
  useDomainsStore().domain = domainWith(hosts)
  return mountClient(DomainNameserversPanel, { props: { domainId: DOMAIN_ID }, pinia })
}

/**
 * Reads the value of a nameserver field.
 *
 * @param wrapper - The mounted panel.
 * @param position - 1-based field number.
 * @returns The field's current value.
 */
const field = (wrapper: ReturnType<typeof mountClient>, position: number): string =>
  (wrapper.find(`[data-testid="domain-ns-${position}"]`).element as HTMLInputElement).value

beforeEach(async () => {
  table = {}
  stub = await startTransport(request => routeTable(table)(request))
})

afterEach(async () => {
  await stub.close()
})

describe('DomainNameserversPanel', () => {
  it('fills the fields from the loaded domain without a request of its own and selects custom', () => {
    const wrapper = mountPanel(['ns1.example.net', 'ns2.example.net'])

    expect(field(wrapper, 1)).toBe('ns1.example.net')
    expect(field(wrapper, 2)).toBe('ns2.example.net')
    expect(stub.requests).toHaveLength(0)
    expect(wrapper.find('[data-testid="domain-ns-custom"]').attributes('aria-checked')).toBe('true')
    // Five nameserver fields; the two radios are inputs too but not fields.
    expect(wrapper.findAll('input:not([type="radio"])')).toHaveLength(5)
  })

  it('selects the defaults and locks the fields when the domain has no nameservers', () => {
    const wrapper = mountPanel([])

    expect(wrapper.find('[data-testid="domain-ns-default"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="domain-ns-1"]').attributes('disabled')).toBeDefined()
  })

  it('saves only the filled fields, in order, re-reads the domain and confirms', async () => {
    table[`PUT ${domainPath(DomainAction.Nameservers)}`] = { body: {} }
    table[`GET ${domainPath('')}`] = { body: domainWith(['ns1.example.net', 'ns2.example.net', 'ns3.example.net']) }
    const wrapper = mountPanel(['ns1.example.net', 'ns2.example.net'])

    await wrapper.find('[data-testid="domain-ns-3"]').setValue('ns3.example.net')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-success"]').text()).toContain('client.domains.nsSuccess'))
    expect(stub.requests).toHaveLength(2)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
    expect(stub.requests[0]?.url).toBe(domainPath(DomainAction.Nameservers))
    expect(bodyOf(stub.requests[0])).toEqual({ nameservers: ['ns1.example.net', 'ns2.example.net', 'ns3.example.net'] })
    expect(stub.requests[1]?.method).toBe(HttpMethod.Get)
    expect(stub.requests[1]?.url).toBe(domainPath(''))
    expect(useDomainsStore().domain?.management.nameservers).toHaveLength(3)
  })

  it('shows the refusal and no confirmation when the save fails', async () => {
    table[`PUT ${domainPath(DomainAction.Nameservers)}`] = refusal('Invalid nameserver.')
    const wrapper = mountPanel(['ns1.example.net'])

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Invalid nameserver.'))
    expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(false)
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
  })
})
