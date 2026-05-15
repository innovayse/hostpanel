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

// Close Innochat widget when clicking outside it
onMounted(() => {
  document.addEventListener('click', (e: MouseEvent) => {
    const target = e.target as HTMLElement
    const widget = document.querySelector('.woot-widget-holder')
    if (widget && !widget.contains(target)) {
      const w = window as any
      try { w.$chatwoot?.toggle('close') } catch {}
    }
  })
})

const langMap: Record<string, string> = { en: 'en', ru: 'ru', hy: 'hy' }
const tokenMap: Record<string, string> = {
  en: '9J2djCS9C979cK8qH55SKQgJ',
  ru: 'UkwaS1xyNnNRv8SDj4kpNn2t',
  hy: 'aMzynyMYGE9p3oxwwUuMMEVa'
}

// Reactively update Innochat when locale changes
watch(locale, (newLocale) => {
  if (!process.client) return
  
  const apiLocale = langMap[newLocale] || 'en'
  const w = window as any

  if (w.$chatwoot) {
    w.$chatwoot.setLocale(apiLocale)
    w.$chatwoot.setCustomAttributes({
      language: newLocale === 'hy' ? 'Armenian' : newLocale === 'ru' ? 'Russian' : 'English'
    })
  }
})

// Correct token for SSR injection
const currentToken = computed(() => tokenMap[locale.value] ?? tokenMap.en)

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
      // Chatwoot live chat widget loader - SSR Injected token
      innerHTML: `window.chatwootSettings = {"position":"right","type":"standard","launcherTitle":"","hideMessageBubble":true};
      (function(d,t){
        var BASE_URL="https://chat.innovayse.com";
        var g=d.createElement(t),s=d.getElementsByTagName(t)[0];
        g.src=BASE_URL+"/packs/js/sdk.js";
        g.async=true;
        s.parentNode.insertBefore(g,s);
        g.onload=function(){
          window.chatwootSDK.run({
            websiteToken: '${currentToken.value}',
            baseUrl: BASE_URL
          });
          window.addEventListener('chatwoot:ready', function() {
            window.$chatwoot.setLocale('${langMap[locale.value] || 'en'}');
            window.$chatwoot.setCustomAttributes({
              language: '${locale.value === 'hy' ? 'Armenian' : locale.value === 'ru' ? 'Russian' : 'English'}'
            });
          });
        }
      })(document,"script");`,
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
