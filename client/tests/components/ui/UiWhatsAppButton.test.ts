/**
 * Unit tests for WhatsAppButton: each channel renders only when the operator configured it, with
 * the right link.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import WhatsAppButton from '~/components/ui/UiWhatsAppButton.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('WhatsAppButton', () => {
  it('renders nothing when no channel is configured', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(WhatsAppButton)

    expect(wrapper.find('[data-testid="contact-whatsapp"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="contact-telegram"]').exists()).toBe(false)
  })

  it('links WhatsApp with the digits of the number and the encoded greeting', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactWhatsapp]: '+374 (10) 123-456' })

    const wrapper = mountWithKit(WhatsAppButton)

    const link = wrapper.find('[data-testid="contact-whatsapp"]')
    expect(link.attributes('href')).toBe('https://wa.me/37410123456?text=whatsapp.defaultMessage')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener noreferrer')
    expect(link.attributes('aria-label')).toBe('whatsapp.contactUs')
  })

  it('links Telegram with the handle', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactTelegram]: 'hostpanel_support' })

    const wrapper = mountWithKit(WhatsAppButton)

    expect(wrapper.find('[data-testid="contact-telegram"]').attributes('href')).toBe('https://t.me/hostpanel_support')
    expect(wrapper.find('[data-testid="contact-whatsapp"]').exists()).toBe(false)
  })

  it('shows both channels when both are configured', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactWhatsapp]: '37410123456', [PortalSettingKey.ContactTelegram]: 'support' })

    const wrapper = mountWithKit(WhatsAppButton)

    expect(wrapper.findAll('a')).toHaveLength(2)
  })
})
