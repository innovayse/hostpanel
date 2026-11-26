/**
 * The operator's public contact details, read from the `portal.contact.*` settings.
 *
 * Nothing is written into the product: a fresh install, a self-hoster and the demo each show
 * only what their operator configured, and every item is an empty string (so its row is
 * hidden) when unset.
 *
 * @module composables/useContactDetails
 */

import { TELEGRAM_LINK_BASE, WHATSAPP_LINK_BASE } from '~/constants/contactChannel'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { HTTPS_PREFIX } from '~/constants/urlScheme'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'

/**
 * Reads the contact details and derives the link forms the pages need.
 *
 * @returns Reactive email, phone (as written and as a `tel:` target), Telegram and WhatsApp URLs.
 */
export const useContactDetails = () => {
  const { get } = usePortalSettings()
  const { getLocalized } = useLocalizedSetting()

  /** Address enquiries are sent to; empty when unset. */
  const email = computed(() => get(PortalSettingKey.ContactEmail, 'portalContactEmail'))

  /** Phone number as the operator wrote it, for display; empty when unset. */
  const phone = computed(() => get(PortalSettingKey.ContactPhone, 'portalContactPhone'))

  /** Phone number reduced to the characters a `tel:` link accepts; empty when unset. */
  const phoneLink = computed(() => phone.value.replace(/[^\d+]/g, ''))

  /** Telegram chat link built from the configured handle; empty when unset. */
  const telegramUrl = computed(() => {
    const handle = get(PortalSettingKey.ContactTelegram, 'portalTelegram').replace(/^@/, '')
    return handle ? `${TELEGRAM_LINK_BASE}${handle}` : ''
  })

  /** WhatsApp chat link built from the configured number; empty when unset. */
  const whatsappUrl = computed(() => {
    const digits = get(PortalSettingKey.ContactWhatsapp, 'portalWhatsapp').replace(/\D/g, '')
    return digits ? `${WHATSAPP_LINK_BASE}${digits}` : ''
  })

  /** Office address as the operator wrote it, in the visitor's language when one is set; empty when unset. */
  const address = computed(() => getLocalized({ key: PortalSettingKey.ContactAddress, envKey: 'portalContactAddress' }))

  /** Address of the embedded map; only an `https` address is accepted, otherwise empty. */
  const mapUrl = computed(() => {
    const url = get(PortalSettingKey.ContactMapUrl, 'portalContactMapUrl')
    return url.startsWith(HTTPS_PREFIX) ? url : ''
  })

  return { email, phone, phoneLink, telegramUrl, whatsappUrl, address, mapUrl }
}
