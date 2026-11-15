/**
 * Unit tests for SectionsSupport: one card per channel, the reply time and link only when the
 * channel carries them.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsSupport from '~/components/sections/SectionsSupport.vue'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

/** A chat provider, an e-mail address and a ticket reply time; no phone number. */
const SET_CHANNELS: Record<string, string> = {
  [PortalSettingKey.ChatProvider]: 'widget',
  [PortalSettingKey.ContactEmail]: 'help@example.test',
  [PortalSettingKey.SupportResponseTicket]: '15 min',
}

describe('SectionsSupport', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    vi.stubGlobal('useSupportChannels', useSupportChannels)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows a card for each channel', async () => {
    await loadPortalSettings(replies, SET_CHANNELS)

    const wrapper = mountWithKit(SectionsSupport, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="support-ticket"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="support-email"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="support-phone"]').exists()).toBe(false)
  })

  it('shows the reply time only when the channel has one', async () => {
    await loadPortalSettings(replies, SET_CHANNELS)

    const wrapper = mountWithKit(SectionsSupport, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="support-response-ticket"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="support-response-email"]').exists()).toBe(false)
  })

  it('links a channel that has a target and shows its value', async () => {
    await loadPortalSettings(replies, SET_CHANNELS)

    const wrapper = mountWithKit(SectionsSupport, { components: CLASSIC_COMPONENTS })

    const link = wrapper.find('[data-testid="support-link-email"]')
    expect(link.attributes('href')).toBe('mailto:help@example.test')
    expect(link.text()).toBe('help@example.test')
    expect(wrapper.find('[data-testid="support-link-chat"]').exists()).toBe(false)
  })

  it('draws every icon on a flat brand tile with no accent glow', async () => {
    await loadPortalSettings(replies, SET_CHANNELS)

    const wrapper = mountWithKit(SectionsSupport, { components: CLASSIC_COMPONENTS })

    const tiles = wrapper.findAll('[data-testid="classic-icon-tile"]')
    expect(tiles.length).toBeGreaterThan(0)
    expect(wrapper.html()).not.toContain('--accent')
  })

  it('never caps the grid width: the shared container sets it for any number of cards', async () => {
    /** The settings that switch on one more card at a time: the ticket form is always there, then e-mail, then chat. */
    const cardSettings: Record<string, string>[] = [
      {},
      { [PortalSettingKey.ContactEmail]: 'help@example.test' },
      { [PortalSettingKey.ContactEmail]: 'help@example.test', [PortalSettingKey.ChatProvider]: 'widget' },
    ]
    for (const settings of cardSettings) {
      await loadPortalSettings(replies, settings)
      const wrapper = mountWithKit(SectionsSupport, { components: CLASSIC_COMPONENTS })
      const classes = wrapper.find('[data-testid="sections-support-grid"]').classes()

      expect(classes.some(name => name.startsWith('max-w-'))).toBe(false)
    }
  })
})
