/**
 * Display metadata of the integrations screen: the category sections and the catalogue of integrations
 * (label, logo and settings fields) the admin renders.
 */

import { IntegrationCategory, IntegrationEncryption, IntegrationFieldType } from '@/types/integration'
import type { IntegrationMeta, IntegrationSlug } from '@/types/integration'
import { PluginCategory } from '@/types/plugin'
import { Icon } from '@/lib/icons'
import { translate } from '@/lib/translate'

/**
 * Metadata for a single integration category section.
 */
export interface IntegrationCategoryMeta {
  /** Category key matching IntegrationCategory. */
  key: IntegrationCategory
  /** Locale key of the full title: the page header and the menu entry's tooltip. */
  labelKey: string
  /** Locale key of the short caption the inner menu shows; the layout skeleton draws its entries from it. */
  menuLabelKey: string
  /** Locale key of the sentence that says what the category holds; the page shows it while the category is empty. */
  descriptionKey: string
  /** The category icon, a member of {@link Icon}, drawn by `UiIcon`. */
  icon: string
  /** The plugin registry category whose listing holds the plugins of this category; undefined when the registry has none for it. */
  registryCategory?: PluginCategory
}

/**
 * Builds the public URL of an integration logo.
 *
 * @param file - File name inside `public/integrations/`.
 * @returns The URL to use as an `<img>` source.
 */
const logoUrl = (file: string): string =>
  `/integrations/${file}`

/**
 * Static metadata for all supported integrations.
 * Keyed by slug for O(1) lookup.
 */
export const INTEGRATION_META: Record<IntegrationSlug, IntegrationMeta> = {
  'bank-transfer': {
    slug: 'bank-transfer',
    color: 'bg-green-600',
    logo: logoUrl('bank-transfer.svg'),
    get shortDescription() { return translate('integrationMeta.bankTransfer.shortDescription') },
    category: 'payments',
    fields: [
      { key: 'accountName', get label() { return translate('integrationMeta.bankTransfer.fieldAccountName') }, type: IntegrationFieldType.Text },
      { key: 'iban', label: 'IBAN', type: IntegrationFieldType.Text },
      { key: 'bankName', get label() { return translate('reports.directDebit.bankName') }, type: IntegrationFieldType.Text },
      { key: 'instructions', get label() { return translate('integrationMeta.bankTransfer.fieldInstructions') }, type: IntegrationFieldType.Textarea },
    ],
  },
  cpanel: {
    slug: 'cpanel',
    color: 'bg-orange-600',
    logo: logoUrl('cpanel.svg'),
    get shortDescription() { return translate('integrationMeta.cpanel.shortDescription') },
    category: 'provisioning',
    fields: [
      { key: 'host', get label() { return translate('integrationMeta.cpanel.fieldHost') }, type: IntegrationFieldType.Text },
      { key: 'port', get label() { return translate('integrationMeta.cpanel.fieldPort') }, type: IntegrationFieldType.Text },
      { key: 'username', get label() { return translate('shared.username') }, type: IntegrationFieldType.Text },
      { key: 'apiToken', get label() { return translate('integrationMeta.cpanel.fieldApiToken') }, type: IntegrationFieldType.Password },
    ],
  },
  smtp: {
    slug: 'smtp',
    color: 'bg-teal-700',
    logo: logoUrl('smtp.svg'),
    get shortDescription() { return translate('integrationMeta.smtp.shortDescription') },
    category: 'email',
    fields: [
      { key: 'host', get label() { return translate('integrationMeta.smtp.fieldHost') }, type: IntegrationFieldType.Text },
      { key: 'port', get label() { return translate('integrationMeta.cpanel.fieldPort') }, type: IntegrationFieldType.Text },
      { key: 'username', get label() { return translate('shared.username') }, type: IntegrationFieldType.Text },
      { key: 'password', get label() { return translate('shared.password') }, type: IntegrationFieldType.Password },
      { key: 'fromAddress', get label() { return translate('integrationMeta.smtp.fieldFromAddress') }, type: IntegrationFieldType.Text },
      { key: 'encryption', get label() { return translate('integrationMeta.smtp.fieldEncryption') }, type: IntegrationFieldType.Select, options: [IntegrationEncryption.Tls, IntegrationEncryption.Ssl, IntegrationEncryption.None] },
    ],
  },
  maxmind: {
    slug: 'maxmind',
    color: 'bg-red-800',
    logo: logoUrl('maxmind.svg'),
    get shortDescription() { return translate('integrationMeta.maxmind.shortDescription') },
    category: 'fraud',
    fields: [
      { key: 'accountId', get label() { return translate('integrationMeta.maxmind.fieldAccountId') }, type: IntegrationFieldType.Text },
      { key: 'licenseKey', get label() { return translate('integrationMeta.maxmind.fieldLicenseKey') }, type: IntegrationFieldType.Password },
    ],
  },
}

