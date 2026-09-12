<template>
  <div>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>

    <!-- Cookie Consent Banner -->
    <UiCookieBanner />
  </div>
</template>

<script setup lang="ts">
/**
 * Root app component: global structured data, the favicon set and the site identity
 * metas. Third-party scripts live in plugins, driven by the operator's settings.
 */

const { locale } = useI18n()
const { organizationSchema, localBusinessSchema, websiteSchema, injectSchema } = useSchemaOrg()

// Inject global schemas
injectSchema([
  organizationSchema(),
  localBusinessSchema(),
  websiteSchema()
])

const langMap: Record<string, string> = { en: 'en', ru: 'ru', hy: 'hy' }

// Live chat and the tag manager are loaded by plugins/live-chat.ts and
// plugins/tracking.client.ts from the operator's settings; nothing here names a
// server, a token or a container id.

/** Operator-uploaded favicon, admin-managed with an environment fallback. Empty keeps nuxt.config's static default. */
const { get: getPortalSetting } = usePortalSettings()
const faviconUrl = computed(() => getPortalSetting('portal.favicon', 'portalFavicon'))

/**
 * Every `<link rel="icon">` the page emits, built-in defaults included.
 *
 * An uploaded favicon expands to the whole browser/iOS/Android set the API generated beside
 * it; a pasted URL stays a single link, because only that one file is known to exist; and
 * with nothing set at all this is where the built-in mark comes from — `nuxt.config` no
 * longer carries its own icon links, because two places emitting them meant an uploaded
 * favicon rendered alongside the built-in one rather than replacing it.
 *
 * Kept as a `computed` rather than an inline arrow so the dependency on the settings state
 * is explicit; both forms work with `useHead`.
 */
const faviconLinks = computed(() => brandingIcons(faviconUrl.value).map(icon => ({
  rel: icon.rel,
  href: icon.href,
  ...(icon.type ? { type: icon.type } : {}),
  ...(icon.sizes ? { sizes: icon.sizes } : {})
})))

/**
 * The operator's site name and tagline. `og:site_name` and `author` used to be static
 * strings in nuxt.config's head; they are set here because the settings are only known
 * at request time. A page's own `useSeo()` still wins for `description`, because it
 * runs after this and unhead lets the later tag replace the earlier one.
 */
const { name: siteName, tagline: siteTagline } = useSiteIdentity()

useSeoMeta({
  ogSiteName: siteName,
  author: siteName,
  ...(siteTagline.value ? { description: siteTagline, ogDescription: siteTagline } : {})
})

useHead({
  htmlAttrs: {
    lang: () => langMap[locale.value] ?? 'en'
  },
  link: faviconLinks,
  // The colour-mode class on <html> is owned by plugins/color-mode.ts; the live-chat
  // loader by plugins/live-chat.ts; the tag manager by plugins/tracking.client.ts.
  // Nothing in this file should add a script or touch documentElement.classList.
})
</script>
