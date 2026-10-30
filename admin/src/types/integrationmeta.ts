/**
 * Display metadata of the integrations screen: the category sections and the catalogue of integrations
 * (label, logo and settings fields) the admin renders.
 */

import { IntegrationEncryption, IntegrationFieldType, IntegrationMode, IntegrationYesNo } from '@/types/integration'
import type { IntegrationMeta, IntegrationSlug, IntegrationCategory } from '@/types/integration'
import { PluginType } from '@/types/plugin'
import { translate } from '@/lib/translate'
import { publicAssetUrl } from '@/lib/runtimeConfig'

/**
 * Metadata for a single integration category section.
 */
interface IntegrationCategoryMeta {
  /** Category key matching IntegrationCategory. */
  key: IntegrationCategory
  /** Human-readable label for the section header. */
  label: string
  /** Emoji icon for the section header. */
  icon: string
}

/**
 * Builds the public URL of an integration logo.
 *
 * Built from the runtime base path (`config.js`) rather than written root-absolute, because the
 * app can be served under a sub-path such as `/admin` and the image is not rebuilt per deployment.
 *
 * @param file - File name inside `public/integrations/`.
 * @returns The URL to use as an `<img>` source.
 */
const logoUrl = (file: string): string =>
  publicAssetUrl(`integrations/${file}`)

/**
 * Static metadata for all supported integrations.
 * Keyed by slug for O(1) lookup.
 */
