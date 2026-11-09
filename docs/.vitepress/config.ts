/**
 * VitePress site configuration for the Hostpanel documentation website.
 * The site is served under a configurable URL path. The path /docs/ of the
 * public site (https://hostpanel.innovayse.com/docs) is only the default; set
 * `DOCS_BASE` and `DOCS_SITE_URL` (see below) to publish it anywhere else, for
 * example at the root of a custom domain.
 */
import { defineConfig } from 'vitepress'
import { existsSync, readFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createSeo } from './seo'
import { withNavBarClasses } from './navClasses'
import { hasSitemap, readLatestPages, resolveVersion } from './versions'
import { listEnglishPages, pageKey, readSettings, translatedKeys } from '../scripts/lib/localeFallbacks.mjs'
import { DEFAULT_LOCALE, FALLBACK_DIR, LOCALES, LocaleKey, TRANSLATED_LOCALES } from './i18n/locales'
import { buildLocaleThemeConfig, buildSearchTranslations } from './i18n/themeConfig'
import type { GeneratedGroup } from './i18n/themeConfig'
import { stringsFor } from './i18n/ui'
import type { PageLanguages } from './seo'

const __dirname = dirname(fileURLToPath(import.meta.url))

/**
 * The endpoint groups of the API reference. `scripts/generate-api-reference.mjs` writes this file
 * from the OpenAPI document in CI; it is not committed, so a plain checkout has no endpoint pages.
 */
const generatedSidebarPath = resolve(__dirname, '../en/api-reference/generated-sidebar.json')
const generatedApiSidebarItems: GeneratedGroup[] = existsSync(generatedSidebarPath)
  ? JSON.parse(readFileSync(generatedSidebarPath, 'utf8'))
  : []

/**
 * Public address of the demo admin panel and client portal.
 * Set both to show the "Try the demo" button in the top bar.
 * Leave empty to hide it.
 */
const DEMO_ADMIN_URL = process.env.DEMO_ADMIN_URL ?? ''
const DEMO_CLIENT_URL = process.env.DEMO_CLIENT_URL ?? ''

/**
 * URL path the site is served under, always with a leading and a trailing slash.
 * Set `DOCS_BASE` when the site is published somewhere other than the default
 * path /docs/ (for example `/` on a custom domain). The value is
 * trimmed and normalized, so `docs`, ` /docs ` and `/docs/` all give `/docs/`;
 * an empty or blank value falls back to the default.
 */
const base: string = `/${(process.env.DOCS_BASE?.trim() || '/docs/').replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/')

/**
 * Public origin plus base path of the published site, without a trailing slash.
 * Used for absolute URLs such as the social preview image. Set `DOCS_SITE_URL`
 * to match `DOCS_BASE` when publishing elsewhere.
 */
const siteUrl: string = (process.env.DOCS_SITE_URL?.trim() || 'https://hostpanel.innovayse.com/docs').replace(/\/+$/, '')

/**
 * Which version of the documentation this build is: `DOCS_VERSION` is empty (unversioned, or the latest
 * release), `next`, or `vMAJOR.MINOR` for an older release. `DOCS_LATEST_VERSION`, `DOCS_LATEST_URL`,
 * `DOCS_VERSIONS_URL` and `DOCS_LATEST_PAGES` describe the latest release, which an older build points
 * its canonical links and its banner at; see `versions.ts`. All are unset for a plain build.
 */
const version = resolveVersion(
  {
    version: process.env.DOCS_VERSION,
    latestVersion: process.env.DOCS_LATEST_VERSION,
    latestUrl: process.env.DOCS_LATEST_URL,
    versionsUrl: process.env.DOCS_VERSIONS_URL,
  },
  readLatestPages(process.env.DOCS_LATEST_PAGES ?? ''),
)

/** Source folder of the documentation. */
const docsRoot = resolve(__dirname, '..')

/**
 * The pages each language really has, as address keys: English lists every translatable page, the other
 * languages only the files translators wrote (the generated placeholders are not in these folders).
 * The SEO hooks link a page to its other languages with `hreflang` from this.
 */
const pageLanguages: PageLanguages = {
  [DEFAULT_LOCALE]: new Set<string>(listEnglishPages(docsRoot, readSettings()).map(pageKey)),
  ...translatedKeys(docsRoot),
}

/** Per page description, canonical, social tags, JSON-LD, hreflang alternates and the sitemap filter, all built from `siteUrl`. */
const seo = createSeo(siteUrl, version, pageLanguages)

/**
 * Address of the page on GitHub, the same in every language. `filePath` is the real source (`en/staff/orders.md`,
 * `ru/staff/orders.md`), not the address the page is served at. A placeholder page has no file in the repository,
 * so its edit link goes to the path where the translation is to be created.
 * @param page The page data.
 * @returns The address.
 */
const editLinkOf = (page: { filePath: string }): string => {
  return `https://github.com/innovayse/hostpanel/edit/main/docs/${page.filePath.replace(/^_fallback\//, '')}`
}

/** Copyright line of the footer, the same in every language. */
const COPYRIGHT = 'Copyright © 2024-present Innovayse Digital Agency'

/**
 * The part of the theme config that depends on the language, per language: navigation, sidebar and the
 * labels of the default theme. English is the root of the site, the others sit below their folder.
 */
const localeTheme = Object.fromEntries(
  Object.values(LocaleKey).map((locale) => [
    locale,
    buildLocaleThemeConfig(locale, stringsFor(locale), {
      demoUrl: DEMO_ADMIN_URL,
      generated: generatedApiSidebarItems,
      editPattern: editLinkOf,
      copyright: COPYRIGHT,
    }),
  ]),
) as Record<LocaleKey, ReturnType<typeof buildLocaleThemeConfig>>

