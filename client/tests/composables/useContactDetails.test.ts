/**
 * Tests for `useContactDetails`: the links are built from the operator's settings and every
 * item is empty (so its row is hidden) when the setting is not set.
 *
 * @module tests/composables/useContactDetails.test
 */
import { describe, expect, it } from 'vitest'
import { useContactDetails } from '~/composables/useContactDetails'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { localizedSettingKey } from '~/utils/localizedSetting'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useContactDetails', () => {
  it('builds the phone, Telegram and WhatsApp links from the configured values', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.ContactEmail]: 'hello@acme.test',
      [PortalSettingKey.ContactPhone]: '+1 (555) 010-200',
      [PortalSettingKey.ContactTelegram]: '@acme',
      [PortalSettingKey.ContactWhatsapp]: '+1 555 010 200',
      [PortalSettingKey.ContactAddress]: '1 Example Street',
      [PortalSettingKey.ContactMapUrl]: 'https://maps.example.test/embed',
    })

    const details = useContactDetails()

    expect(details.email.value).toBe('hello@acme.test')
    expect(details.phone.value).toBe('+1 (555) 010-200')
    expect(details.phoneLink.value).toBe('+1555010200')
    expect(details.telegramUrl.value).toBe('https://t.me/acme')
    expect(details.whatsappUrl.value).toBe('https://wa.me/1555010200')
    expect(details.address.value).toBe('1 Example Street')
    expect(details.mapUrl.value).toBe('https://maps.example.test/embed')
  })

  it('answers empty strings when nothing is configured', async () => {
    await loadPortalSettings(replies)

    const details = useContactDetails()

    expect(details.email.value).toBe('')
    expect(details.phoneLink.value).toBe('')
    expect(details.telegramUrl.value).toBe('')
    expect(details.whatsappUrl.value).toBe('')
    expect(details.address.value).toBe('')
    expect(details.mapUrl.value).toBe('')
  })

  it('refuses a map address that is not https', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactMapUrl]: 'http://maps.example.test/embed' })

    expect(useContactDetails().mapUrl.value).toBe('')
  })

  it('reads the address in the visitor language and falls back to the default one', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.ContactAddress]: '1 Example Street',
      [localizedSettingKey(PortalSettingKey.ContactAddress, 'ru')]: 'ул. Примерная, 1',
      [localizedSettingKey(PortalSettingKey.ContactAddress, 'es')]: 'Calle Ejemplo 1',
    })

    stubLocale('ru')
    expect(useContactDetails().address.value).toBe('ул. Примерная, 1')

    stubLocale('es')
    expect(useContactDetails().address.value).toBe('Calle Ejemplo 1')

    stubLocale('hy')
    expect(useContactDetails().address.value).toBe('1 Example Street')
  })
})
