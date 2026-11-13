/**
 * The admin's main navigation: the one place that decides which entries the sidebar shows, in
 * which order and under which section label. The desktop sidebar and the phone menu drawer both
 * draw this list, so their order cannot drift apart. The Add-ons section is built from the menu
 * items plugins declare ({@link withAddons}) and is left out while no plugin declares one.
 */
import { Icon } from '@/lib/icons'
import { pluginIconPath } from '@/lib/pluginView'
import { AppRoute } from '@/lib/routes'
import type { PluginMenuLink } from '@/types/pluginui'

/** Closed set of sidebar sections, by daily frequency and workflow: home, sales, support, catalog, infrastructure, insights, add-ons, system. */
export enum NavSectionId {
  /** The dashboard, alone and without a heading. */
  Home = 'home',
  /** Clients, orders, billing and domains. */
  Sales = 'sales',
  /** Support tickets and the help content. */
  Support = 'support',
  /** What the storefront shows: its content pages. */
  Catalog = 'catalog',
  /** Servers, integrations and plugins. */
  Infrastructure = 'infrastructure',
  /** Reports. */
  Insights = 'insights',
  /** The pages plugins add, one entry per menu item a plugin declares. */
  Addons = 'addons',
  /** Rarely used tools: settings, fraud and migration. */
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
  /** Inner SVG markup of the icon, drawn by `UiIcon` in the sidebar, the rail and the command palette. */
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
  /** i18n key for the section label; a section without one is drawn without a heading. */
  labelKey?: string
  /** The section's items, in display order. */
  items: NavItem[]
}

/** The sidebar sections and their items, in display order. */
export const NAV_SECTIONS: readonly NavSection[] = [
  {
    /** The landing page, under no heading. */
    id: NavSectionId.Home,
    items: [
      {
        to: AppRoute.Dashboard, labelKey: 'nav.dashboard',
        icon: Icon.Dashboard,
      },
    ],
  },
  {
    /** Sales. */
    id: NavSectionId.Sales, labelKey: 'nav.sections.sales',
    items: [
      {
        to: AppRoute.Clients, labelKey: 'nav.clients',
        icon: Icon.Users,
        children: [
          { to: AppRoute.Clients, labelKey: 'nav.clientsChildren.view' },
          { to: AppRoute.ClientsUsers, labelKey: 'nav.clientsChildren.users' },
          { to: AppRoute.ClientsGroups, labelKey: 'nav.clientsChildren.groups' },
          { to: AppRoute.ClientsSharedHostings, labelKey: 'nav.clientsChildren.sharedHostings' },
          { to: AppRoute.ClientsAddons, labelKey: 'nav.clientsChildren.addons' },
          { to: AppRoute.ClientsDomainRegistrations, labelKey: 'nav.clientsChildren.domainRegistrations' },
          { to: AppRoute.ClientsCancellations, labelKey: 'nav.clientsChildren.cancellations' },
          { to: AppRoute.ClientsPlanChanges, labelKey: 'nav.clientsChildren.planChanges' },
          { to: AppRoute.ClientsAffiliates, labelKey: 'nav.clientsChildren.affiliates' },
        ],
      },
      {
        to: AppRoute.Orders, labelKey: 'nav.orders',
        icon: Icon.Cart,
      },
      {
        to: AppRoute.Billing, labelKey: 'nav.billing',
        icon: Icon.CreditCard,
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
        icon: Icon.Globe,
      },
    ],
  },
  {
    /** Support. */
    id: NavSectionId.Support, labelKey: 'nav.sections.support',
    items: [
      {
        to: AppRoute.Support, labelKey: 'nav.support',
        icon: Icon.LifeBuoy,
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
    /** Catalog. */
    id: NavSectionId.Catalog, labelKey: 'nav.sections.catalog',
    items: [
      {
        to: AppRoute.Storefront, labelKey: 'nav.storefront',
        icon: Icon.Storefront,
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
    /** Infrastructure. */
    id: NavSectionId.Infrastructure, labelKey: 'nav.sections.infrastructure',
    items: [
      {
        to: AppRoute.Servers, labelKey: 'nav.servers',
        icon: Icon.Rack,
      },
      {
        to: AppRoute.Integrations, labelKey: 'nav.integrations',
        icon: Icon.Zap,
      },
      {
        to: AppRoute.Plugins, labelKey: 'nav.plugins',
        icon: Icon.Puzzle,
      },
    ],
  },
  {
    /** Insights. */
    id: NavSectionId.Insights, labelKey: 'nav.sections.insights',
    items: [
      {
        to: AppRoute.Reports, labelKey: 'nav.reports',
        icon: Icon.BarChart,
      },
    ],
  },
  {
    /** System. */
    id: NavSectionId.System, labelKey: 'nav.sections.system',
    items: [
      {
        to: AppRoute.Settings, labelKey: 'nav.settings',
        icon: Icon.Settings,
        children: [
          { to: AppRoute.SettingsGeneral, labelKey: 'nav.settingsChildren.general' },
          { to: AppRoute.SettingsProducts, labelKey: 'nav.settingsChildren.products' },
          { to: AppRoute.SettingsProductBundles, labelKey: 'nav.settingsChildren.productBundles' },
          { to: AppRoute.SettingsTldPricing, labelKey: 'nav.settingsChildren.tldPricing' },
          { to: AppRoute.SettingsSlides, labelKey: 'nav.settingsChildren.slides' },
          { to: AppRoute.SettingsEmailTemplates, labelKey: 'nav.settingsChildren.emailTemplates' },
          { to: AppRoute.IntegrationsPayments, labelKey: 'nav.settingsChildren.gateways' },
          { to: AppRoute.SettingsCurrencies, labelKey: 'nav.settingsChildren.currencies' },
          { to: AppRoute.SettingsTaxRules, labelKey: 'nav.settingsChildren.taxRules' },
        ],
      },
      {
        to: AppRoute.Fraud, labelKey: 'nav.fraud',
        icon: Icon.Shield,
        children: [
          { to: AppRoute.FraudSettings, labelKey: 'nav.fraudChildren.settings' },
          { to: AppRoute.FraudBlockLists, labelKey: 'nav.fraudChildren.blockLists' },
          { to: AppRoute.FraudLog, labelKey: 'nav.fraudChildren.log' },
        ],
      },
      {
        to: AppRoute.Migration, labelKey: 'nav.migration',
        icon: Icon.Upload,
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
    items: links.map((link) => ({ to: link.to, label: link.label, icon: `<path d="${pluginIconPath(link.icon)}"/>` })),
  }
  const system = sections.findIndex((section) => section.id === NavSectionId.System)
  const at = system === -1 ? sections.length : system
  return [...sections.slice(0, at), addons, ...sections.slice(at)]
}
