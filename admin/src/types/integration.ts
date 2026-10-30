import type { PluginType } from '@/types/plugin'

/** Closed set of integration categories; the last three hold only plugins, grouped by their type. */
export const IntegrationCategory = {
  Payments: 'payments',
  Registrars: 'registrars',
  Provisioning: 'provisioning',
  Email: 'email',
  Fraud: 'fraud',
  /** Fulfilment plugins: deliver digital products after payment. */
  Fulfilment: 'fulfilment',
  /** Identity plugins: decide which client a signed-in person acts for. */
  Identity: 'identity',
  /** Hosted plugins: background workers. */
  Hosted: 'hosted',
} as const

/** Category grouping for an integration. */
export type IntegrationCategory = (typeof IntegrationCategory)[keyof typeof IntegrationCategory]

/** Closed set of the category names the API groups integrations under. */
export const IntegrationCategoryName = {
  /** Card and bank payment providers. */
  PaymentGateways: 'Payment Gateways',
  /** Domain registrars. */
  DomainRegistrars: 'Domain Registrars',
  /** Hosting and provisioning panels. */
  Provisioning: 'Hosting / Provisioning',
  /** Outgoing mail. */
  Email: 'Email / SMTP',
  /** Fraud screening. */
  FraudProtection: 'Fraud Protection',
} as const

/** One of the {@link IntegrationCategoryName} values. */
export type IntegrationCategoryName = (typeof IntegrationCategoryName)[keyof typeof IntegrationCategoryName]

/** Closed set of integration slugs. */
export const IntegrationSlug = {
  Stripe: 'stripe',
  Paypal: 'paypal',
  BankTransfer: 'bank-transfer',
  Inecobank: 'inecobank',
  Namecheap: 'namecheap',
  Nameam: 'nameam',
  Resellerclub: 'resellerclub',
  Enom: 'enom',
  Cpanel: 'cpanel',
  Plesk: 'plesk',
  Cwp: 'cwp',
  Cwp7: 'cwp7',
  Smtp: 'smtp',
  Maxmind: 'maxmind',
} as const

/** All valid integration slugs. */
export type IntegrationSlug = (typeof IntegrationSlug)[keyof typeof IntegrationSlug]

/** Summary of a single integration returned by GET /api/admin/integrations. */
export interface IntegrationDto {
  /** URL-safe slug used in routes and API calls. */
  slug: string
  /** Human-readable name, as the integration list shows it. */
  name: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** Whether every field the integration needs holds a stored value. */
  isConfigured: boolean
  /** True if this integration comes from an installed plugin (not built-in). */
  isPlugin: boolean
  /** The type a plugin's manifest declares; null for a built-in integration. */
  pluginType: PluginType | null
}

/** Metadata for one configuration field, returned by the detail endpoint. */
export interface FieldDefinitionDto {
  /** Storage key used in the Settings table (e.g. "secret_key"). */
  key: string
  /** Human-readable label shown above the input. */
  label: string
  /** Input kind of the field. */
  type: IntegrationFieldType
  /** Whether the field is required before the integration can be enabled. */
  required: boolean
  /** Allowed values for "select" type fields; absent for all others. */
  options?: string[]
}

/** The stored state of one integration field. */
export interface IntegrationFieldValue {
  /** The stored value; always null for a secret, whose value the API never sends. */
  value: string | null
  /** Whether the field holds a secret; it is then write only. */
  isSecret: boolean
  /** Whether a non-blank value is stored. */
  hasValue: boolean
}

/** Full config for a single integration returned by GET /api/admin/integrations/:slug. */
export interface IntegrationDetailDto {
  /** URL-safe slug used in routes and API calls. */
  slug: string
  /** Human-readable name from the built-in catalogue or the plugin manifest. */
  name: string
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** Field key to its stored state; a secret field answers only whether it is set. */
  config: Record<string, IntegrationFieldValue>
  /**
   * Ordered field definitions for dynamic form rendering.
   * Source of truth for which fields to show and their input types.
   */
  fieldDefinitions: FieldDefinitionDto[]
}

