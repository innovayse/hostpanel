/**
 * i18n Plugin
 * Dynamically loads all locale files
 */
export default defineNuxtPlugin(async ({ $i18n }) => {
  const locales = ['en', 'ru', 'hy']
  const modules = ['common', 'nav', 'hero', 'products', 'productsSlider', 'contact', 'faq', 'cta', 'whychooseus', 'team', 'partners', 'process', 'testimonials', 'footer', 'terms', 'privacy', 'whatsapp', 'error', 'cookieBanner', 'cookiePolicy', 'refundPolicy', 'aup', 'seo', 'domains', 'hosting', 'cart', 'checkout', 'client', 'configure', 'order', 'announcements', 'invoicePay', 'knowledgebase', 'trial', 'aurora', 'nova', 'paymentResult']

  for (const locale of locales) {
    const messages: Record<string, unknown> = {}

    for (const module of modules) {
      try {
        const data = await import(`~/locales/${locale}/${module}.json`)
        Object.assign(messages, data.default || data)
      } catch {
        // Silently skip missing locale files
      }
    }

    // @ts-expect-error i18n types
    $i18n.setLocaleMessage(locale, messages)
  }
})
