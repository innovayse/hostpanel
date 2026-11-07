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
import { hasSitemap, readLatestPages, resolveVersion } from './versions'
import { sidebarSectionText } from './sidebarIcon'
import type { SidebarIcon } from './sidebarIcon'

const __dirname = dirname(fileURLToPath(import.meta.url))
/** A group or a page of the generated endpoint sidebar; groups nest (audience, then area). */
type GeneratedSidebarItem = { text: string; link?: string; collapsed?: boolean; items?: GeneratedSidebarItem[] }

/**
 * The endpoint groups of the API reference. `scripts/generate-api-reference.mjs` writes this file
 * from the OpenAPI document in CI; it is not committed, so a plain checkout has no endpoint pages.
 */
const generatedSidebarPath = resolve(__dirname, '../api-reference/generated-sidebar.json')
const generatedApiSidebarItems: GeneratedSidebarItem[] = existsSync(generatedSidebarPath)
  ? JSON.parse(readFileSync(generatedSidebarPath, 'utf8'))
  : []

/**
 * Icon of each endpoint group, by the group title `scripts/generate-api-reference.mjs` writes, so the
 * API reference sidebar looks like the main one. A group with a new title falls back to the book icon.
 */
const API_GROUP_ICONS: Record<string, SidebarIcon> = {
  'Auth endpoints': 'key-round',
  'Public endpoints': 'globe',
  'Client endpoints': 'user',
  'Any signed-in user': 'user-check',
  'Admin endpoints': 'shield-check',
}

/** The sidebar of the API reference: the pages every endpoint shares, then the generated endpoint groups. */
const apiReferenceSidebar: GeneratedSidebarItem[] = [
  {
    text: sidebarSectionText('book-open', 'API reference'),
    items: [
      { text: 'Overview', link: '/api-reference/' },
      { text: 'Authentication', link: '/api-reference/authentication' },
      { text: 'Errors', link: '/api-reference/errors' },
      { text: 'Languages', link: '/api-reference/languages' },
      { text: 'Rate limits', link: '/api-reference/rate-limits' },
      { text: 'Pagination', link: '/api-reference/pagination' },
    ],
  },
  ...generatedApiSidebarItems.map((group) => ({
    ...group,
    text: sidebarSectionText(API_GROUP_ICONS[group.text] ?? 'book-open', group.text),
  })),
]

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

/** Per page description, canonical, social tags, JSON-LD and the sitemap filter, all built from `siteUrl`. */
const seo = createSeo(siteUrl, version)