/**
 * The language entries of the config. English has the key `root` (served without a folder); each other
 * language has a title, a description, a menu label, a `lang` for the page and its own theme config.
 */
const locales = {
  root: {
    label: LOCALES[DEFAULT_LOCALE].label,
    lang: LOCALES[DEFAULT_LOCALE].lang,
    title: stringsFor(DEFAULT_LOCALE).site.title,
    description: stringsFor(DEFAULT_LOCALE).site.description,
  },
  ...Object.fromEntries(
    TRANSLATED_LOCALES.map((locale) => [
      locale,
      {
        label: LOCALES[locale].label,
        lang: LOCALES[locale].lang,
        link: `/${LOCALES[locale].prefix}/`,
        title: stringsFor(locale).site.title,
        description: stringsFor(locale).site.description,
        themeConfig: localeTheme[locale],
      },
    ]),
  ),
}

/** The search box texts of every language, keyed like the locales above (`root` for English). */
const searchLocales = Object.fromEntries(
  Object.values(LocaleKey).map((locale) => [locale === DEFAULT_LOCALE ? 'root' : locale, { translations: buildSearchTranslations(stringsFor(locale)) }]),
)

export default defineConfig({
  title: stringsFor(DEFAULT_LOCALE).site.title,
  description: stringsFor(DEFAULT_LOCALE).site.description,
  lang: LOCALES[DEFAULT_LOCALE].lang,
  cleanUrls: true,

  /** English at the root, Russian below /ru/ and Armenian below /hy/. */
  locales,

  /**
   * Every language keeps its pages in a folder of its own. English is served without its folder (the addresses
   * stay `/docs/staff/orders`), Russian and Armenian below `/ru/` and `/hy/`. The placeholder pages of
   * languages that lack a page (see `scripts/generate-locale-fallbacks.mjs`) are written to a folder of their
   * own and served at the address of the page they stand in for.
   */
  rewrites: {
    [`${LOCALES[DEFAULT_LOCALE].folder}/:rest*`]: ':rest*',
    [`${FALLBACK_DIR}/:rest*`]: ':rest*',
  },

  /** Generated fragments that pages include; they are not pages of their own. */
  srcExclude: [`${LOCALES[DEFAULT_LOCALE].folder}/api-reference/_partials/**`],

  /**
   * Replaces the default theme's search box with `theme/components/LocalSearchBox.vue`, a copy of
   * VitePress 1.6.4's `VPLocalSearchBox.vue` with a start view. This is the documented way to
   * override an internal theme component (a `resolve.alias` entry matching the component's file
   * name). The copy follows the pinned version, see the header of that file.
   */
  vite: {
    resolve: {
      alias: [
        {
          find: /^.*\/VPLocalSearchBox\.vue$/,
          replacement: fileURLToPath(new URL('./theme/components/LocalSearchBox.vue', import.meta.url)),
        },
      ],
    },
  },

  /**
   * The site data, which holds the sidebar of every endpoint of the API reference, is large. One
   * cached chunk costs less than repeating it in the HTML of every page.
   */
  metaChunk: true,

  /**
   * Defaults to /docs/, where the public site is served at
   * https://hostpanel.innovayse.com/docs/. Override with the `DOCS_BASE`
   * environment variable to publish under another path.
   */
  base,

  head: [
    ['link', { rel: 'icon', href: `${base}mark.svg`, type: 'image/svg+xml' }],
    ['link', { rel: 'icon', href: `${base}favicon-32x32.png`, type: 'image/png', sizes: '32x32' }],
    ['link', { rel: 'apple-touch-icon', href: `${base}apple-touch-icon.png`, sizes: '180x180' }],
    ['meta', { name: 'theme-color', content: '#323DF3' }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
  ],

  /**
   * The sitemap is written to the output root, so it is served at `<base>sitemap.xml`. The hostname
   * ends in a slash because it carries the base path and the page addresses are relative to it.
   * Pages marked Work in progress are left out.
   */
  sitemap: hasSitemap(version)
    ? {
        hostname: `${siteUrl}/`,
        transformItems: seo.sitemapItems,
      }
    : undefined,

  /**
   * Writes the navigation bar classes the browser sets after hydration into the rendered HTML, so the
   * static page already has its final layout (see `navClasses.ts`).
   */
  transformHtml: (code) => withNavBarClasses(code),

  /** Gives every page its own description and records the stubs that must not be indexed. */
  transformPageData: seo.transformPageData,

  /** Adds the per page canonical, Open Graph, Twitter, robots and JSON-LD tags. */
  transformHead: seo.transformHead,

  themeConfig: {
    /** Separate light and dark logos; VitePress prepends `base` automatically. */
    logo: {
      light: '/logo-light.svg',
      dark: '/logo-dark.svg',
      alt: 'Hostpanel',
    },

    siteTitle: false,

    /** Navigation, sidebar and the labels of the default theme in English; the other languages bring their own in `locales`. */
    ...localeTheme[DEFAULT_LOCALE],

    search: {
      provider: 'local',
      options: { locales: searchLocales },
    },

    socialLinks: [
      { icon: 'discord', link: 'https://discord.gg/NfqTkyTgD' },
      { icon: 'github', link: 'https://github.com/innovayse/hostpanel' },
    ],

    /** Read by the version switcher and the notice banner of the theme. */
    docsVersion: {
      kind: version.kind,
      label: version.label,
      latestUrl: version.latestUrl,
      versionsUrl: version.versionsUrl,
    },

    demoAdminUrl: DEMO_ADMIN_URL,
    demoClientUrl: DEMO_CLIENT_URL,
  },
})
