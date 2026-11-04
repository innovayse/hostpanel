/**
 * VitePress site configuration for the Hostpanel documentation website.
 * The site is served under a configurable URL path. The GitHub Pages project
 * path /hostpanel/ is only the default; set `DOCS_BASE` and `DOCS_SITE_URL`
 * (see below) to publish it anywhere else, for example at the root of a
 * custom domain.
 *
 * For the default GitHub Pages deploy, switch the Pages source to "GitHub
 * Actions" in Settings → Pages → Build and deployment before the first deploy.
 */
import { defineConfig } from 'vitepress'

/**
 * URL path the site is served under, always with a leading and a trailing slash.
 * Set `DOCS_BASE` when the site is published somewhere other than the default
 * GitHub Pages project path (for example `/` on a custom domain). The value is
 * trimmed and normalized, so `docs`, ` /docs ` and `/docs/` all give `/docs/`;
 * an empty or blank value falls back to the default.
 */
const base: string = `/${(process.env.DOCS_BASE?.trim() || '/hostpanel/').replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/')

/**
 * Public origin plus base path of the published site, without a trailing slash.
 * Used for absolute URLs such as the social preview image. Set `DOCS_SITE_URL`
 * to match `DOCS_BASE` when publishing elsewhere.
 */
const siteUrl: string = (process.env.DOCS_SITE_URL?.trim() || 'https://innovayse.github.io/hostpanel').replace(/\/+$/, '')

export default defineConfig({
  title: 'Hostpanel',
  description: 'Open source web hosting control panel',
  lang: 'en-US',
  cleanUrls: true,

  /**
   * Defaults to the GitHub Pages project path (/hostpanel/), where the site is
   * served at https://<org>.github.io/hostpanel/. Override with the `DOCS_BASE`
   * environment variable to publish under another path.
   */
  base,

  head: [
    ['link', { rel: 'icon', href: `${base}mark.svg`, type: 'image/svg+xml' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Hostpanel' }],
    ['meta', { property: 'og:description', content: 'Open source web hosting control panel' }],
    ['meta', { property: 'og:image', content: `${siteUrl}/social-preview.png` }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
    ['meta', { name: 'twitter:title', content: 'Hostpanel' }],
    ['meta', { name: 'twitter:description', content: 'Open source web hosting control panel' }],
    ['meta', { name: 'twitter:image', content: `${siteUrl}/social-preview.png` }],
  ],

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
    ],

    sidebar: [
      {
        text: 'Get started',
        collapsed: true,
        items: [
          { text: 'Introduction', link: '/get-started/' },
          { text: 'Requirements', link: '/get-started/requirements' },
          { text: 'Quick start', link: '/get-started/quick-start' },
        ],
      },
      {
        text: 'Operators',
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
        text: 'Staff',
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
        text: 'Customers',
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
        text: 'Payments',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/payments/' },
          { text: 'Gateways', link: '/payments/gateways' },
          { text: 'How online payments are confirmed', link: '/payments/confirming' },
          { text: 'Plugins', link: '/payments/plugins' },
        ],
      },
      {
        text: 'Servers & SSH',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/servers/' },
          { text: 'Servers', link: '/servers/servers' },
          { text: 'SSH key agent', link: '/servers/ssh-key-agent' },
          { text: 'Provisioning', link: '/servers/provisioning' },
        ],
      },
      {
        text: 'Migration',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/migration/' },
          { text: 'Moving from WHMCS', link: '/migration/from-another-system' },
          { text: 'Import report and skip reasons', link: '/migration/import-report' },
        ],
      },
      {
        text: 'Developers',
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
          { text: 'Translating', link: '/developers/translating' },
          { text: 'Integrations', link: '/developers/integrations' },
          { text: 'Write a plugin', link: '/developers/write-a-plugin' },
          { text: 'Publishing a plugin', link: '/developers/publishing-a-plugin' },
        ],
      },
      {
        text: 'Contributing',
        collapsed: true,
        items: [
          { text: 'Components', link: '/components' },
        ],
      },
    ],

    editLink: {
      pattern: 'https://github.com/innovayse/hostpanel/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },

    lastUpdated: true,

    search: {
      provider: 'local',
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/innovayse/hostpanel' },
    ],

    footer: {
      message: 'Released under the AGPL-3.0 License.',
      copyright: 'Copyright © 2024-present Innovayse Digital Agency',
    },
  },
})
