/**
 * Mount tests for Cwp7ServerCard: a server as a link to the servers page with its state dot, hostname, address and
 * account usage, the state dot of each known state, and the same layout with placeholder bars and no link while there is no server yet.
 */
import { describe, expect, it } from 'vitest'
import Cwp7ServerCard from '@/components/integrations/Cwp7ServerCard.vue'
import { toneDotClasses, UiTone } from '@/utils/ui/tone'
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
    expect(card.text()).toContain('12 / 100 accounts')
  })

  it.each([
    ['online', true, UiTone.Success],
    ['offline', false, UiTone.Danger],
    ['untested', null, UiTone.Neutral],
  ])('draws the %s state dot in its tone', (_state, isOnline, tone) => {
    const wrapper = mountWithApp(Cwp7ServerCard, { props: { server: serverDto({ isOnline }) } })

    expect(wrapper.get('span.rounded-full').classes()).toContain(toneDotClasses[tone])
  })

  it('shows the account count alone for an unlimited server and no usage line when the count is unknown', () => {
    const unlimited = mountWithApp(Cwp7ServerCard, { props: { server: serverDto({ accountsCount: 7, limits: { maxAccounts: null, isDefault: false, isDisabled: false } }) } })
    const unknown = mountWithApp(Cwp7ServerCard, { props: { server: serverDto({ accountsCount: null }) } })

    expect(unlimited.text()).toContain('7 accounts')
    expect(unlimited.text()).not.toContain('/')
    expect(unknown.text()).not.toContain('accounts')
  })

  it('marks a disabled server with a badge in place of the state dot', () => {
    const wrapper = mountWithApp(Cwp7ServerCard, { props: { server: serverDto({ limits: { maxAccounts: 100, isDefault: false, isDisabled: true } }) } })

    expect(wrapper.text()).toContain('Disabled')
  })

  it('is no link and draws bars while there is no server', () => {
    const wrapper = mountWithApp(Cwp7ServerCard)
    const card = wrapper.get('[data-testid="cwp7-server-loading"]')

    expect(card.attributes('href')).toBeUndefined()
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(4)
  })
})