export const INTEGRATION_META: Record<IntegrationSlug, IntegrationMeta> = {
  stripe: {
    slug: 'stripe',
    color: 'bg-[#635bff]',
    logo: logoUrl('stripe.svg'),
    get shortDescription() { return translate('integrationMeta.stripe.shortDescription') },
    category: 'payments',
    fields: [
      { key: 'secretKey', get label() { return translate('integrationMeta.stripe.fieldSecretKey') }, type: IntegrationFieldType.Password },
      { key: 'publishableKey', get label() { return translate('integrationMeta.stripe.fieldPublishableKey') }, type: IntegrationFieldType.Text },
      { key: 'mode', get label() { return translate('integrationMeta.stripe.fieldMode') }, type: IntegrationFieldType.Select, options: [IntegrationMode.Live, IntegrationMode.Test] },
    ],
  },
  paypal: {
    slug: 'paypal',
    color: 'bg-[#009cde]',
    logo: logoUrl('paypal.svg'),
    get shortDescription() { return translate('integrationMeta.paypal.shortDescription') },
    category: 'payments',
    fields: [
      { key: 'clientId', get label() { return translate('reports.creditsReviewer.clientId') }, type: IntegrationFieldType.Text },
      { key: 'clientSecret', get label() { return translate('integrationMeta.paypal.fieldClientSecret') }, type: IntegrationFieldType.Password },
      { key: 'mode', get label() { return translate('integrationMeta.stripe.fieldMode') }, type: IntegrationFieldType.Select, options: [IntegrationMode.Live, IntegrationMode.Sandbox] },
    ],
  },
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
  // Slug is the plugin id from its plugin.json, not a short name: the backend keys plugin
  // integrations by that id, and a mismatch here silently falls back to a blank grey tile.
  'inecobank': {
    slug: 'inecobank',
    color: 'bg-[#006038]',
    logo: logoUrl('inecobank.svg'),
    get shortDescription() { return translate('integrationMeta.inecobank.shortDescription') },
    category: 'payments',
    fields: [
      { key: 'gateway_url', get label() { return translate('integrationMeta.inecobank.fieldGateway_url') }, type: IntegrationFieldType.Text },
      { key: 'username', get label() { return translate('integrationMeta.inecobank.fieldUsername') }, type: IntegrationFieldType.Text },
      { key: 'password', get label() { return translate('integrationMeta.inecobank.fieldPassword') }, type: IntegrationFieldType.Password },
      { key: 'currency', get label() { return translate('integrationMeta.inecobank.fieldCurrency') }, type: IntegrationFieldType.Text },
      { key: 'language', get label() { return translate('integrationMeta.inecobank.fieldLanguage') }, type: IntegrationFieldType.Text },
    ],
  },
  namecheap: {
    slug: 'namecheap',
    color: 'bg-amber-600',
    logo: logoUrl('namecheap.svg'),
    get shortDescription() { return translate('integrationMeta.namecheap.shortDescription') },
    category: 'registrars',
    fields: [
      { key: 'apiKey', get label() { return translate('integrationMeta.namecheap.fieldApiKey') }, type: IntegrationFieldType.Password },
      { key: 'apiUsername', get label() { return translate('integrationMeta.inecobank.fieldUsername') }, type: IntegrationFieldType.Text },
      { key: 'clientIp', get label() { return translate('integrationMeta.namecheap.fieldClientIp') }, type: IntegrationFieldType.Text },
    ],
  },
  nameam: {
    slug: 'nameam',
    color: 'bg-blue-800',
    logo: logoUrl('nameam.svg'),
    get shortDescription() { return translate('integrationMeta.nameam.shortDescription') },
    category: 'registrars',
    fields: [
      { key: 'email', get label() { return translate('integrationMeta.nameam.fieldEmail') }, type: IntegrationFieldType.Text },
      { key: 'password', get label() { return translate('integrationMeta.nameam.fieldPassword') }, type: IntegrationFieldType.Password },
      { key: 'apiUrl', get label() { return translate('integrationMeta.nameam.fieldApiUrl') }, type: IntegrationFieldType.Text },
      { key: 'test_mode', get label() { return translate('integrationMeta.nameam.fieldTest_mode') }, type: IntegrationFieldType.Toggle },
    ],
  },
  resellerclub: {
    slug: 'resellerclub',
    color: 'bg-sky-500',
    logo: logoUrl('resellerclub.svg'),
    get shortDescription() { return translate('integrationMeta.resellerclub.shortDescription') },
    category: 'registrars',
    fields: [
      { key: 'resellerId', get label() { return translate('integrationMeta.resellerclub.fieldResellerId') }, type: IntegrationFieldType.Text },
      { key: 'apiKey', get label() { return translate('integrationMeta.namecheap.fieldApiKey') }, type: IntegrationFieldType.Password },
    ],
  },
  enom: {
    slug: 'enom',
    color: 'bg-violet-700',
    logo: logoUrl('enom.svg'),
    get shortDescription() { return translate('integrationMeta.enom.shortDescription') },
    category: 'registrars',
    fields: [
      { key: 'accountId', get label() { return translate('integrationMeta.enom.fieldAccountId') }, type: IntegrationFieldType.Text },
      { key: 'apiKey', get label() { return translate('integrationMeta.namecheap.fieldApiKey') }, type: IntegrationFieldType.Password },
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
  plesk: {
    slug: 'plesk',
    color: 'bg-blue-700',
    logo: logoUrl('plesk.svg'),
    get shortDescription() { return translate('integrationMeta.plesk.shortDescription') },
    category: 'provisioning',
    fields: [
      { key: 'host', get label() { return translate('integrationMeta.plesk.fieldHost') }, type: IntegrationFieldType.Text },
      { key: 'port', get label() { return translate('integrationMeta.cpanel.fieldPort') }, type: IntegrationFieldType.Text },
      { key: 'username', get label() { return translate('shared.username') }, type: IntegrationFieldType.Text },
      { key: 'password', get label() { return translate('shared.password') }, type: IntegrationFieldType.Password },
    ],
  },
  cwp: {
    slug: 'cwp',
    color: 'bg-sky-700',
    logo: logoUrl('cwp.svg'),
    get shortDescription() { return translate('integrationMeta.cwp.shortDescription') },
    category: 'provisioning',
    fields: [
      { key: 'host', get label() { return translate('integrationMeta.cwp.fieldHost') }, type: IntegrationFieldType.Text },
      { key: 'port', get label() { return translate('integrationMeta.cpanel.fieldPort') }, type: IntegrationFieldType.Text },
      { key: 'api_key', get label() { return translate('integrationMeta.namecheap.fieldApiKey') }, type: IntegrationFieldType.Password },
    ],
  },
  cwp7: {
    slug: 'cwp7',
    color: 'bg-sky-800',
    logo: logoUrl('cwp7.svg'),
    get shortDescription() { return translate('integrationMeta.cwp7.shortDescription') },
    category: 'provisioning',
    fields: [
      { key: 'server_name', get label() { return translate('servers.servers.serverName') }, type: IntegrationFieldType.Text },
      { key: 'hostname', get label() { return translate('servers.serverForm.hostname') }, type: IntegrationFieldType.Text },
      { key: 'ip_address', get label() { return translate('shared.ipAddress') }, type: IntegrationFieldType.Text },
      { key: 'assigned_ips', get label() { return translate('servers.serverForm.assignedIpAddresses') }, type: IntegrationFieldType.Textarea },
      { key: 'monthly_cost', get label() { return translate('integrationMeta.cwp7.fieldMonthly_cost') }, type: IntegrationFieldType.Text },
      { key: 'datacenter', get label() { return translate('integrationMeta.cwp7.fieldDatacenter') }, type: IntegrationFieldType.Text },
      { key: 'username', get label() { return translate('shared.username') }, type: IntegrationFieldType.Text },
      { key: 'password', get label() { return translate('shared.password') }, type: IntegrationFieldType.Password },
      { key: 'access_hash', get label() { return translate('integrationMeta.cwp7.fieldAccess_hash') }, type: IntegrationFieldType.Password },
      { key: 'port', get label() { return translate('integrationMeta.cwp7.fieldPort') }, type: IntegrationFieldType.Text },
      { key: 'max_accounts', get label() { return translate('integrationMeta.cwp7.fieldMax_accounts') }, type: IntegrationFieldType.Text },
      { key: 'server_status_address', get label() { return translate('servers.serverForm.serverStatusAddress') }, type: IntegrationFieldType.Text },
      { key: 'ns1', get label() { return translate('integrationMeta.cwp7.fieldNs1') }, type: IntegrationFieldType.Text },
      { key: 'ns1_ip', get label() { return translate('integrationMeta.cwp7.fieldNs1_ip') }, type: IntegrationFieldType.Text },
      { key: 'ns2', get label() { return translate('integrationMeta.cwp7.fieldNs2') }, type: IntegrationFieldType.Text },
      { key: 'ns2_ip', get label() { return translate('integrationMeta.cwp7.fieldNs2_ip') }, type: IntegrationFieldType.Text },
      { key: 'secure', get label() { return translate('integrationMeta.cwp7.fieldSecure') }, type: IntegrationFieldType.Select, options: [IntegrationYesNo.Yes, IntegrationYesNo.No] },
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
      { key: 'accountId', get label() { return translate('integrationMeta.enom.fieldAccountId') }, type: IntegrationFieldType.Text },
      { key: 'licenseKey', get label() { return translate('integrationMeta.maxmind.fieldLicenseKey') }, type: IntegrationFieldType.Password },
    ],
  },
}

/**
 * Ordered category definitions for rendering sections on the main page.
 */
export const INTEGRATION_CATEGORIES: IntegrationCategoryMeta[] = [
  { key: 'payments', get label() { return translate('nav.settingsChildren.gateways') }, icon: '💳' },
  { key: 'registrars', get label() { return translate('integrationMeta.maxmind.fieldRegistrars') }, icon: '🌐' },
  { key: 'provisioning', get label() { return translate('integrations.cwp7Integration.hostingProvisioning') }, icon: '🖥️' },
  { key: 'email', get label() { return translate('integrationMeta.maxmind.fieldEmail') }, icon: '📧' },
  { key: 'fraud', get label() { return translate('integrationMeta.maxmind.fieldFraud') }, icon: '🛡️' },
  { key: 'fulfilment', get label() { return translate('integrations.categories.fulfilment') }, icon: '📦' },
  { key: 'identity', get label() { return translate('integrations.categories.identity') }, icon: '🪪' },
  { key: 'hosted', get label() { return translate('integrations.categories.hosted') }, icon: '⚙️' },
]

/** The badge label of each plugin type, read in the active language. */
export const PLUGIN_TYPE_LABELS: Record<PluginType, string> = {
  get [PluginType.Provisioning]() { return translate('integrations.pluginType.provisioning') },
  get [PluginType.Payment]() { return translate('integrations.pluginType.payment') },
  get [PluginType.Registrar]() { return translate('integrations.pluginType.registrar') },
  get [PluginType.Identity]() { return translate('integrations.pluginType.identity') },
  get [PluginType.Fulfilment]() { return translate('integrations.pluginType.fulfilment') },
  get [PluginType.Hosted]() { return translate('integrations.pluginType.hosted') },
}