/**
 * Ordered category definitions: the order of the inner menu and of the layout skeleton.
 */
export const INTEGRATION_CATEGORIES: IntegrationCategoryMeta[] = [
  {
    key: IntegrationCategory.Payments,
    labelKey: 'integrations.categories.payments',
    menuLabelKey: 'integrations.categoryMenu.payments',
    descriptionKey: 'integrations.categoryEmpty.payments',
    icon: Icon.CreditCard,
    registryCategory: PluginCategory.Payment,
  },
  {
    key: IntegrationCategory.Provisioning,
    labelKey: 'integrations.categories.provisioning',
    menuLabelKey: 'integrations.categoryMenu.provisioning',
    descriptionKey: 'integrations.categoryEmpty.provisioning',
    icon: Icon.Server,
    registryCategory: PluginCategory.Provisioning,
  },
  {
    key: IntegrationCategory.Registrars,
    labelKey: 'integrations.categories.registrars',
    menuLabelKey: 'integrations.categoryMenu.registrars',
    descriptionKey: 'integrations.categoryEmpty.registrars',
    icon: Icon.Globe,
    registryCategory: PluginCategory.Registrar,
  },
  {
    key: IntegrationCategory.Email,
    labelKey: 'integrations.categories.email',
    menuLabelKey: 'integrations.categoryMenu.email',
    descriptionKey: 'integrations.categoryEmpty.email',
    icon: Icon.Mail,
  },
  {
    key: IntegrationCategory.Fulfilment,
    labelKey: 'integrations.categories.fulfilment',
    menuLabelKey: 'integrations.categoryMenu.fulfilment',
    descriptionKey: 'integrations.categoryEmpty.fulfilment',
    icon: Icon.Box,
  },
  {
    key: IntegrationCategory.Fraud,
    labelKey: 'integrations.categories.fraud',
    menuLabelKey: 'integrations.categoryMenu.fraud',
    descriptionKey: 'integrations.categoryEmpty.fraud',
    icon: Icon.Shield,
  },
  {
    key: IntegrationCategory.Identity,
    labelKey: 'integrations.categories.identity',
    menuLabelKey: 'integrations.categoryMenu.identity',
    descriptionKey: 'integrations.categoryEmpty.identity',
    icon: Icon.User,
  },
  {
    key: IntegrationCategory.Hosted,
    labelKey: 'integrations.categories.hosted',
    menuLabelKey: 'integrations.categoryMenu.hosted',
    descriptionKey: 'integrations.categoryEmpty.hosted',
    icon: Icon.Activity,
  },
]

/**
 * The category entries by key, for a caller that holds a key and needs the entry (the menu, the page).
 */
export const INTEGRATION_CATEGORY_BY_KEY = Object.fromEntries(
  INTEGRATION_CATEGORIES.map(category => [category.key, category]),
) as Record<IntegrationCategory, IntegrationCategoryMeta>
