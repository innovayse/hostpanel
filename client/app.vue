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
 * Root app component
 * Injects global SEO schemas and Google Tag Manager
 */

const { locale } = useI18n()
const { organizationSchema, localBusinessSchema, websiteSchema, injectSchema } = useSchemaOrg()

// Inject global schemas
injectSchema([
  organizationSchema(),
  localBusinessSchema(),
  websiteSchema()
])

// Close the live chat widget when clicking outside it
onMounted(() => {
  document.addEventListener('click', (e: MouseEvent) => {
    const target = e.target as HTMLElement
    const widget = document.querySelector(LIVE_CHAT_HOLDER_SELECTOR)
    if (widget && !widget.contains(target)) {
      closeLiveChat(window)
    }
  })
})

const langMap: Record<string, string> = { en: 'en', ru: 'ru', hy: 'hy' }

/** Website token for the default locale, and the fallback for any other. */
const DEFAULT_CHAT_TOKEN = '9J2djCS9C979cK8qH55SKQgJ'

const tokenMap: Record<string, string> = {
  en: DEFAULT_CHAT_TOKEN,
  ru: 'UkwaS1xyNnNRv8SDj4kpNn2t',
  hy: 'aMzynyMYGE9p3oxwwUuMMEVa'
}

/** Language name recorded on the conversation, so an agent knows how to answer. */
const languageOf = (code: string) =>
  code === 'hy' ? 'Armenian' : code === 'ru' ? 'Russian' : 'English'

// Tell a widget that is already running about a locale change. A widget that is
// not running is not an error here — the visitor may simply not have opened it.
watch(locale, (newLocale) => {
  if (!import.meta.client) return

  setLiveChatLocale(window, langMap[newLocale] || 'en', languageOf(newLocale))
})

// Correct token for SSR injection
const currentToken = computed(() => tokenMap[locale.value] ?? DEFAULT_CHAT_TOKEN)

useHead({
  htmlAttrs: {
    lang: () => langMap[locale.value] ?? 'en'
  },
  script: [
    {
      // Blocking script: always force dark on public pages
      innerHTML: `document.documentElement.classList.add('dark');document.documentElement.classList.remove('light');`,
      tagPosition: 'head'
    },
    {
      // Live chat widget loader. The globals it drives are named in
      // utils/liveChat.ts rather than inline, because the provider is an
      // Innochat build whose SDK shares none of Chatwoot's global names, and
      // reaching for the wrong one fails without an error.
      innerHTML: buildLiveChatLoader({
        baseUrl: 'https://chat.innovayse.com',
        websiteToken: currentToken.value,
        locale: langMap[locale.value] || 'en',
        language: languageOf(locale.value)
      }),
      type: 'text/javascript',
      tagPosition: 'bodyClose'
    },
    {
      innerHTML: `(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':
new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],
j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src=
'https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);
})(window,document,'script','dataLayer','GTM-5C9TKM58');`,
      type: 'text/javascript',
      tagPosition: 'head'
    }
  ],
  noscript: [
    {
      innerHTML: '<iframe src="https://www.googletagmanager.com/ns.html?id=GTM-5C9TKM58" height="0" width="0" style="display:none;visibility:hidden"></iframe>',
      tagPosition: 'bodyOpen'
    }
  ]
})
</script>
