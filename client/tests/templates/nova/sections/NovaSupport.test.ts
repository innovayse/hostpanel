/**
 * Unit tests for NovaSupport: a card per channel, the response time when set, and a link only for
 * channels that have an address.
 *
 * @module tests/templates/nova/sections/NovaSupport.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import NovaSupport from '~/templates/nova/sections/NovaSupport.vue'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useSupportChannels })

/** The operator has named a live-chat provider, which is what puts the chat channel on the list. */
const CHAT_PROVIDER: Record<string, string> = { [PortalSettingKey.ChatProvider]: 'tawk' }

describe('NovaSupport', () => {
  it('renders a card per channel', async () => {
    await loadPortalSettings(replies, CHAT_PROVIDER)

    expect(mountWithKit(NovaSupport).findAll('[data-testid="nova-support-grid"] > li')).toHaveLength(2)
  })

  it('links only the channels that have an address', async () => {
    await loadPortalSettings(replies, { ...CHAT_PROVIDER, [PortalSettingKey.ContactEmail]: 'help@example.test' })
    const wrapper = mountWithKit(NovaSupport)

    expect(wrapper.find('[data-testid="nova-support-email"] [data-testid="nova-support-link"]').attributes('href')).toBe('mailto:help@example.test')
    expect(wrapper.find('[data-testid="nova-support-chat"] [data-testid="nova-support-link"]').exists()).toBe(false)
  })

  it('shows the response time only where one is set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactPhone]: '+1 555 0100', [PortalSettingKey.SupportResponsePhone]: '2 min' })
    const wrapper = mountWithKit(NovaSupport)

    expect(wrapper.find('[data-testid="nova-support-phone"] [data-testid="nova-support-response"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nova-support-ticket"] [data-testid="nova-support-response"]').exists()).toBe(false)
  })
})
