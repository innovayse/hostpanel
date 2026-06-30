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
    logo: '/integrations/stripe.svg',
    shortDescription: 'Accept credit card payments online with Stripe\'s payment processing platform.',
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
    logo: '/integrations/paypal.svg',
    shortDescription: 'Enable PayPal checkout for clients who prefer PayPal payments.',
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
    logo: '/integrations/bank-transfer.svg',
    shortDescription: 'Allow clients to pay via direct bank transfer with your banking details.',
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
    logo: '/integrations/namecheap.svg',
    shortDescription: 'Register and manage domains through Namecheap\'s reseller API.',
    category: 'registrars',
    fields: [
      { key: 'apiKey', label: 'API Key', type: 'password' },
      { key: 'apiUsername', label: 'API Username', type: 'text' },
      { key: 'clientIp', label: 'Whitelisted Client IP', type: 'text' },
    ],
  },
  nameam: {
    slug: 'nameam',
    color: 'bg-blue-800',
    logo: '/integrations/nameam.svg',
    shortDescription: 'Register and manage .am domains through the Name.am registrar API.',
    category: 'registrars',
    fields: [
      { key: 'email', label: 'Account Email', type: 'text' },
      { key: 'password', label: 'Account Password', type: 'password' },
      { key: 'apiUrl', label: 'API URL', type: 'text' },
      { key: 'test_mode', label: 'Test Mode', type: 'toggle' },
    ],
  },
  resellerclub: {
    slug: 'resellerclub',
    color: 'bg-sky-500',
    logo: '/integrations/resellerclub.svg',
    shortDescription: 'Manage domain registrations via ResellerClub\'s reseller platform.',
    category: 'registrars',
    fields: [
      { key: 'resellerId', label: 'Reseller ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  enom: {
    slug: 'enom',
    color: 'bg-violet-700',
    logo: '/integrations/enom.svg',
    shortDescription: 'Connect to eNom for domain registration and DNS management.',
    category: 'registrars',
    fields: [
      { key: 'accountId', label: 'Account ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  cpanel: {
    slug: 'cpanel',
    color: 'bg-orange-600',
    logo: '/integrations/cpanel.svg',
    shortDescription: 'Provision and manage hosting accounts via cPanel WHM server API.',
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
    logo: '/integrations/plesk.svg',
    shortDescription: 'Automate server and hosting management through Plesk\'s XML API.',
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
    logo: '/integrations/cwp.svg',
    shortDescription: 'Manage hosting accounts on CentOS Web Panel servers.',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'CWP Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'api_key', label: 'API Key', type: 'password' },
    ],
  },
  cwp7: {
    slug: 'cwp7',
    color: 'bg-sky-800',
    logo: '/integrations/cwp7.svg',
    shortDescription: 'Provision and manage hosting accounts via Control Web Panel 7 API.',
    category: 'provisioning',
    fields: [
      { key: 'server_name', label: 'Server Name', type: 'text' },
      { key: 'hostname', label: 'Hostname', type: 'text' },
      { key: 'ip_address', label: 'IP Address', type: 'text' },
      { key: 'assigned_ips', label: 'Assigned IP Addresses', type: 'textarea' },
      { key: 'monthly_cost', label: 'Monthly Cost', type: 'text' },
      { key: 'datacenter', label: 'Datacenter/NOC', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'password' },
      { key: 'access_hash', label: 'Access Hash', type: 'password' },
      { key: 'port', label: 'API Port', type: 'text' },
      { key: 'max_accounts', label: 'Maximum No. of Accounts', type: 'text' },
      { key: 'server_status_address', label: 'Server Status Address', type: 'text' },
      { key: 'ns1', label: 'Primary Nameserver', type: 'text' },
      { key: 'ns1_ip', label: 'Primary Nameserver IP', type: 'text' },
      { key: 'ns2', label: 'Secondary Nameserver', type: 'text' },
      { key: 'ns2_ip', label: 'Secondary Nameserver IP', type: 'text' },
      { key: 'secure', label: 'Use SSL for Connections', type: 'select', options: ['Yes', 'No'] },
    ],
  },
  smtp: {
    slug: 'smtp',
    color: 'bg-teal-700',
    logo: '/integrations/smtp.svg',
    shortDescription: 'Configure outgoing email delivery via your SMTP mail server.',
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
    logo: '/integrations/maxmind.svg',
    shortDescription: 'Detect fraudulent orders with MaxMind\'s GeoIP risk scoring.',
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
