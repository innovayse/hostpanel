import type { IntegrationMeta, IntegrationSlug, IntegrationCategory } from './integration.types'

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
 * Static metadata for all supported integrations.
 * Keyed by slug for O(1) lookup.
 */
export const INTEGRATION_META: Record<IntegrationSlug, IntegrationMeta> = {
  stripe: {
    slug: 'stripe',
    color: 'bg-[#635bff]',
    category: 'payments',
    hint: 'Add this webhook endpoint in your Stripe dashboard: https://yourdomain.com/api/webhooks/stripe',
    fields: [
      { key: 'secretKey', label: 'Secret Key', type: 'password' },
      { key: 'publishableKey', label: 'Publishable Key', type: 'text' },
      { key: 'webhookSecret', label: 'Webhook Secret', type: 'password' },
      { key: 'mode', label: 'Mode', type: 'select', options: ['Live', 'Test'] },
    ],
  },
  paypal: {
    slug: 'paypal',
    color: 'bg-[#009cde]',
    category: 'payments',
    fields: [
      { key: 'clientId', label: 'Client ID', type: 'text' },
      { key: 'clientSecret', label: 'Client Secret', type: 'password' },
      { key: 'mode', label: 'Mode', type: 'select', options: ['Live', 'Sandbox'] },
    ],
  },
  'bank-transfer': {
    slug: 'bank-transfer',
    color: 'bg-green-600',
    category: 'payments',
    fields: [
      { key: 'accountName', label: 'Account Name', type: 'text' },
      { key: 'iban', label: 'IBAN', type: 'text' },
      { key: 'bankName', label: 'Bank Name', type: 'text' },
      { key: 'instructions', label: 'Payment Instructions', type: 'textarea' },
    ],
  },
  namecheap: {
    slug: 'namecheap',
    color: 'bg-amber-600',
    category: 'registrars',
    fields: [
      { key: 'apiKey', label: 'API Key', type: 'password' },
      { key: 'apiUsername', label: 'API Username', type: 'text' },
      { key: 'clientIp', label: 'Whitelisted Client IP', type: 'text' },
    ],
  },
  resellerclub: {
    slug: 'resellerclub',
    color: 'bg-sky-500',
    category: 'registrars',
    fields: [
      { key: 'resellerId', label: 'Reseller ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  enom: {
    slug: 'enom',
    color: 'bg-violet-700',
    category: 'registrars',
    fields: [
      { key: 'accountId', label: 'Account ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  cpanel: {
    slug: 'cpanel',
    color: 'bg-orange-600',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'WHM Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'apiToken', label: 'API Token', type: 'password' },
    ],
  },
  plesk: {
    slug: 'plesk',
    color: 'bg-blue-700',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'Plesk Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'password' },
    ],
  },
  cwp: {
    slug: 'cwp',
    color: 'bg-sky-700',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'CWP Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'api_key', label: 'API Key', type: 'password' },
    ],
  },
  smtp: {
    slug: 'smtp',
    color: 'bg-teal-700',
    category: 'email',
    fields: [
      { key: 'host', label: 'SMTP Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'password' },
      { key: 'fromAddress', label: 'From Address', type: 'text' },
      { key: 'encryption', label: 'Encryption', type: 'select', options: ['TLS', 'SSL', 'None'] },
    ],
  },
  maxmind: {
    slug: 'maxmind',
    color: 'bg-red-800',
    category: 'fraud',
    fields: [
      { key: 'accountId', label: 'Account ID', type: 'text' },
      { key: 'licenseKey', label: 'License Key', type: 'password' },
    ],
  },
}

/**
 * Ordered category definitions for rendering sections on the main page.
 */
export const INTEGRATION_CATEGORIES: IntegrationCategoryMeta[] = [
  { key: 'payments', label: 'Payment Gateways', icon: '💳' },
  { key: 'registrars', label: 'Domain Registrars', icon: '🌐' },
  { key: 'provisioning', label: 'Hosting / Provisioning', icon: '🖥️' },
  { key: 'email', label: 'Email / SMTP', icon: '📧' },
  { key: 'fraud', label: 'Fraud Protection', icon: '🛡️' },
]
