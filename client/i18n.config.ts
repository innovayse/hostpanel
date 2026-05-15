/**
 * i18n Configuration
 * Loads translation messages from locale files
 */
export default defineI18nConfig(() => ({
  legacy: false,
  locale: 'en',
  fallbackLocale: 'en',
  missingWarn: false,
  fallbackWarn: false,
  globalInjection: true,
  silentFallbackWarn: true,
  silentTranslationWarn: true
}))
