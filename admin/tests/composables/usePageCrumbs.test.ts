/**
 * Tests for usePageCrumbs: a page's trail is the navigation's parents plus the page, and a client page's trail is the
 * client list, the open client, the section and the page, in the active language.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { ClientSection } from '@/lib/routes'
import { useClientsStore } from '@/stores/clients'
import type { ClientDetail } from '@/types/client'
import { mountScreen } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** A host that prints the trails as `a > b`, so a test reads them as text. */
const Host = defineComponent({
  /**
   * Draws the page trail and the client trail, each as its labels joined by an arrow.
   *
   * @returns The render function.
   */
  setup() {
    const { crumbs, clientCrumbs, sectionCrumbs } = usePageCrumbs()
    /** Joins crumbs into one readable line; a pending crumb shows as dots. */
    const join = (items: { label: string; pending?: boolean }[]): string => items.map((item) => (item.pending === true ? '...' : item.label)).join(' > ')
    return () => h('div', [
      h('p', { 'data-testid': 'page' }, join(crumbs('#1001'))),
      h('p', { 'data-testid': 'pending' }, join(crumbs())),
      h('p', { 'data-testid': 'client' }, join(clientCrumbs(7, ClientSection.Domains, 'example.com'))),
      h('p', { 'data-testid': 'section' }, join(sectionCrumbs(7, ClientSection.Domains))),
    ])
  },
})

describe('usePageCrumbs', () => {
  it('builds the trail of a page in the navigation from its parents and its own name', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/orders/8', '/orders/:id')

    expect(wrapper.get('[data-testid="page"]').text()).toBe('Orders > #1001')
  })

  it('draws a pending last step while the page name is not known', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/billing/invoices/3', '/billing/invoices/:id')

    expect(wrapper.get('[data-testid="pending"]').text()).toBe('Billing > Invoices > ...')
  })

  it('builds the trail of a client page from the open client, its section and the page', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/clients/7/domains/4', '/clients/:id/domains/:domainId')
    useClientsStore().current = { id: 7, firstName: 'Ada', lastName: 'Lovelace' } as ClientDetail
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="client"]').text()).toBe('Clients > Ada Lovelace > Domains > example.com')
  })

  it('names the client by its id until the client has loaded', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/clients/7/domains/4', '/clients/:id/domains/:domainId')

    expect(wrapper.get('[data-testid="client"]').text()).toBe('Clients > #7 > Domains > example.com')
  })

  it('builds the trail of a section page of the client, ending at the section', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/clients/7/domains', '/clients/:id/domains')
    useClientsStore().current = { id: 7, firstName: 'Ada', lastName: 'Lovelace' } as ClientDetail
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="section"]').text()).toBe('Clients > Ada Lovelace > Domains')
  })
})
