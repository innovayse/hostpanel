/**
 * The admin's main navigation: the one place that decides which entries the sidebar shows, in
 * which order and under which section label. The desktop sidebar and the phone menu drawer both
 * draw this list, so their order cannot drift apart. The Add-ons section is built from the menu
 * items plugins declare ({@link withAddons}) and is left out while no plugin declares one.
 */
import { pluginIconPath } from '@/lib/pluginView'
import { AppRoute } from '@/lib/routes'
import type { PluginMenuLink } from '@/types/pluginui'

/** Closed set of sidebar sections, in the order an operator works: daily work, insights, setup, add-ons, system. */
export enum NavSectionId {
  /** Everyday work: clients, orders, billing, domains and support. */
  Daily = 'daily',
  /** Reports. */
  Insights = 'insights',
  /** One-time and occasional setup: servers, integrations, plugins and storefront content. */
  Setup = 'setup',
  /** The pages plugins add, one entry per menu item a plugin declares. */
  Addons = 'addons',
  /** Rarely used tools: migration and settings. */
  System = 'system',
}

/** A sub-menu child item. */
export interface NavChild {
  /** Route path. */
  to: string
  /** i18n key for the display label. */
  labelKey: string
}

/** What every top-level item carries besides its label. */
interface NavItemBase {
  /** Route path (used when no children). */
  to: string
  /** SVG path data for the icon. */
  icon: string
  /** Optional sub-menu items. */
  children?: NavChild[]
}

/**
 * A top-level navigation item, optionally with children. Its label is an i18n key for the panel's own
 * entries, or the text the API already put in the operator's language for a plugin's entry.
 */
export type NavItem = NavItemBase & (
  | {
    /** i18n key for the display label. */
    labelKey: string
    /** Not set on an entry of the panel's own. */
    label?: never
  }
  | {
    /** Not set on a plugin's entry. */
    labelKey?: never
    /** The display label, already translated by the API. */
    label: string
  })

/** A labelled group of top-level items. */
export interface NavSection {
  /** Stable id of the section, also used for its test id. */
  id: NavSectionId
  /** i18n key for the section label. */
  labelKey: string
  /** The section's items, in display order. */
  items: NavItem[]
}

