/**
 * The catalogue behind the settings index: which sections exist, how they are grouped and where
 * each one leads.
 *
 * Kept as data so the index screen only renders it, and a new section is one entry here plus its
 * locale keys (`settings.index.items.<item>` and `settings.index.groups.<group>`).
 */
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'

/** Closed set of groups on the settings index. */
export enum SettingsGroup {
  /** What the storefront shows and sells. */
  Storefront = 'storefront',
  /** Money: gateways, currencies, taxes. */
  Billing = 'billing',
  /** Messages the panel sends. */
  Communication = 'communication',
  /** Protection of the panel and its orders. */
  Security = 'security',
}

/** Closed set of the sections the index links to; the value is the locale key segment. */
export enum SettingsItem {
  /** Site identity, appearance and every global key. */
  General = 'general',
  /** Products and plans. */
  Products = 'products',
  /** Product bundles. */
  ProductBundles = 'productBundles',
  /** Domain extension prices. */
  TldPricing = 'tldPricing',
  /** Home page slides. */
  Slides = 'slides',
  /** Payment gateways (under integrations). */
  Gateways = 'gateways',
  /** Currencies and rates. */
  Currencies = 'currencies',
  /** Tax rules. */
  TaxRules = 'taxRules',
  /** Email templates. */
  EmailTemplates = 'emailTemplates',
  /** Fraud protection. */
  Fraud = 'fraud',
}

/** One link of the index. */
export interface SettingsIndexEntry {
  /** The section the card leads to. */
  item: SettingsItem
  /** Where it leads. */
  to: AppRoute
  /** Inner SVG markup of the card's icon. */
  icon: string
}

/** One titled group of links. */
export interface SettingsIndexGroup {
  /** The group, which names its heading. */
  group: SettingsGroup
  /** The links in display order. */
  entries: SettingsIndexEntry[]
}

/** The index, in display order. */
export const SETTINGS_INDEX: SettingsIndexGroup[] = [
  {
    group: SettingsGroup.Storefront,
    entries: [
      { item: SettingsItem.General, to: AppRoute.SettingsGeneral, icon: Icon.Globe },
      { item: SettingsItem.Products, to: AppRoute.SettingsProducts, icon: Icon.Box },
      { item: SettingsItem.ProductBundles, to: AppRoute.SettingsProductBundles, icon: Icon.Archive },
      { item: SettingsItem.TldPricing, to: AppRoute.SettingsTldPricing, icon: Icon.Tag },
      { item: SettingsItem.Slides, to: AppRoute.SettingsSlides, icon: Icon.Image },
    ],
  },
  {
    group: SettingsGroup.Billing,
    entries: [
      { item: SettingsItem.Gateways, to: AppRoute.IntegrationsPayments, icon: Icon.Puzzle },
      { item: SettingsItem.Currencies, to: AppRoute.SettingsCurrencies, icon: Icon.Dollar },
      { item: SettingsItem.TaxRules, to: AppRoute.SettingsTaxRules, icon: Icon.List },
    ],
  },
  {
    group: SettingsGroup.Communication,
    entries: [{ item: SettingsItem.EmailTemplates, to: AppRoute.SettingsEmailTemplates, icon: Icon.Mail }],
  },
  {
    group: SettingsGroup.Security,
    entries: [{ item: SettingsItem.Fraud, to: AppRoute.FraudSettings, icon: Icon.Shield }],
  },
]
