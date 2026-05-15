// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
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
    minify: true
  },
  modules: ['@nuxt/eslint', '@nuxt/icon', '@nuxt/image', '@nuxtjs/tailwindcss', '@nuxtjs/i18n', '@nuxtjs/sitemap', 'nuxt-swiper', '@pinia/nuxt'],

  image: {
    quality: 80,
    format: ['webp'],
    // Default lazy loading for all NuxtImg — components that need eager override individually
    loading: 'lazy',
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
    // Server-side only (not exposed to client)
    apiUrl: process.env.API_URL || 'http://localhost:5000',
    telegramBotToken: process.env.TELEGRAM_BOT_TOKEN,
    telegramChatId: process.env.TELEGRAM_CHAT_ID,
    // Email SMTP configuration
    smtpHost: process.env.SMTP_HOST,
    smtpPort: process.env.SMTP_PORT,
    smtpUser: process.env.SMTP_USER,
    smtpPassword: process.env.SMTP_PASSWORD,
    emailTo: process.env.EMAIL_TO,
    // WHMCS API configuration — trailing slash stripped so URL joins work correctly
    whmcsUrl: (process.env.WHMCS_URL || '').replace(/\/+$/, ''),
    whmcsIdentifier: process.env.WHMCS_IDENTIFIER,
    whmcsSecret: process.env.WHMCS_SECRET,
    whmcsAccessKey: process.env.WHMCS_ACCESS_KEY,
    // WHM root API token for cPanel SSO (create_user_session)
    whmApiToken: process.env.WHM_API_TOKEN,

    // Public runtime config (exposed to client)
    public: {
      baseUrl: process.env.NUXT_PUBLIC_BASE_URL || 'https://innovayse.com',
      whmcsUrl: (process.env.WHMCS_URL || '').replace(/\/+$/, ''),
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
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'i18n_locale',
      redirectOn: 'root',
      fallbackLocale: 'en'
    }
  },

  // Sitemap configuration
  sitemap: {
    hostname: 'https://innovayse.com',
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
        { loc: '/about', changefreq: 'monthly' as const, priority: 0.8 },
        { loc: '/services', changefreq: 'weekly' as const, priority: 0.9 },
        { loc: '/products', changefreq: 'weekly' as const, priority: 0.9 },
        { loc: '/portfolio', changefreq: 'weekly' as const, priority: 0.8 },
        { loc: '/blog', changefreq: 'daily' as const, priority: 0.8 },
        { loc: '/contact', changefreq: 'monthly' as const, priority: 0.7 },
        { loc: '/faq', changefreq: 'monthly' as const, priority: 0.6 },
        { loc: '/resources', changefreq: 'monthly' as const, priority: 0.7 },
        { loc: '/write-for-us', changefreq: 'monthly' as const, priority: 0.6 },
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

      // Service branches - Web Development
      const webDevBranches = ['corporate-websites', 'e-commerce', 'web-applications', 'landing-pages', 'custom-cms']
      webDevBranches.forEach(branch => {
        routes.push({
          loc: `/services/web-development/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      // Service branches - Mobile Development
      const mobileBranches = ['native-ios', 'native-android', 'cross-platform', 'pwa']
      mobileBranches.forEach(branch => {
        routes.push({
          loc: `/services/mobile-development/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      // Service branches - Technical SEO
      const techSeoBranches = ['technical-audit', 'core-web-vitals', 'schema-markup', 'crawl-optimization']
      techSeoBranches.forEach(branch => {
        routes.push({
          loc: `/services/technical-seo/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      // Service branches - Content SEO
      const contentSeoBranches = ['keyword-research', 'content-strategy', 'on-page-seo', 'link-building', 'local-seo', 'e-commerce-seo', 'international-seo']
      contentSeoBranches.forEach(branch => {
        routes.push({
          loc: `/services/content-seo/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      // Service branches - Google Ads
      const googleAdsBranches = ['search', 'display', 'social', 'shopping', 'video-youtube', 'remarketing', 'app-campaigns']
      googleAdsBranches.forEach(branch => {
        routes.push({
          loc: `/services/google-ads/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      // Service branches - Yandex Direct
      const yandexBranches = ['search-ads', 'yan-display', 'retargeting', 'smart-banners']
      yandexBranches.forEach(branch => {
        routes.push({
          loc: `/services/yandex-direct/${branch}`,
          changefreq: 'monthly' as const,
          priority: 0.7
        })
      })

      return routes
    }
  },

  build: {
    transpile: ['lucide-vue-next']
  },

  vite: {
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
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
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
      link: [
        // Performance: preconnect to third-party origins used on all pages
        { rel: 'preconnect', href: 'https://www.googletagmanager.com' },
        { rel: 'dns-prefetch', href: 'https://www.googletagmanager.com' },
        { rel: 'dns-prefetch', href: 'https://www.google-analytics.com' },
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32x32.png' },
        { rel: 'icon', type: 'image/png', sizes: '16x16', href: '/favicon-16x16.png' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/site.webmanifest' }
      ]
    }
  }
})