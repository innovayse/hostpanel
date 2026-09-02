// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  /**
   * Development only: render in the browser instead of on the server.
   *
   * The dev server's SSR transform runs in a vite-node worker that talks to Nitro over a
   * unix socket, and on this Docker-for-Windows setup that exchange never completes for this
   * app: every server-rendered route either hangs or answers 500 "IPC connection closed",
   * while API routes, static assets and the `ssr: false` pages under /client all work. It is
   * not the application code — the same routes render correctly in production, and the
   * failure reproduces on an unmodified checkout.
   *
   * Turning SSR off in development trades away one thing — you cannot see server-rendered
   * output locally — to get back every page, which otherwise cannot be opened at all.
   * Production is untouched: `$development` applies only to `nuxt dev`.
   *
   * Remove this once the dev-server issue is fixed, and verify by loading any public page.
   */
  $development: {
    ssr: false,
  },

  compatibilityDate: '2026-02-04',
  devtools: { enabled: true },

  // Client area pages are auth-gated and fetch data client-side only — disable SSR
  // to avoid hydration mismatches (server renders empty state, client renders content)
  // X-Robots-Tag: noindex prevents search engines from indexing private pages
  routeRules: {
    // Immutable hashed assets — cache for 1 year
    '/_nuxt/**': { headers: { 'Cache-Control': 'public, max-age=31536000, immutable' } },
    // Public static assets — cache for 30 days
    '/images/**': { headers: { 'Cache-Control': 'public, max-age=2592000, stale-while-revalidate=86400' } },
    '/fonts/**': { headers: { 'Cache-Control': 'public, max-age=31536000, immutable' } },
    // Auth-gated SPA pages — no SSR, no indexing
    '/client/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/cart/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/checkout/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
  },

  nitro: {
    // Compress static assets with gzip and brotli at build time
    compressPublicAssets: { gzip: true, brotli: true },
    // Minify server-side HTML output
    minify: true,
    // Nitro's built-in handler drops an H3Error's `data`, which is where the C# backend's
    // machine-readable error code travels. server/error.ts adds it back for /api/* and
    // delegates everything else untouched. See the file header for why the code has to
    // reach the browser at all.
    errorHandler: '~/server/error'
  },
  modules: ['@nuxt/eslint', '@nuxt/icon', '@nuxt/image', '@nuxtjs/tailwindcss', '@nuxtjs/i18n', '@nuxtjs/sitemap', 'nuxt-swiper', '@pinia/nuxt'],

  image: {
    quality: 80,
    format: ['webp'],
    // No `loading: 'lazy'` here. It sat in this block claiming to set a default for every
    // NuxtImg, but `@nuxt/image`'s ModuleOptions has no such key -- the module ignored it and
    // the comment described behaviour nobody had. Lazy loading is per-component: a `<NuxtImg>`
    // that should defer needs its own `loading="lazy"`, as the eager ones already spell out
    // `loading="eager"`.
    screens: {
      xs: 320,
      sm: 640,
      md: 768,
      lg: 1024,
      xl: 1280,
      '2xl': 1536
    }
  },

  // GA4 tracking now handled by Google Tag Manager (GTM-5C9TKM58)

  swiper: {
    modules: ['autoplay', 'effect-fade', 'navigation', 'pagination']
  } as any,

  css: ['~/assets/css/main.css', '@fontsource-variable/inter', '~/assets/styles/animations.css', '~/assets/styles/global.css'],

  runtimeConfig: {
    // Server-side only. No credential for an external service belongs here — only this
    // BFF's own OIDC client credential, internal addresses, and the auth-mode switch.
    authMode: process.env.AUTH_MODE || 'sso',
    apiUrl: process.env.API_URL || 'http://localhost:5000',
    ssoUrl: process.env.SSO_URL || 'http://sso-api:8080',
    ssoClientId: process.env.SSO_CLIENT_ID || 'hostpanel',
    ssoClientSecret: process.env.SSO_CLIENT_SECRET || 'dev-secret-hostpanel',
    ssoCallbackUrl: process.env.SSO_CALLBACK_URL || 'http://panel.local/api/portal/auth/sso/callback',
    // Internal URL of innovayse-main (app.local) — used for backchannel logout delegation
    mainApiUrl: process.env.NUXT_MAIN_API_URL || 'http://main-client:3000',

    // Public runtime config (exposed to client)
    public: {
      authMode: process.env.AUTH_MODE || 'sso',
      baseUrl: process.env.NUXT_PUBLIC_BASE_URL || 'https://innovayse.com',
      ssoPublicUrl: process.env.SSO_PUBLIC_URL || 'http://sso.local',
      tasksUrl: process.env.NUXT_PUBLIC_TASKS_URL || 'http://tasks.local',
      erpUrl: process.env.NUXT_PUBLIC_ERP_URL || 'http://erp.local',
      sheetsUrl: process.env.NUXT_PUBLIC_SHEETS_URL || 'http://sheets.local',
      emailUrl: process.env.NUXT_PUBLIC_EMAIL_URL || 'http://email.local',
      driveUrl: process.env.NUXT_PUBLIC_DRIVE_URL || 'http://drive.local',
      // Address of the WHMCS install this panel hands visitors off to (card
      // management, invoice payment, domain transfer). An address only — the WHMCS
      // API credentials are the C# backend's and deliberately absent here. Empty
      // hides every link that needs it rather than rendering a broken one.
      //
      // The trailing-slash strip below only runs where nuxt.config is evaluated: a
      // dev server, or the `docker build` that freezes the production defaults. A
      // deployed container overrides this through NUXT_PUBLIC_WHMCS_URL, which Nitro
      // substitutes verbatim, so that value must carry no trailing slash of its own.
      whmcsUrl: (process.env.WHMCS_URL || '').replace(/\/+$/, ''),
      // Stripe.js's publishable key, read by useStripe(). Public by design; the
      // secret key is the backend's. Overridden at runtime with
      // NUXT_PUBLIC_STRIPE_PUBLISHABLE_KEY — empty leaves the card form uninitialised.
      stripePublishableKey: process.env.STRIPE_PUBLISHABLE_KEY || '',
      mainUrl: process.env.NUXT_PUBLIC_MAIN_URL || 'http://app.local',
      // Active portal template. Phase 4 lets an admin override this from settings;
      // until then it is the only switch. Unknown values fall back to 'aurora'.
      portalTemplate: process.env.NUXT_PUBLIC_PORTAL_TEMPLATE || 'aurora',
      // The header logo and browser tab icon, settable from the admin panel.
      // Empty renders the template's built-in mark/wordmark and static favicon.
      portalLogo: process.env.NUXT_PUBLIC_PORTAL_LOGO || '',
      portalFavicon: process.env.NUXT_PUBLIC_PORTAL_FAVICON || '',
      // Header app launcher. Off unless a deployment actually runs the sibling
      // apps it links to; every app URL below has a development default, so
      // presence of a URL cannot decide this on its own.
      portalAppsEnabled: process.env.NUXT_PUBLIC_PORTAL_APPS_ENABLED || '',
      // Contact and widget configuration. Each is hidden when empty — a fresh
      // install with none of these set still renders a complete site.
      portalWhatsapp: process.env.NUXT_PUBLIC_PORTAL_WHATSAPP || '',
      portalTelegram: process.env.NUXT_PUBLIC_PORTAL_TELEGRAM || '',
      portalChatProvider: process.env.NUXT_PUBLIC_PORTAL_CHAT_PROVIDER || '',
      portalNewsletterUrl: process.env.NUXT_PUBLIC_PORTAL_NEWSLETTER_URL || '',
      portalContactEmail: process.env.NUXT_PUBLIC_PORTAL_CONTACT_EMAIL || '',
      portalSocialFacebook: process.env.NUXT_PUBLIC_PORTAL_SOCIAL_FACEBOOK || '',
      portalSocialInstagram: process.env.NUXT_PUBLIC_PORTAL_SOCIAL_INSTAGRAM || '',
      portalSocialLinkedin: process.env.NUXT_PUBLIC_PORTAL_SOCIAL_LINKEDIN || '',
      portalSocialYoutube: process.env.NUXT_PUBLIC_PORTAL_SOCIAL_YOUTUBE || '',
      portalContactPhone: process.env.NUXT_PUBLIC_PORTAL_CONTACT_PHONE || '',
      portalLegalTaxId: process.env.NUXT_PUBLIC_PORTAL_LEGAL_TAX_ID || '',
    }
  },

  i18n: {
    vueI18n: './i18n.config.ts',
    // Locale messages are loaded dynamically by plugins/i18n.ts via setLocaleMessage.
    // No langDir/files needed here — the plugin handles all locale JSON imports.
    locales: [
      { code: 'en', name: 'English', language: 'en-US' },
      { code: 'ru', name: 'Русский', language: 'ru-RU' },
      { code: 'hy', name: 'Հայերեն', language: 'hy-AM' }
    ],
    defaultLocale: 'en',
    strategy: 'prefix_except_default',
    detectBrowserLanguage: false
  },

  // Canonical origin for generated URLs. @nuxtjs/sitemap v7 reads this rather
  // than its own `hostname` option, which earlier versions used and which was
  // still set here — silently ignored, so the sitemap advertised whatever host
  // the request arrived on, down to the container's own address.
  //
  // Deliberately no default: with nothing set, nuxt-site-config keeps falling
  // back to the request host, which is at least the operator's own domain. A
  // literal here would put this deployment's domain in every other operator's
  // sitemap.
  site: {
    url: process.env.NUXT_PUBLIC_BASE_URL,
  },

  // Sitemap configuration
  sitemap: {
    gzip: true,
    exclude: [
      '/admin/**',
      '/_nuxt/**'
    ],
    defaults: {
      changefreq: 'weekly' as const,
      priority: 0.7,
      lastmod: new Date().toISOString()
    },
    i18n: true,
    // @ts-ignore - Type compatibility issue with sitemap module
    urls: async () => {
      const routes = [
        // Main pages
        { loc: '/', changefreq: 'daily' as const, priority: 1.0 },
        { loc: '/products', changefreq: 'weekly' as const, priority: 0.9 },
        // The sitemap module discovers both automatically; these entries only
        // raise them from the default 0.7 to the 0.9 the storefront deserves.
        { loc: '/hosting', changefreq: 'weekly' as const, priority: 0.9 },
        { loc: '/domains', changefreq: 'weekly' as const, priority: 0.9 },
        { loc: '/contact', changefreq: 'monthly' as const, priority: 0.7 },
        { loc: '/faq', changefreq: 'monthly' as const, priority: 0.6 },
        { loc: '/terms', changefreq: 'yearly' as const, priority: 0.3 },
        { loc: '/privacy', changefreq: 'yearly' as const, priority: 0.3 },
        { loc: '/cookie-policy', changefreq: 'yearly' as const, priority: 0.3 },
        { loc: '/refund-policy', changefreq: 'yearly' as const, priority: 0.3 },
        { loc: '/acceptable-use', changefreq: 'yearly' as const, priority: 0.3 }
      ]

      // Individual product pages (SaaS families)
      const productSlugs = ['smartlearn-system', 'propsystem-pro', 'shopkit-pro', 'metricskit-pro', 'quickbite', 'elpida-ai', 'taskero']
      productSlugs.forEach(slug => {
        routes.push({ loc: `/products/${slug}`, changefreq: 'weekly' as const, priority: 0.8 })
      })

      return routes
    }
  },

  build: {
    transpile: ['lucide-vue-next']
  },

  vite: {
    server: {
      allowedHosts: ['panel.local', 'panel-admin.local']
    },
    ssr: {
      // Force lucide-vue-next to be bundled into the SSR output (not treated as external)
      // This prevents named export resolution issues (e.g. "Server is not defined")
      noExternal: ['lucide-vue-next']
    },
    build: {
      // esbuild is the default Nuxt 3 minifier — fastest and produces smallest bundles
      minify: 'esbuild'
    },
    esbuild: {
      // Drop console.log/debug statements in production builds
      drop: process.env.NODE_ENV === 'production' ? ['console', 'debugger'] : []
    }
  },

  app: {
    // Set NUXT_APP_CDN_URL env var in production to serve assets from CDN
    cdnURL: process.env.NUXT_APP_CDN_URL || '',
    head: {
      htmlAttrs: {
        lang: 'en'
      },
      title: 'Innovayse - Full-Cycle Digital Agency | Web Development, SEO, PPC',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'description', content: 'Innovayse is a full-cycle digital agency specializing in web & mobile development, technical SEO, PPC advertising, and SaaS products. Transform your business with our expert solutions.' },
        { name: 'keywords', content: 'web development, mobile development, SEO, PPC, Google Ads, Yandex Direct, SaaS, digital agency, e-commerce development, technical SEO, content optimization' },
        { name: 'author', content: 'Innovayse' },
        { name: 'robots', content: 'index, follow' },

        // Open Graph / Facebook
        { property: 'og:type', content: 'website' },
        { property: 'og:title', content: 'Innovayse - Full-Cycle Digital Agency' },
        { property: 'og:description', content: 'Expert web development, SEO, PPC, and SaaS solutions for your business growth' },
        { property: 'og:image', content: '/og-image.jpg' },
        { property: 'og:locale', content: 'en_US' },
        { property: 'og:locale:alternate', content: 'ru_RU' },
        { property: 'og:locale:alternate', content: 'hy_AM' },
        { property: 'og:site_name', content: 'Innovayse' },

        // Twitter Card
        { name: 'twitter:card', content: 'summary_large_image' },
        { name: 'twitter:title', content: 'Innovayse - Full-Cycle Digital Agency' },
        { name: 'twitter:description', content: 'Expert web development, SEO, PPC, and SaaS solutions for your business growth' },
        { name: 'twitter:image', content: '/og-image.jpg' },

        // Additional SEO
        { name: 'format-detection', content: 'telephone=no' },
        { name: 'theme-color', content: '#0ea5e9' }
      ],
      script: [
        ...(process.env.NUXT_PUBLIC_MAIN_URL
          ? [{ src: `${process.env.NUXT_PUBLIC_MAIN_URL}/widget/header.js`, async: true }]
          : []),
      ],
      link: [
        // Performance: preconnect to third-party origins used on all pages
        { rel: 'preconnect', href: 'https://www.googletagmanager.com' },
        { rel: 'dns-prefetch', href: 'https://www.googletagmanager.com' },
        { rel: 'dns-prefetch', href: 'https://www.google-analytics.com' },
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/site.webmanifest' }
      ]
    }
  }
})