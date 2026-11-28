/**
 * Display metadata of the integrations screen: the category sections and the catalogue of built-in integrations
 * (name, logo, description and the wording of their settings fields) the admin renders.
 */

import { IntegrationCategory, IntegrationEncryption, IntegrationFieldKey } from '@/types/integration'
import type { IntegrationFieldMeta, IntegrationMeta, IntegrationSlug } from '@/types/integration'
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
 * Keyed by slug for O(1) lookup. This is the one place that words a built-in integration: the API sends
 * no name, field label or option label for it, and the store resolves them here from the slug and the field key.
 */
export const INTEGRATION_META: Record<IntegrationSlug, IntegrationMeta> = {
  'bank-transfer': {
    slug: 'bank-transfer',
    labelKey: 'integrationMeta.bankTransfer.name',
    color: 'bg-green-600',
    logo: logoUrl('bank-transfer.svg'),
    get shortDescription() { return translate('integrationMeta.bankTransfer.shortDescription') },
    category: 'payments',
    fields: [
      { key: IntegrationFieldKey.AccountName, labelKey: 'integrationMeta.bankTransfer.fieldAccountName' },
      { key: IntegrationFieldKey.Iban, labelKey: 'integrationMeta.bankTransfer.fieldIban' },
      { key: IntegrationFieldKey.Swift, labelKey: 'integrationMeta.bankTransfer.fieldSwift' },
      { key: IntegrationFieldKey.BankName, labelKey: 'reports.directDebit.bankName' },
      { key: IntegrationFieldKey.Instructions, labelKey: 'integrationMeta.bankTransfer.fieldInstructions' },
      { key: IntegrationFieldKey.ExpiryDays, labelKey: 'integrationMeta.bankTransfer.fieldExpiryDays' },
    ],
  },
  cpanel: {
    slug: 'cpanel',
    labelKey: 'integrationMeta.cpanel.name',
    color: 'bg-orange-600',
    logo: logoUrl('cpanel.svg'),
    get shortDescription() { return translate('integrationMeta.cpanel.shortDescription') },
    category: 'provisioning',
    fields: [
      { key: IntegrationFieldKey.Host, labelKey: 'integrationMeta.cpanel.fieldHost' },
      { key: IntegrationFieldKey.Port, labelKey: 'integrationMeta.cpanel.fieldPort' },
      { key: IntegrationFieldKey.Username, labelKey: 'shared.username' },
      { key: IntegrationFieldKey.ApiToken, labelKey: 'integrationMeta.cpanel.fieldApiToken' },
    ],
  },
  smtp: {
    slug: 'smtp',
    labelKey: 'integrationMeta.smtp.name',
    color: 'bg-teal-700',
    logo: logoUrl('smtp.svg'),
    get shortDescription() { return translate('integrationMeta.smtp.shortDescription') },
    category: 'email',
    fields: [
      { key: IntegrationFieldKey.Host, labelKey: 'integrationMeta.smtp.fieldHost' },
      { key: IntegrationFieldKey.Port, labelKey: 'integrationMeta.cpanel.fieldPort' },
      { key: IntegrationFieldKey.Username, labelKey: 'shared.username' },
      { key: IntegrationFieldKey.Password, labelKey: 'shared.password' },
      { key: IntegrationFieldKey.FromAddress, labelKey: 'integrationMeta.smtp.fieldFromAddress' },
      {
        key: IntegrationFieldKey.Encryption,
        labelKey: 'integrationMeta.smtp.fieldEncryption',
        options: [{ value: IntegrationEncryption.None, labelKey: 'integrationMeta.smtp.encryptionNone' }],
      },
    ],
  },
  maxmind: {
    slug: 'maxmind',
    labelKey: 'integrationMeta.maxmind.name',
    color: 'bg-red-800',
    logo: logoUrl('maxmind.svg'),
    get shortDescription() { return translate('integrationMeta.maxmind.shortDescription') },
    category: 'fraud',
    fields: [
      { key: IntegrationFieldKey.AccountId, labelKey: 'integrationMeta.maxmind.fieldAccountId' },
      { key: IntegrationFieldKey.LicenseKey, labelKey: 'integrationMeta.maxmind.fieldLicenseKey' },
    ],
  },
}

/**
 * Finds the catalogue entry of a built-in integration.
 *
 * @param slug - Slug of any integration; a plugin's slug is not in the catalogue.
 * @returns The entry, or undefined when the slug is not a built-in integration.
 */
const builtInMetaOf = (slug: string): IntegrationMeta | undefined =>
  (INTEGRATION_META as Record<string, IntegrationMeta | undefined>)[slug]

/**
 * Finds the wording of one field of a built-in integration.
 *
 * @param slug - Slug of the integration.
 * @param key - The field's key, as the API sends it.
 * @returns The field's wording, or undefined when the integration is not built in or declares no wording for that key.
 */
const fieldMetaOf = (slug: string, key: string): IntegrationFieldMeta | undefined =>
  builtInMetaOf(slug)?.fields.find(field => field.key === key)

/**
 * Words the name of a built-in integration in the operator's language.
 *
 * @param slug - Slug of the integration.
 * @returns The translated name; the slug itself when the integration is not built in.
 */
export const builtInIntegrationName = (slug: string): string => {
  const meta = builtInMetaOf(slug)
  return meta ? translate(meta.labelKey) : slug
}

/**
 * Words the label of a field of a built-in integration in the operator's language.
 *
 * @param slug - Slug of the integration.
 * @param key - The field's key, as the API sends it.
 * @returns The translated label; the key itself when the integration declares no label for it.
 */
export const builtInFieldLabel = (slug: string, key: string): string => {
  const meta = fieldMetaOf(slug, key)
  return meta ? translate(meta.labelKey) : key
}

/**
 * Words the options of a field of a built-in integration in the operator's language.
 *
 * @param slug - Slug of the integration.
 * @param key - The field's key, as the API sends it.
 * @returns Option value to translated text for the options that have their own wording; null when none has.
 */
export const builtInOptionLabels = (slug: string, key: string): Record<string, string> | null => {
  const options = fieldMetaOf(slug, key)?.options
  return options ? Object.fromEntries(options.map(option => [option.value, translate(option.labelKey)])) : null
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
