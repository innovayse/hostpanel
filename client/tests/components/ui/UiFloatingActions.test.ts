/**
 * Unit tests for FloatingActions: the fan opens and closes, each action appears only when the
 * operator configured it, and Escape or an outside click closes the fan.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import FloatingActions from '~/components/ui/UiFloatingActions.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { LIVE_CHAT_API_GLOBAL, openLiveChat } from '~/utils/liveChat'
import { LiveChatPanelState } from '~/types/livechatpanelstate'
import { ChatProvider } from '~/types/chatprovider'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ openLiveChat })

/**
 * Mounts the component attached to the page so document listeners see its elements.
 *
 * @returns The wrapper.
 */
const mountAttached = () => mountWithKit(FloatingActions, { attachToBody: true })

describe('FloatingActions', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    Reflect.deleteProperty(window, LIVE_CHAT_API_GLOBAL)
  })

  it('starts closed with only the toggle button', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactWhatsapp]: '37410123456' })

    const wrapper = mountAttached()

    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('false')
    expect(wrapper.find('[data-testid="floating-whatsapp"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('opens the fan with the configured channels only', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactWhatsapp]: '37410123456', [PortalSettingKey.ContactTelegram]: 'support' })
    const wrapper = mountAttached()

    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.find('[data-testid="floating-whatsapp"]').attributes('href')).toContain('https://wa.me/37410123456')
    expect(wrapper.find('[data-testid="floating-telegram"]').attributes('href')).toBe('https://t.me/support')
    expect(wrapper.find('[data-testid="floating-live-chat"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it.each([[ChatProvider.LegacyChat], [ChatProvider.Innochat], ['InnoChat']])('offers live chat for the %s provider', async (provider) => {
    await loadPortalSettings(replies, { [PortalSettingKey.ChatProvider]: provider })
    const wrapper = mountAttached()

    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="floating-live-chat"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('does not offer live chat for an unknown provider', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ChatProvider]: 'other' })
    const wrapper = mountAttached()

    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="floating-live-chat"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('opens the widget and closes the fan after choosing live chat', async () => {
    const toggle = vi.fn()
    Reflect.set(window, LIVE_CHAT_API_GLOBAL, { toggle })
    await loadPortalSettings(replies, { [PortalSettingKey.ChatProvider]: ChatProvider.Innochat })
    const wrapper = mountAttached()
    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    await wrapper.find('[data-testid="floating-live-chat"]').trigger('click')

    expect(toggle).toHaveBeenCalledWith(LiveChatPanelState.Open)
    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('false')
    wrapper.unmount()
  })

  it('closes the fan after choosing live chat even when the widget is not running yet', async () => {
    // The component logs that the widget is not running; the log is silenced here, not what this test is about.
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    await loadPortalSettings(replies, { [PortalSettingKey.ChatProvider]: ChatProvider.Innochat })
    const wrapper = mountAttached()
    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    await wrapper.find('[data-testid="floating-live-chat"]').trigger('click')

    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('false')
    wrapper.unmount()
  })

  it('closes on Escape', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactTelegram]: 'support' })
    const wrapper = mountAttached()
    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()

    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('false')
    wrapper.unmount()
  })

  it('sits above the cookie banner through the shared height variable', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountAttached()

    expect(wrapper.find('[data-testid="floating-actions"]').classes())
      .toContain('bottom-[calc(1.5rem+var(--cookie-banner-height,0px))]')
    wrapper.unmount()
  })

  it('paints the toggle with brand tokens, readable in light and dark', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountAttached()
    const toggle = wrapper.find('[data-testid="floating-toggle"]').classes()

    expect(toggle).toEqual(expect.arrayContaining(['bg-primary-600', 'text-on-primary', 'dark:bg-primary-500']))
    expect(toggle).not.toContain('from-channel-chat')
    wrapper.unmount()
  })

  it('closes on a click outside', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactTelegram]: 'support' })
    const wrapper = mountAttached()
    await wrapper.find('[data-testid="floating-toggle"]').trigger('click')

    document.body.click()
    await nextTick()

    expect(wrapper.find('[data-testid="floating-toggle"]').attributes('aria-expanded')).toBe('false')
    wrapper.unmount()
  })
})
