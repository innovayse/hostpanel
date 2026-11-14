/**
 * Mount tests for Cwp7ServerCard: a server as a link to the servers page with its state dot, hostname, address and
 * account usage, and the same layout with placeholder bars and no link while there is no server yet.
 */
import { describe, expect, it } from 'vitest'
import Cwp7ServerCard from '@/components/integrations/Cwp7ServerCard.vue'
import { serverDto } from '../../helpers/serverFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('Cwp7ServerCard', () => {
  it('links to the servers page and shows the hostname, address and account usage', () => {
    const wrapper = mountWithApp(Cwp7ServerCard, { props: { server: serverDto() } })
    const card = wrapper.get('[data-testid="cwp7-server-1"]')

    expect(card.attributes('href')).toBe('/servers')
    expect(card.text()).toContain('host1')
    expect(card.text()).toContain('host1.example.com')
    expect(card.text()).toContain('203.0.113.10')
    expect(card.text()).toContain('12 / 100')
  })

  it('marks a disabled server with a badge in place of the state dot', () => {
    const wrapper = mountWithApp(Cwp7ServerCard, { props: { server: serverDto({ limits: { maxAccounts: 100, isDefault: false, isDisabled: true } }) } })

    expect(wrapper.text()).toContain('Disabled')
  })

  it('is no link and draws bars while there is no server', () => {
    const wrapper = mountWithApp(Cwp7ServerCard)
    const card = wrapper.get('[data-testid="cwp7-server-loading"]')

    expect(card.attributes('href')).toBeUndefined()
    expect(wrapper.attributes('aria-busy')).toBeUndefined()
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(4)
  })
})
