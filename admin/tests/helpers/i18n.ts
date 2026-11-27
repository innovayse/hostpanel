/**
 * i18n plugin for component tests.
 *
 * Mounts get the real English catalogue, so a test asserts the text a reader would see instead of
 * a key, and a missing key shows up as the key itself.
 */
import { createI18n } from 'vue-i18n'
import { MessageLanguage, messages } from '@/locales/messages'

/**
 * Builds a fresh i18n instance (one per mount, so tests never share state).
 *
 * @param locale - Interface language of the mount; English when omitted.
 * @returns A vue-i18n plugin in composition mode with the English messages and those of the locale.
 */
export const createTestI18n = (locale: MessageLanguage = MessageLanguage.En) =>
  createI18n({ legacy: false, locale, fallbackLocale: 'en', messages: { en: messages.en, [locale]: messages[locale] } })
