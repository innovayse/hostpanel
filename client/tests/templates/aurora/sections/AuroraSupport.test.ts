/**
 * Unit tests for AuroraSupport: a card per channel, links only where the channel has one, the response time only when set.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraSupport from '~/templates/aurora/sections/AuroraSupport.vue'
import { CLIENT_TICKET_NEW_PATH } from '~/constants/clientRoutes'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

describe('AuroraSupport', () => {
  it('links a channel that has an address and shows its value and response time', () => {
    stubAuroraGlobals({
      settings: {
        [PortalSettingKey.ChatProvider]: 'widget',
        [PortalSettingKey.SupportResponseChat]: '2 min',
        [PortalSettingKey.ContactEmail]: 'help@example.test',
      },
    })

    const wrapper = mountWithKit(AuroraSupport)

    expect(wrapper.find('[data-testid="support-email"]').attributes('href')).toBe('mailto:help@example.test')
    expect(wrapper.find('[data-testid="support-value-email"]').text()).toBe('help@example.test')
    expect(wrapper.find('[data-testid="support-chat"]').attributes('href')).toBeUndefined()
    expect(wrapper.find('[data-testid="support-response-chat"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="support-response-email"]').exists()).toBe(false)
  })

  it('always offers the ticket form, so the section renders without any other channel', () => {
    stubAuroraGlobals()

    const wrapper = mountWithKit(AuroraSupport)

    expect(wrapper.find('[data-testid="aurora-support"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="support-ticket"]').attributes('href')).toBe(CLIENT_TICKET_NEW_PATH)
  })

  it('lays the cards out in the balanced grid', () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.ContactPhone]: '+1 555 0100' } })

    const grid = mountWithKit(AuroraSupport).find('[data-testid="aurora-support-grid"]')

    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(2)))
  })
})
