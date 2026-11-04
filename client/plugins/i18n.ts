/**
 * i18n Plugin
 * Dynamically loads all locale files
 */
import { APP_LOCALES } from '~/constants/appLocales'

/** Locale files merged into one message set per locale. */
const MODULES = ['common', 'nav', 'hero', 'products', 'contact', 'faq', 'cta', 'whychooseus', 'team', 'partners', 'process', 'testimonials', 'footer', 'terms', 'legal', 'privacy', 'whatsapp', 'error', 'cookieBanner', 'cookiePolicy', 'refundPolicy', 'aup', 'seo', 'domains', 'hosting', 'cart', 'checkout', 'client', 'configure', 'announcements', 'invoicePay', 'knowledgebase', 'trial', 'aurora', 'nova', 'paymentResult', 'demo', 'about', 'hostingSections', 'storefront', 'trust', 'signInPrompt', 'plugins']

export default defineNuxtPlugin({
  name: 'i18n-messages',
  async setup({ $i18n }) {
    for (const locale of APP_LOCALES) {
      const messages: Record<string, unknown> = {}

      for (const module of MODULES) {
        try {
          const data = await import(`~/locales/${locale}/${module}.json`)
          Object.assign(messages, data.default || data)
        } catch {
          // Silently skip missing locale files
        }
      }

      $i18n.setLocaleMessage(locale, messages)
    }
  },
})