/** The sidebar sections and their items, in display order. */
export const NAV_SECTIONS: readonly NavSection[] = [
  {
    /** Daily work. */
    id: NavSectionId.Daily, labelKey: 'nav.sections.daily',
    items: [
      {
        to: AppRoute.Dashboard, labelKey: 'nav.dashboard',
        icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
      },
      {
        to: AppRoute.Clients, labelKey: 'nav.clients',
        icon: 'M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8zm12 2v2m0 0v2m0-2h2m-2 0h-2',
        children: [
          { to: AppRoute.Clients, labelKey: 'nav.clientsChildren.view' },
          { to: AppRoute.ClientsUsers, labelKey: 'nav.clientsChildren.users' },
          { to: AppRoute.ClientsSharedHostings, labelKey: 'nav.clientsChildren.sharedHostings' },
          { to: AppRoute.ClientsAddons, labelKey: 'nav.clientsChildren.addons' },
          { to: AppRoute.ClientsDomainRegistrations, labelKey: 'nav.clientsChildren.domainRegistrations' },
          { to: AppRoute.ClientsCancellations, labelKey: 'nav.clientsChildren.cancellations' },
          { to: AppRoute.ClientsAffiliates, labelKey: 'nav.clientsChildren.affiliates' },
        ],
      },
      {
        to: AppRoute.Orders, labelKey: 'nav.orders',
        icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 7h6m-6 4h4',
      },
      {
        to: AppRoute.Billing, labelKey: 'nav.billing',
        icon: 'M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z',
        children: [
          { to: AppRoute.BillingTransactions, labelKey: 'nav.billingChildren.transactions' },
          { to: AppRoute.BillingInvoices, labelKey: 'nav.billingChildren.invoices' },
          { to: AppRoute.BillingBillableItems, labelKey: 'nav.billingChildren.billableItems' },
          { to: AppRoute.BillingQuotes, labelKey: 'nav.billingChildren.quotes' },
          { to: AppRoute.BillingOfflineCc, labelKey: 'nav.billingChildren.offlineCc' },
          { to: AppRoute.BillingDisputes, labelKey: 'nav.billingChildren.disputes' },
          { to: AppRoute.BillingGatewayLog, labelKey: 'nav.billingChildren.gatewayLog' },
          { to: AppRoute.BillingPaymentClaims, labelKey: 'nav.billingChildren.paymentClaims' },
          { to: AppRoute.BillingBankTransfers, labelKey: 'nav.billingChildren.bankTransfers' },
          { to: AppRoute.BillingCreditNotes, labelKey: 'nav.billingChildren.creditNotes' },
          { to: AppRoute.BillingRenewals, labelKey: 'nav.billingChildren.renewals' },
        ],
      },
      {
        to: AppRoute.Domains, labelKey: 'nav.domains',
        icon: 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9',
      },
      {
        to: AppRoute.Support, labelKey: 'nav.support',
        icon: 'M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192l-3.536 3.536M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-5 0a4 4 0 11-8 0 4 4 0 018 0z',
        children: [
          { to: AppRoute.SupportOverview, labelKey: 'nav.supportChildren.overview' },
          { to: AppRoute.SupportTickets, labelKey: 'nav.supportChildren.tickets' },
          { to: AppRoute.SupportTicketsNew, labelKey: 'nav.supportChildren.newTicket' },
          { to: AppRoute.SupportPredefinedReplies, labelKey: 'nav.supportChildren.predefinedReplies' },
          { to: AppRoute.SupportAnnouncements, labelKey: 'nav.supportChildren.announcements' },
          { to: AppRoute.SupportEmailCampaigns, labelKey: 'nav.supportChildren.emailCampaigns' },
          { to: AppRoute.SupportDownloads, labelKey: 'nav.supportChildren.downloads' },
          { to: AppRoute.SupportKnowledgebase, labelKey: 'nav.supportChildren.knowledgebase' },
          { to: AppRoute.SupportNetworkIssues, labelKey: 'nav.supportChildren.networkIssues' },
        ],
      },
    ],
  },
  {
    /** Insights. */
    id: NavSectionId.Insights, labelKey: 'nav.sections.insights',
    items: [
      {
        to: AppRoute.Reports, labelKey: 'nav.reports',
        icon: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z',
      },
    ],
  },
  {
    /** Setup. */
    id: NavSectionId.Setup, labelKey: 'nav.sections.setup',
    items: [
      {
        to: AppRoute.Servers, labelKey: 'nav.servers',
        icon: 'M5 12H3a2 2 0 00-2 2v4a2 2 0 002 2h18a2 2 0 002-2v-4a2 2 0 00-2-2h-2M5 12V8a2 2 0 012-2h10a2 2 0 012 2v4M5 12h14M8 20h.01M12 20h.01M16 20h.01',
      },
      {
        to: AppRoute.Integrations, labelKey: 'nav.integrations',
        icon: 'M13 10V3L4 14h7v7l9-11h-7z',
      },
      {
        to: AppRoute.Plugins, labelKey: 'nav.plugins',
        icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4',
      },
      {
        to: AppRoute.Storefront, labelKey: 'nav.storefront',
        icon: 'M3 9l1-5h16l1 5M3 9v11h18V9M3 9h18M9 20v-6h6v6',
        children: [
          { to: AppRoute.StorefrontTestimonials, labelKey: 'nav.storefrontChildren.testimonials' },
          { to: AppRoute.StorefrontPartners, labelKey: 'nav.storefrontChildren.partners' },
          { to: AppRoute.StorefrontProjects, labelKey: 'nav.storefrontChildren.projects' },
          { to: AppRoute.StorefrontFaqs, labelKey: 'nav.storefrontChildren.faqs' },
          { to: AppRoute.StorefrontLocations, labelKey: 'nav.storefrontChildren.locations' },
        ],
      },
    ],
  },
  {
    /** System. */
    id: NavSectionId.System, labelKey: 'nav.sections.system',
    items: [
      {
        to: AppRoute.Migration, labelKey: 'nav.migration',
        icon: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12',
      },
      {
        to: AppRoute.Settings, labelKey: 'nav.settings',
        icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
        children: [
          { to: AppRoute.Settings, labelKey: 'nav.settingsChildren.general' },
          { to: AppRoute.SettingsProducts, labelKey: 'nav.settingsChildren.products' },
          { to: AppRoute.SettingsTldPricing, labelKey: 'nav.settingsChildren.tldPricing' },
          { to: AppRoute.SettingsSlides, labelKey: 'nav.settingsChildren.slides' },
          { to: AppRoute.SettingsEmailTemplates, labelKey: 'nav.settingsChildren.emailTemplates' },
          { to: AppRoute.IntegrationsPayments, labelKey: 'nav.settingsChildren.gateways' },
          { to: AppRoute.SettingsCurrencies, labelKey: 'nav.settingsChildren.currencies' },
        ],
      },
    ],
  },
]

/**
 * The sidebar sections with the Add-ons section of the plugins' menu items, placed before System. The
 * section is left out while no plugin declares an item, so a panel without plugins shows only its own.
 *
 * @param sections - The panel's own sections, in display order.
 * @param links - The plugins' menu entries, already in display order.
 * @returns The sections to draw.
 */
export const withAddons = (sections: readonly NavSection[], links: readonly PluginMenuLink[]): NavSection[] => {
  if (links.length === 0) {
    return [...sections]
  }
  const addons: NavSection = {
    id: NavSectionId.Addons,
    labelKey: 'nav.sections.addons',
    items: links.map((link) => ({ to: link.to, label: link.label, icon: pluginIconPath(link.icon) })),
  }
  const system = sections.findIndex((section) => section.id === NavSectionId.System)
  const at = system === -1 ? sections.length : system
  return [...sections.slice(0, at), addons, ...sections.slice(at)]
}
