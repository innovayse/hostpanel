/**
 * SEO composable for consistent meta tags across all pages
 * Handles canonical URLs, hreflang tags, Open Graph, and Twitter Cards
 *
 * Note: TypeScript may show errors for auto-imported composables (useI18n, useRoute, etc.)
 * during typecheck, but these are false positives - Nuxt auto-imports work at runtime.
 *
 * @module composables/useSeo
 */

import type { MaybeRefOrGetter } from 'vue'
import { AppLocale } from '~/types/applocale'
import { SeoPageType } from '~/types/seopagetype'
import { buildSeoLinks, localizedUrl, stripLocalePrefix } from '~/utils/seoLinks'
import { shareImageMeta } from '~/utils/shareImage'

/** What a page tells the SEO composable about itself. */
export interface SeoOptions {
  /** Page title. */
  title?: string
  /** Meta description. */
  description?: string
  /** Meta keywords. */
  keywords?: string
  /** Share image path, relative to the site root. */
  image?: string
  /** Open Graph object type. */
  type?: SeoPageType
  /** Path the canonical and alternate URLs are built from. */
  path?: string
}

/**
 * Sets the head tags a public page needs: title, description, canonical, hreflang and share cards.
 *
 * The options may be a getter: every tag then follows the values it returns, so a page whose
 * content arrives after setup, or changes with the route or the language, never leaves stale tags.
 *
 * @param options What the page knows about itself, or a getter returning it.
 * @returns The canonical URL builder and the page's base values.
 */
export const useSeo = (options: MaybeRefOrGetter<SeoOptions>) => {
  const { locale } = useI18n()
  const config = useRuntimeConfig()
  const route = useRoute()
  const { name: siteName } = useSiteIdentity()

  const baseUrl = config.public.baseUrl

  // What the page says about itself right now.
  const page = computed(() => toValue(options))

  // The page path in the default language, which every URL below is built from: the path the page
  // provides or the current route path.
  const pagePath = computed(() => stripLocalePrefix(page.value.path || route.path))

  // Build canonical URL with locale
  const canonicalUrl = computed(() => localizedUrl(baseUrl, pagePath.value, locale.value))

  // Share image: the page's own, or the bundled card with its size and type. The alt text
  // names the page, or the site when the page has no title.
  const image = computed(() => shareImageMeta(baseUrl, page.value.title || siteName.value, page.value.image))

  // OG Locale mapping
  const ogLocaleMap: Record<string, string> = {
    [AppLocale.En]: 'en_US',
    [AppLocale.Ru]: 'ru_RU',
    [AppLocale.Hy]: 'hy_AM',
    [AppLocale.Fr]: 'fr_FR'
  }

  // Set meta tags
  useSeoMeta({
    title: () => page.value.title,
    description: () => page.value.description,

    // Open Graph
    ogTitle: () => page.value.title,
    ogDescription: () => page.value.description,
    ogImage: () => image.value.ogImage,
    ogImageAlt: () => image.value.ogImageAlt,
    ogImageWidth: () => image.value.ogImageWidth,
    ogImageHeight: () => image.value.ogImageHeight,
    ogImageType: () => image.value.ogImageType,
    ogUrl: () => canonicalUrl.value,
    ogType: () => page.value.type || SeoPageType.Website,
    ogLocale: () => ogLocaleMap[locale.value] || 'en_US',

    // Twitter Card
    twitterCard: 'summary_large_image',
    twitterTitle: () => page.value.title,
    twitterDescription: () => page.value.description,
    twitterImage: () => image.value.twitterImage,
    twitterImageAlt: () => image.value.twitterImageAlt
  })

  // Set canonical and hreflang, and the keywords tag. `useSeoMeta` has no `keywords` member in the
  // current head library, so it is a plain meta entry here.
  useHead(() => ({
    link: buildSeoLinks(baseUrl, pagePath.value, locale.value),
    meta: page.value.keywords ? [{ name: 'keywords', content: page.value.keywords }] : []
  }))

  return {
    canonicalUrl,
    baseUrl
  }
}
