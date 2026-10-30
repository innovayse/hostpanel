import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Hostpanel',
  description: 'Open source web hosting control panel',
  lang: 'en-US',
  cleanUrls: true,

  head: [
    ['link', { rel: 'icon', href: '/mark.svg', type: 'image/svg+xml' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Hostpanel' }],
    ['meta', { property: 'og:description', content: 'Open source web hosting control panel' }],
    ['meta', { property: 'og:image', content: '/social-preview.png' }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
    ['meta', { name: 'twitter:title', content: 'Hostpanel' }],
    ['meta', { name: 'twitter:description', content: 'Open source web hosting control panel' }],
    ['meta', { name: 'twitter:image', content: '/social-preview.png' }],
  ],

  themeConfig: {
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
          { text: 'Installation', link: '/operators/install' },
          { text: 'Upgrade', link: '/operators/upgrade' },
          { text: 'Backup', link: '/operators/backup' },
          {
            text: 'Rollback',
            link: 'https://github.com/innovayse/hostpanel/issues/602',
          },
          { text: 'Storefront & branding', link: '/operators/storefront' },
          { text: 'Configuration reference', link: '/operators/configuration' },
        ],
      },
      {
        text: 'Staff',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/staff/' },
          { text: 'Clients', link: '/staff/clients' },
          { text: 'Orders', link: '/staff/orders' },
          { text: 'Billing', link: '/staff/billing' },
          { text: 'Products & pricing', link: '/staff/products' },
          { text: 'Support desk', link: '/staff/support' },
        ],
      },
      {
        text: 'Customers',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/customers/' },
          {
            text: 'Client portal',
            link: 'https://github.com/innovayse/hostpanel/issues/611',
          },
        ],
      },
      {
        text: 'Payments',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/payments/' },
          { text: 'Gateways', link: '/payments/gateways' },
          {
            text: 'Plugins',
            link: 'https://github.com/innovayse/hostpanel/issues/609',
          },
        ],
      },
      {
        text: 'Servers & SSH',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/servers/' },
          { text: 'Servers', link: '/servers/servers' },
          { text: 'SSH key agent', link: '/servers/ssh-key-agent' },
          {
            text: 'Provisioning',
            link: 'https://github.com/innovayse/hostpanel/issues/607',
          },
        ],
      },
      {
        text: 'Migration',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/migration/' },
          {
            text: 'From WHMCS',
            link: 'https://github.com/innovayse/hostpanel/issues/610',
          },
        ],
      },
      {
        text: 'Developers',
        collapsed: true,
        items: [
          { text: 'Overview', link: '/developers/' },
          { text: 'Architecture', link: '/developers/architecture' },
          { text: 'Local development', link: '/developers/local-dev' },
          { text: 'Contributing', link: '/developers/contributing' },
          { text: 'Translating', link: '/developers/translating' },
          { text: 'Integrations', link: '/developers/integrations' },
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