/** Payload sent to PUT /api/admin/integrations/:slug. */
export interface IntegrationConfigPayload {
  /** Whether the integration should be enabled after save. */
  isEnabled: boolean
  /**
   * Field values to store. A secret field is sent only when the admin typed a replacement; an
   * empty or absent secret keeps the stored value.
   */
  config: Record<string, string>
  /** Field keys whose stored value is removed (the explicit Clear of a secret). */
  clearFields?: string[]
}

/** Result returned by POST /api/admin/integrations/:slug/test. */
export interface IntegrationTestResult {
  /** Whether the connection test succeeded. */
  success: boolean
  /** Human-readable message (error detail or "Connection OK"). */
  message: string
  /** ISO 8601 UTC timestamp of when the backend ran the test. */
  testedAt?: string
  /**
   * Round-trip time of the test as seen from the browser, in milliseconds. Measured here rather
   * than on the server because it is the number an admin can act on: it includes the hop to our
   * API and the API's hop to the provider, which is the whole path a real payment will take.
   */
  durationMs?: number
}

/**
 * Static metadata for rendering each integration card.
 * Not fetched from the API: defined client-side.
 */
export interface IntegrationMeta {
  /** Matches IntegrationDto.slug. */
  slug: string
  /** Tailwind background color class for the logo block (e.g. "bg-[#635bff]"). */
  color: string
  /** Path to the SVG logo in /public/integrations/ (e.g. "/integrations/<slug>.svg"). */
  logo: string
  /** Short description shown on the integration card. */
  shortDescription: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Ordered list of config field keys and their labels. */
  fields: IntegrationField[]
  /** Optional hint shown in the status sidebar (e.g. webhook URL instructions). */
  hint?: string
}

/** A single configurable field for an integration. */
export interface IntegrationField {
  /** Field key matching the config Record key. */
  key: string
  /** Human-readable label. */
  label: string
  /** Input type: "text" for most, "password" for secrets, "select" for enums, "toggle" for booleans. */
  type: IntegrationFieldType
  /** Options for "select" type fields. */
  options?: string[]
}

/** Closed set of input kinds an integration configuration field can use; the strings are the API's enum names. */
export const IntegrationFieldType = {
  /** Plain text. */
  Text: 'Text',
  /** A secret from the built-in catalogue, write-only in the form. */
  Password: 'Password',
  /** A secret declared by a plugin manifest, write-only in the form. */
  Secret: 'Secret',
  /** One of a fixed list of options. */
  Select: 'Select',
  /** Multi-line text. */
  Textarea: 'TextArea',
  /** A yes/no switch. */
  Toggle: 'Toggle',
} as const

/** One of the {@link IntegrationFieldType} values. */
export type IntegrationFieldType = (typeof IntegrationFieldType)[keyof typeof IntegrationFieldType]

/** Closed set of environments a payment integration runs against; the strings are what the API stores. */
export const IntegrationMode = {
  /** Real charges. */
  Live: 'Live',
  /** The provider's test environment. */
  Test: 'Test',
  /** The provider's sandbox environment. */
  Sandbox: 'Sandbox',
} as const

/** One of the {@link IntegrationMode} values. */
export type IntegrationMode = (typeof IntegrationMode)[keyof typeof IntegrationMode]

/** Closed set of the answers of a yes/no integration field; the strings are what the API stores. */
export const IntegrationYesNo = {
  /** The option is on. */
  Yes: 'Yes',
  /** The option is off. */
  No: 'No',
} as const

/** One of the {@link IntegrationYesNo} values. */
export type IntegrationYesNo = (typeof IntegrationYesNo)[keyof typeof IntegrationYesNo]

/** Closed set of mail transport encryptions; the strings are what the API stores. */
export const IntegrationEncryption = {
  /** Upgrade the connection with STARTTLS. */
  Tls: 'TLS',
  /** Encrypt from the first byte. */
  Ssl: 'SSL',
  /** Send in the clear. */
  None: 'None',
} as const

/** One of the {@link IntegrationEncryption} values. */
export type IntegrationEncryption = (typeof IntegrationEncryption)[keyof typeof IntegrationEncryption]
