/**
 * Nuxt configuration: rendering rules per route, runtime config, modules and the head defaults.
 *
 * @see https://nuxt.com/docs/api/configuration/nuxt-config
 * @module nuxt.config
 */

export default defineNuxtConfig({
  /**
   * Development only: render in the browser instead of on the server.
   *
   * The dev server's SSR transform runs in a vite-node worker that talks to Nitro over a
   * unix socket, and on this Docker-for-Windows setup that exchange never completes for this
   * app: every server-rendered route either hangs or answers 500 "IPC connection closed",
   * while API routes, static assets and the `ssr: false` pages under /client all work. It is
   * not the application code: the same routes render correctly in production, and the
   * failure reproduces on an unmodified checkout.
   *
   * Turning SSR off in development trades away one thing, you cannot see server-rendered
   * output locally, to get back every page, which otherwise cannot be opened at all.
   * Production is untouched: `$development` applies only to `nuxt dev`.
   *
   * Remove this once the dev-server issue is fixed, and verify by loading any public page.
   */
  $development: {
    ssr: false,
  },

  compatibilityDate: '2026-02-04',
  devtools: { enabled: true },

  // Client area pages are auth-gated and fetch data client-side only: disable SSR
  // to avoid hydration mismatches (server renders empty state, client renders content)
  // X-Robots-Tag: noindex prevents search engines from indexing private pages
  routeRules: {
    // Immutable hashed assets: cache for 1 year
    '/_nuxt/**': { headers: { 'Cache-Control': 'public, max-age=31536000, immutable' } },
    // Public static assets: cache for 30 days
    '/images/**': { headers: { 'Cache-Control': 'public, max-age=2592000, stale-while-revalidate=86400' } },
    '/fonts/**': { headers: { 'Cache-Control': 'public, max-age=31536000, immutable' } },
    // Auth-gated SPA pages: no SSR, no indexing
    '/client/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    // The same pages under a language prefix: a rule matches the path, and server-rendering them answers 500 on a block
    // that reads the browser, a difference nobody sees in the default language.
    '/ru/client/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/hy/client/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/cart/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/ru/cart/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/hy/cart/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/checkout/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/ru/checkout/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/hy/checkout/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    // The hosted-gateway returnUrl landing page. It belongs to this group for the same two
    // reasons: it has nothing to server-render (the payment outcome is only known after the
    // on-mount verification call), and a page whose URL carries an invoice/order id must
    // never be indexed. Omitting it is not cosmetic: server-rendering it answers 500 in
    // production, breaking every payer's return from the bank. Development runs with
    // `ssr: false` globally (see the note at the top of this file), so this can only ever
    // be caught against a production build.
    '/payment/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/ru/payment/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/hy/payment/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    // The unsubscribe landing page opened from a campaign mail: its address carries a personal token and the
    // request is made on mount, so there is nothing to server-render and nothing to index.
    '/unsubscribe/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/ru/unsubscribe/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    '/hy/unsubscribe/**': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } },
    // The live-chat SDK (plugins/live-chat.ts) draws its launcher bubble in *this* page and
    // points the inbox icon at a root-relative `/brand-assets/` path, which would 404 into the
    // Vue Router. When the operator names a chat server (NUXT_PUBLIC_PORTAL_CHAT_BASE_URL) the
    // path is proxied to it; with none configured there is no chat widget and no rule, so no
    // host is ever assumed. Route rules are fixed at build time, hence the environment value.
    ...(process.env.NUXT_PUBLIC_PORTAL_CHAT_BASE_URL
      ? { '/brand-assets/**': { proxy: `${process.env.NUXT_PUBLIC_PORTAL_CHAT_BASE_URL.replace(/\/+$/, '')}/brand-assets/**` } }
      : {}),
  },

  nitro: {
    // Compress static assets with gzip and brotli at build time
    compressPublicAssets: { gzip: true, brotli: true },
    // Minify server-side HTML output
    minify: true,
    // The error handlers are assembled in the `nitro:config` hook below: setting
    // `errorHandler` here would make Nuxt skip its own handler, the one that renders error.vue.
  },

  hooks: {
    // Nitro's built-in handler drops an H3Error's `data`, which is where the C# backend's
    // machine-readable error code travels. server/error.ts adds it back for /api/* and
    // answers nothing else. See the file header for why the code has to reach the browser at all.
    //
    // It goes in FRONT of the handler Nuxt registers (the one that renders error.vue) rather
    // than replacing it: Nitro runs the chain in order and stops at the first handler that
    // responds, so a page error falls through to Nuxt's handler and the branded error page is
    // rendered. Replacing it left every 404 on Nitro's bare fallback template, which also pulls
    // its stylesheet from a public CDN.
    'nitro:config': (nitroConfig) => {
      nitroConfig.errorHandler = ['~/server/error', ...[nitroConfig.errorHandler ?? []].flat()]
    }
  },
  modules: [
    /**
     * Inline module: drops the `vue-router/volar/sfc-route-blocks` Volar plugin that Nuxt adds to the
     * generated tsconfig. vue-router 4.6 does not export that path, so vue-tsc would print
     * `Resolve plugin path failed` with `ERR_PACKAGE_PATH_NOT_EXPORTED` on every typecheck. The plugin only
     * type-checks `<route>` blocks, which this app does not use.
     */
    (_options, nuxt) => {
      // Registered once every module is installed, so this handler runs after the one that adds the plugin.
      nuxt.hook('modules:done', () => {
        nuxt.hook('prepare:types', ({ tsConfig }) => {
          const options = tsConfig.vueCompilerOptions
          if (options?.plugins) {
            options.plugins = options.plugins.filter((plugin: unknown) => plugin !== 'vue-router/volar/sfc-route-blocks')
          }
        })
      })
    },
    '@nuxt/eslint', '@nuxt/icon', '@nuxt/image', '@nuxtjs/tailwindcss', '@nuxtjs/i18n', '@nuxtjs/sitemap', 'nuxt-swiper', '@pinia/nuxt'],

  /**
   * Icon delivery. Two settings, one cause: the header's cart and menu icons rendered as empty
   * squares on a deployed install.
   *
   * - `localApiEndpoint`: the module's own icon route defaults to `/api/_nuxt_icon`, but the edge proxy
   *   sends every `/api/` request to the C# backend, which answers 404 for it. The route lives outside
   *   `/api` so the Nuxt server answers it.
   * - `clientBundle.scan`: every icon named in the source (literals in templates and data files) is
   *   shipped inside the client bundle, so the browser makes no icon request at all and nothing can
   *   404 at runtime. The endpoint above stays as the fallback for a name built at runtime.
   */
  icon: {
    localApiEndpoint: '/_nuxt_icon',
    clientBundle: { scan: true },
  },

  image: {
    quality: 80,
    format: ['webp'],
    // No `loading: 'lazy'` here. It sat in this block claiming to set a default for every
    // NuxtImg, but `@nuxt/image`'s ModuleOptions has no such key: the module ignored it and
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

  css: ['~/assets/css/main.css', '@fontsource-variable/inter', '~/assets/styles/animations.css', '~/assets/styles/global.css'],

  runtimeConfig: {
    // Server-side only. No credential for an external service belongs here: only this
    // BFF's own OIDC client credential, internal addresses, and the auth-mode switch.
    authMode: process.env.AUTH_MODE || 'sso',
    apiUrl: process.env.API_URL || 'http://localhost:5000',
    ssoUrl: process.env.SSO_URL || 'http://sso-api:8080',
    ssoClientId: process.env.SSO_CLIENT_ID || 'hostpanel',
    // Optional pin of the issuer identifier the sign-on provider declares in its openid-configuration. The
    // declared issuer is what the `iss` of a callback and of an ID token are compared to, exactly. Empty
    // requires the declared issuer to be the same URL as SSO_PUBLIC_URL (a trailing slash on either does not
    // count); set, it must equal the declared issuer exactly or sign-in is refused.
    ssoIssuer: process.env.SSO_ISSUER || '',
    // Filled at runtime from NUXT_SSO_CLIENT_SECRET, which compose feeds from CLIENT_HOSTPANEL_SECRET:
    // the same credential the API reads, so it has one variable name.
    ssoClientSecret: '',
    // Empty (no development host baked in) unless configured: SSO sign-in cannot work without it, and a
    // silent default would ship to production. Set SSO_CALLBACK_URL to the portal's callback address.
    ssoCallbackUrl: process.env.SSO_CALLBACK_URL || '',
    // Redis that keeps the signed-in sessions (the tokens never reach the browser). The same value
    // as the API's REDIS_CONNECTION, overridden at runtime by NUXT_SESSION_REDIS. Empty keeps the
    // sessions in this process's memory, which only suits a single process (tests, local dev).
    sessionRedis: process.env.SESSION_REDIS || '',
    // Key the sessions are encrypted with before they reach Redis (32 random bytes, base64). Filled at runtime from
    // NUXT_SESSION_ENCRYPTION_KEY and required whenever sessionRedis is set outside development: the server
    // refuses to start without it (server/utils/session-crypto.ts).
    sessionEncryptionKey: '',
    // How many trusted proxies stand in front of this server, filled at runtime from
    // NUXT_TRUSTED_PROXY_COUNT. The visitor's address sent to the API is the Nth entry from the right of
    // X-Forwarded-For (the same reading as the API's RateLimit__TrustedProxyCount); 0 uses the socket
    // address and believes no header at all (server/utils/clientAddress.ts).
    trustedProxyCount: 0,

    // Public runtime config (exposed to client)
    public: {
      authMode: process.env.AUTH_MODE || 'sso',
      baseUrl: process.env.NUXT_PUBLIC_BASE_URL || '',
      // The sibling-product addresses below default to empty: a development host baked in here would
      // silently survive into production. Empty hides the launcher entry / skips the widget, and the
      // SSO public address must be set for an SSO install.
      ssoPublicUrl: process.env.SSO_PUBLIC_URL || '',
      tasksUrl: process.env.NUXT_PUBLIC_TASKS_URL || '',
      erpUrl: process.env.NUXT_PUBLIC_ERP_URL || '',
      sheetsUrl: process.env.NUXT_PUBLIC_SHEETS_URL || '',
      emailUrl: process.env.NUXT_PUBLIC_EMAIL_URL || '',
      driveUrl: process.env.NUXT_PUBLIC_DRIVE_URL || '',
      // Address of the billing-portal install this panel hands visitors off to (announcements,
      // product orders, domain transfer). Cards and invoices are handled in this panel. An address only: the billing portal's
      // API credentials are the C# backend's and deliberately absent here. Empty
      // hides every link that needs it rather than rendering a broken one.
      //
      // The trailing-slash strip below only runs where nuxt.config is evaluated: a
      // dev server, or the `docker build` that freezes the production defaults. A
      // deployed container overrides this through NUXT_PUBLIC_BILLING_PORTAL_URL, which Nitro
      // substitutes verbatim, so that value must carry no trailing slash of its own.
      billingPortalUrl: (process.env.BILLING_PORTAL_URL || '').replace(/\/+$/, ''),
      mainUrl: process.env.NUXT_PUBLIC_MAIN_URL || '',
      docsUrl: (process.env.NUXT_PUBLIC_DOCS_URL || '').replace(/\/+$/, ''),
      // Active portal template when the operator's portal.template setting is empty. Empty here
      // (and anything unknown) resolves to DEFAULT_TEMPLATE in templates/types.ts, the one default.
      portalTemplate: process.env.NUXT_PUBLIC_PORTAL_TEMPLATE || '',
      // The header logos and browser tab icon, settable from the admin panel.
      // Empty renders the template's built-in mark/wordmark and static favicon;
      // useBrandLogo() picks the dark variant in dark mode and falls back to the
      // light one, and the mark where a wordmark does not fit.
      portalLogo: process.env.NUXT_PUBLIC_PORTAL_LOGO || '',
      portalLogoDark: process.env.NUXT_PUBLIC_PORTAL_LOGO_DARK || '',
      portalLogoMark: process.env.NUXT_PUBLIC_PORTAL_LOGO_MARK || '',
      portalFavicon: process.env.NUXT_PUBLIC_PORTAL_FAVICON || '',
      // Colour mode for a visitor with no saved choice (light | dark | system) and
      // whether the sun/moon switch is shown. 'dark' is what the site did before the
      // setting existed. Resolution lives in utils/colorMode.ts.
      portalThemeDefault: process.env.NUXT_PUBLIC_PORTAL_THEME_DEFAULT || 'dark',
      portalThemeUserToggle: process.env.NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE || '',
      // What the site calls itself: og:site_name, logo alt text, structured data.
      // Empty falls back to the built-in name in useSiteIdentity().
      portalSiteName: process.env.NUXT_PUBLIC_PORTAL_SITE_NAME || '',
      portalSiteTagline: process.env.NUXT_PUBLIC_PORTAL_SITE_TAGLINE || '',
      // Brand colours (#rrggbb) and typefaces; empty keeps every template's own. Read by
      // plugins/brand-tokens.ts, which derives the whole scale from the one colour.
      portalBrandPrimary: process.env.NUXT_PUBLIC_PORTAL_BRAND_PRIMARY || '',
      portalBrandAccent: process.env.NUXT_PUBLIC_PORTAL_BRAND_ACCENT || '',
      portalBrandFontHeading: process.env.NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING || '',
      portalBrandFontBody: process.env.NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY || '',
      // Tracking and live chat. Nothing loads until these are set, and the tag only after
      // the visitor accepts all cookies (plugins/tracking.client.ts).
      portalGtmId: process.env.NUXT_PUBLIC_PORTAL_GTM_ID || '',
      portalChatBaseUrl: process.env.NUXT_PUBLIC_PORTAL_CHAT_BASE_URL || '',
      portalChatWebsiteToken: process.env.NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN || '',
      portalChatWebsiteTokenRu: process.env.NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_RU || '',
      portalChatWebsiteTokenHy: process.env.NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_HY || '',
      // Header app launcher. Off unless a deployment actually runs the sibling
      // apps it links to; every app URL below has a development default, so
      // presence of a URL cannot decide this on its own.
      portalAppsEnabled: process.env.NUXT_PUBLIC_PORTAL_APPS_ENABLED || '',
      // Contact and widget configuration. Each is hidden when empty: a fresh
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
      portalSocialTwitter: process.env.NUXT_PUBLIC_PORTAL_SOCIAL_TWITTER || '',
      // Address of the hosting product demo, linked from the home products section; empty hides the link.
      portalHostingDemoUrl: process.env.NUXT_PUBLIC_PORTAL_HOSTING_DEMO_URL || '',
      portalContactPhone: process.env.NUXT_PUBLIC_PORTAL_CONTACT_PHONE || '',
      // Support working hours and their time zone, shown on the contact page; each is hidden when empty.
      portalContactHours: process.env.NUXT_PUBLIC_PORTAL_CONTACT_HOURS || '',
      portalContactTimezone: process.env.NUXT_PUBLIC_PORTAL_CONTACT_TIMEZONE || '',
      // Office address and an https map embed address for the contact page and footer; each is hidden when empty.
      portalContactAddress: process.env.NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS || '',
      // Per-language address and hours; empty uses the default ones above.
      portalContactAddressRu: process.env.NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_RU || '',
      portalContactAddressHy: process.env.NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_HY || '',
      portalContactHoursRu: process.env.NUXT_PUBLIC_PORTAL_CONTACT_HOURS_RU || '',
      portalContactHoursHy: process.env.NUXT_PUBLIC_PORTAL_CONTACT_HOURS_HY || '',
      portalContactMapUrl: process.env.NUXT_PUBLIC_PORTAL_CONTACT_MAP_URL || '',
      // Home hero figures and badge; each is hidden when empty.
      portalStatsClients: process.env.NUXT_PUBLIC_PORTAL_STATS_CLIENTS || '',
      portalStatsUptime: process.env.NUXT_PUBLIC_PORTAL_STATS_UPTIME || '',
      portalStatsRegions: process.env.NUXT_PUBLIC_PORTAL_STATS_REGIONS || '',
      portalHeroBadge: process.env.NUXT_PUBLIC_PORTAL_HERO_BADGE || '',
      portalLegalTaxId: process.env.NUXT_PUBLIC_PORTAL_LEGAL_TAX_ID || '',
      // Company story on the About page, per language with the first as the fallback; empty sends visitors to contact.
      portalAboutText: process.env.NUXT_PUBLIC_PORTAL_ABOUT_TEXT || '',
      portalAboutTextRu: process.env.NUXT_PUBLIC_PORTAL_ABOUT_TEXT_RU || '',
      portalAboutTextHy: process.env.NUXT_PUBLIC_PORTAL_ABOUT_TEXT_HY || '',
    }
  },

  i18n: {
    // The module resolves `vueI18n` inside its restructure directory (`i18n/` by default); this app keeps
    // the file at the project root, so the directory is the root itself.
    restructureDir: '.',
    vueI18n: './i18n.config.ts',
    // Locale messages are loaded dynamically by plugins/i18n.ts via setLocaleMessage.
    // No langDir/files needed here: the plugin handles all locale JSON imports.
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
  // than its own `hostname` option, which it silently ignores.
  //
  // Deliberately no default: with nothing set, nuxt-site-config keeps falling
  // back to the request host, which is at least the operator's own domain. A
  // literal here would put this deployment's domain in every other operator's
  // sitemap.
  //
  // The name is set explicitly: with none, nuxt-site-config falls back to the
  // package name and ships it in the SSR payload. Same neutral fallback as
  // NEUTRAL_SITE_NAME in useSiteIdentity().
  site: {
    url: process.env.NUXT_PUBLIC_BASE_URL,
    name: process.env.NUXT_PUBLIC_PORTAL_SITE_NAME || 'Hostpanel',
  },

  // Sitemap configuration
  sitemap: {
    exclude: [
      '/admin/**',
      '/_nuxt/**'
    ],
    defaults: {
      changefreq: 'weekly' as const,
      priority: 0.7,
      lastmod: new Date().toISOString()
    },
    // The product family pages come from the catalogue at request time, never from a fixed list.
    sources: ['/api/sitemap/products'],
    urls: async () => {
      // The module accepts only these priorities and change frequencies, so the list is typed
      // rather than left to widen to `number`.
      const routes: Array<{
        loc: string
        changefreq: 'daily' | 'weekly' | 'monthly' | 'yearly'
        priority: 0 | 0.1 | 0.2 | 0.3 | 0.4 | 0.5 | 0.6 | 0.7 | 0.8 | 0.9 | 1
      }> = [
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

      return routes
    }
  },

  build: {
    transpile: ['lucide-vue-next']
  },

  vite: {
    server: {
      // Host names the dev server answers to besides localhost, comma separated, from the environment
      // (NUXT_DEV_ALLOWED_HOSTS): a deployment's own names are not written into the repository.
      allowedHosts: (process.env.NUXT_DEV_ALLOWED_HOSTS ?? '').split(',').map(host => host.trim()).filter(Boolean)
    },
    ssr: {
      // Force lucide-vue-next to be bundled into the SSR output (not treated as external)
      // This prevents named export resolution issues (e.g. "Server is not defined")
      noExternal: ['lucide-vue-next']
    },
    build: {
      rolldownOptions: {
        // Production builds drop console.* calls and debugger statements. Vite 8 minifies with the Oxc
        // minifier and ignores `esbuild.drop` with a warning, so the same rule is stated for the
        // minifier itself.
        output: process.env.NODE_ENV === 'production'
          ? { minify: { compress: { dropConsole: true, dropDebugger: true } } }
          : {},
        checks: {
          // The "plugin timings" notice reports that Nuxt's own transform plugins (keyed-functions,
          // imports-transform, the Vue compiler) take most of the build time. They are Nuxt internals this
          // project cannot make faster, so the notice carries no action and is turned off.
          bundlerTimings: false
        }
      }
    }
  },

  postcss: {
    plugins: {
      // The Tailwind module adds this nesting plugin for Tailwind 3, but Nuxt cannot load it as an ES
      // module (the package exposes it as CommonJS only) and printed a warning on every build while
      // skipping it. Nothing in the stylesheets uses nested rules, so it is switched off explicitly.
      'tailwindcss/nesting': false
    }
  },

  app: {
    // Set NUXT_APP_CDN_URL env var in production to serve assets from CDN
    cdnURL: process.env.NUXT_APP_CDN_URL || '',
    head: {
      htmlAttrs: {
        lang: 'en'
      },
      // Brand-free defaults: app.vue adds the operator's site name (og:site_name, author) at request time.
      title: 'Web Hosting, Domains and Servers | Client Portal',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'description', content: 'Web hosting, domains, VPS and dedicated servers with online billing and a self-service client portal to manage services, invoices and support.' },
        { name: 'keywords', content: 'web hosting, domain registration, VPS, dedicated servers, SSL certificates, client portal, online billing' },
        { name: 'robots', content: 'index, follow' },

        // Open Graph
        { property: 'og:type', content: 'website' },
        { property: 'og:title', content: 'Web Hosting, Domains and Servers' },
        { property: 'og:description', content: 'Hosting, domains and servers with online billing and a client portal' },
        // Fallback only: useSeo() and the product page replace it with an absolute URL built
        // from the configured site URL, which is not known when this file is built.
        { property: 'og:image', content: '/og-image.jpg' },
        { property: 'og:image:type', content: 'image/jpeg' },
        { property: 'og:image:width', content: '1200' },
        { property: 'og:image:height', content: '630' },
        { property: 'og:locale', content: 'en_US' },
        { property: 'og:locale:alternate', content: 'ru_RU' },
        { property: 'og:locale:alternate', content: 'hy_AM' },
        // og:site_name and author come from app.vue, where the operator's site name is known.

        // Twitter Card
        { name: 'twitter:card', content: 'summary_large_image' },
        { name: 'twitter:title', content: 'Web Hosting, Domains and Servers' },
        { name: 'twitter:description', content: 'Hosting, domains and servers with online billing and a client portal' },
        { name: 'twitter:image', content: '/og-image.jpg' },

        // Additional SEO
        { name: 'format-detection', content: 'telephone=no' },
        // theme-color is NOT here: plugins/color-mode.ts writes it per template and mode.

        // Windows start-screen tile; browserconfig.xml carries the same tile for older Edge.
        { name: 'msapplication-TileImage', content: '/mstile-150x150.png' },
        { name: 'msapplication-TileColor', content: '#0B0B14' },
        { name: 'msapplication-config', content: '/browserconfig.xml' }
      ],
      // The portal header widget's <script> is NOT here: process.env is read at build time,
      // so it would bake the builder's URL into the image. plugins/header-widget.ts adds it
      // from runtimeConfig instead.
      link: [
        // No preconnect to a tag or analytics host here: whether either is contacted at
        // all is the operator's setting plus the visitor's consent (plugins/tracking.client.ts).
        // The favicon links are NOT here. app.vue emits them through brandingIcons(),
        // built-in defaults included, because unhead only dedupes link tags that share an
        // explicit key, so a static link here and an uploaded one there both render, and
        // Chrome picks the SVG. An operator who uploaded a favicon would keep seeing the
        // built-in one. One owner instead of two.
        { rel: 'manifest', href: '/site.webmanifest' }
      ]
    }
  }
})