export default defineConfig({
  title: 'Hostpanel',
  description: 'Open source web hosting control panel',
  lang: 'en-US',
  cleanUrls: true,

  /** Generated fragments that pages include; they are not pages of their own. */
  srcExclude: ['api-reference/_partials/**'],

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

    nav: [
      { text: 'Home', link: '/' },
      { text: 'Get started', link: '/get-started/' },
      { text: 'Guides', link: '/operators/' },
      { text: 'API Reference', link: '/api-reference/' },
      { text: 'Changelog', link: '/changelog/' },
      ...(DEMO_ADMIN_URL ? [{ text: 'Try the demo', link: DEMO_ADMIN_URL }] : []),
    ],

    sidebar: {
      /** The API reference has a sidebar of its own, so its endpoint pages do not weigh down every other page. */
      '/api-reference/': apiReferenceSidebar,
      '/': [
        {
          text: sidebarSectionText('rocket', 'Get started'),
          collapsed: true,
          items: [
            { text: 'Introduction', link: '/get-started/' },
            { text: 'Requirements', link: '/get-started/requirements' },
            { text: 'Quick start', link: '/get-started/quick-start' },
          ],
        },
        {
          text: sidebarSectionText('wrench', 'Operators'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/operators/' },
            { text: 'Install Hostpanel', link: '/operators/install' },
            { text: 'Upgrade', link: '/operators/upgrade' },
            { text: 'Back up and restore', link: '/operators/backup' },
            { text: 'Rollback', link: '/operators/rollback' },
            { text: 'Storefront & branding', link: '/operators/storefront' },
            { text: 'Configuration reference', link: '/operators/configuration' },
          ],
        },
        {
          text: sidebarSectionText('briefcase', 'Staff'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/staff/' },
            { text: 'Clients, users and contacts', link: '/staff/clients' },
            { text: 'Orders', link: '/staff/orders' },
            { text: 'Invoices and payments', link: '/staff/billing' },
            { text: 'Transactions, billable items and quotes', link: '/staff/transactions' },
            { text: 'Currencies, renewals and jobs', link: '/staff/currencies' },
            { text: 'Products and pricing', link: '/staff/products' },
            { text: 'Domain extensions and registrars', link: '/staff/tld-pricing' },
            { text: 'Domains', link: '/staff/domains' },
            { text: 'Support tickets', link: '/staff/support-tickets' },
            { text: 'Announcements, knowledge base and downloads', link: '/staff/announcements' },
            { text: 'Network issues', link: '/staff/network-issues' },
            { text: 'Email templates', link: '/staff/email-templates' },
          ],
        },
        {
          text: sidebarSectionText('users', 'Customers'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/customers/' },
            { text: 'Client portal', link: '/customers/portal' },
            { text: 'Your account and signing in', link: '/customers/account' },
            { text: 'Two-factor authentication', link: '/customers/two-factor' },
            { text: 'Ordering', link: '/customers/ordering' },
            { text: 'Invoices and paying', link: '/customers/invoices' },
            { text: 'Services and SSH keys', link: '/customers/services' },
            { text: 'Domains', link: '/customers/domains' },
            { text: 'Support tickets', link: '/customers/tickets' },
          ],
        },
        {
          text: sidebarSectionText('credit-card', 'Payments'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/payments/' },
            { text: 'Gateways', link: '/payments/gateways' },
            { text: 'How online payments are confirmed', link: '/payments/confirming' },
            { text: 'Plugins', link: '/payments/plugins' },
          ],
        },
        {
          text: sidebarSectionText('server', 'Servers & SSH'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/servers/' },
            { text: 'Servers', link: '/servers/servers' },
            { text: 'cPanel servers', link: '/servers/cpanel' },
            { text: 'SSH key agent', link: '/servers/ssh-key-agent' },
            { text: 'Provisioning', link: '/servers/provisioning' },
          ],
        },
        {
          text: sidebarSectionText('arrow-left-right', 'Migration'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/migration/' },
            { text: 'Moving from WHMCS', link: '/migration/from-another-system' },
            { text: 'Import report and skip reasons', link: '/migration/import-report' },
          ],
        },
        {
          text: sidebarSectionText('code', 'Developers'),
          collapsed: true,
          items: [
            { text: 'Overview', link: '/developers/' },
            { text: 'Architecture overview', link: '/developers/architecture' },
            { text: 'Backend', link: '/developers/backend' },
            { text: 'Frontends', link: '/developers/frontends' },
            { text: 'Run locally', link: '/developers/local-dev' },
            { text: 'Tests and checks', link: '/developers/tests' },
            { text: 'Where a new feature goes', link: '/developers/new-feature' },
            { text: 'Contributing', link: '/developers/contributing' },
            { text: 'Translating Hostpanel', link: '/developers/translating' },
            { text: 'Documentation versions', link: '/developers/documentation-versions' },
            { text: 'Add a new language', link: '/developers/add-language' },
            { text: 'Integrations', link: '/developers/integrations' },
            { text: 'Write a plugin', link: '/developers/write-a-plugin' },
            { text: 'Publishing a plugin', link: '/developers/publishing-a-plugin' },
          ],
        },
        {
          text: sidebarSectionText('git-pull-request', 'Contributing'),
          collapsed: true,
          items: [
            { text: 'Components', link: '/components' },
          ],
        },
      ],
    },

    editLink: {
      pattern: 'https://github.com/innovayse/hostpanel/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },

    lastUpdated: true,

    search: {
      provider: 'local',
    },

    socialLinks: [
      { icon: 'discord', link: 'https://discord.gg/NfqTkyTgD' },
      { icon: 'github', link: 'https://github.com/innovayse/hostpanel' },
    ],

    footer: {
      message: 'Released under the AGPL-3.0 License.',
      copyright: 'Copyright © 2024-present Innovayse Digital Agency',
    },

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
