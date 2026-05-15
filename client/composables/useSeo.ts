/**
 * SEO composable for consistent meta tags across all pages
 * Handles canonical URLs, hreflang tags, Open Graph, and Twitter Cards
 *
 * Note: TypeScript may show errors for auto-imported composables (useI18n, useRoute, etc.)
 * during typecheck, but these are false positives - Nuxt auto-imports work at runtime.
 */

export const useSeo = (options: {
  title?: string
  description?: string
  keywords?: string
  image?: string
  type?: 'website' | 'article'
  path?: string
}) => {
  const { locale } = useI18n()
  const config = useRuntimeConfig()
  const route = useRoute()

  const baseUrl = config.public.baseUrl

  // Get current path or use provided path
  const currentPath = options.path || route.path

  // Build canonical URL with locale
  const canonicalUrl = computed(() => {
    // Remove locale prefix for English (default)
    const pathWithoutLocale = currentPath.replace(/^\/(en|ru|hy)/, '')

    if (locale.value === 'en') {
      return `${baseUrl}${pathWithoutLocale || '/'}`
    }
    return `${baseUrl}/${locale.value}${pathWithoutLocale || ''}`
  })

  // Build alternate URLs for hreflang
  const getAlternateUrl = (lang: string) => {
    const pathWithoutLocale = currentPath.replace(/^\/(en|ru|hy)/, '')

    if (lang === 'en') {
      return `${baseUrl}${pathWithoutLocale || '/'}`
    }
    return `${baseUrl}/${lang}${pathWithoutLocale || ''}`
  }

  // Default image
  const defaultImage = `${baseUrl}/og-image.jpg`
  const imageUrl = options.image ? `${baseUrl}${options.image}` : defaultImage

  // OG Locale mapping
  const ogLocaleMap: Record<string, string> = {
    en: 'en_US',
    ru: 'ru_RU',
    hy: 'hy_AM'
  }

  // Set meta tags
  useSeoMeta({
    title: options.title,
    description: options.description,
    keywords: options.keywords,

    // Open Graph
    ogTitle: options.title,
    ogDescription: options.description,
    ogImage: imageUrl,
    ogUrl: canonicalUrl.value,
    ogType: options.type || 'website',
    ogLocale: ogLocaleMap[locale.value] || 'en_US',

    // Twitter Card
    twitterCard: 'summary_large_image',
    twitterTitle: options.title,
    twitterDescription: options.description,
    twitterImage: imageUrl
  })

  // Set canonical and hreflang
  useHead({
    link: [
      // Canonical URL
      { rel: 'canonical', href: canonicalUrl.value },
      // Hreflang tags
      { rel: 'alternate', hreflang: 'en', href: getAlternateUrl('en') },
      { rel: 'alternate', hreflang: 'ru', href: getAlternateUrl('ru') },
      { rel: 'alternate', hreflang: 'hy', href: getAlternateUrl('hy') },
      { rel: 'alternate', hreflang: 'x-default', href: getAlternateUrl('en') }
    ]
  })

  return {
    canonicalUrl,
    baseUrl
  }
}
