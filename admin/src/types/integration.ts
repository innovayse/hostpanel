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
  BankTransfer: 'bank-transfer',
  Cpanel: 'cpanel',
  Smtp: 'smtp',
  Maxmind: 'maxmind',
} as const

/** All valid integration slugs. */
export type IntegrationSlug = (typeof IntegrationSlug)[keyof typeof IntegrationSlug]

/** Closed set of the ways an integration text is carried; it decides whether the text is translated when drawn. */
export enum IntegrationLabelKind {
  /** Finished text in the request language, shown as it is (a plugin's own wording). */
  Text = 'text',
  /** A locale key of the admin catalogue, translated when drawn so it follows a language switch. */
  Key = 'key',
}

/** One text of an integration screen: either finished text or a locale key. */
export type IntegrationLabel =
  | { kind: IntegrationLabelKind.Text; text: string }
  | { kind: IntegrationLabelKind.Key; key: string }

/** Summary of a single integration returned by GET /api/admin/integrations. */
export interface IntegrationDto {
  /** URL-safe slug used in routes and API calls. */
  slug: string
  /** Display name: the manifest name for a plugin, the catalogue's locale key for a built-in integration. */
  name: IntegrationLabel
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
  /** Address of the plugin's icon; null for a built-in integration or a plugin without one. */
  iconUrl: string | null
  /**
   * Last known connection state of a switched-on plugin that implements the status capability; null for a built-in
   * integration and every other plugin.
   */
  connection: IntegrationConnection | null
  /** The plugin's short description in the request language; null for a built-in integration (admin-owned text). */
  description: string | null
}

/** Closed set of connection states the API reports for a plugin integration; the strings are the API's enum names. */
export const IntegrationConnectionState = {
  /** The plugin reports that it works. */
  Connected: 'Connected',
  /** The plugin works with reduced function. */
  Degraded: 'Degraded',
  /** The plugin does not work, failed to answer or did not answer in time. */
  Unavailable: 'Unavailable',
  /** The plugin is switched off or not fully configured, so it was not asked. */
  Inactive: 'Inactive',
} as const

/** One of the {@link IntegrationConnectionState} values. */
export type IntegrationConnectionState = (typeof IntegrationConnectionState)[keyof typeof IntegrationConnectionState]

/** The connection state of a plugin integration, as the API words it in the reader's language. */
export interface IntegrationConnection {
  /** The state. */
  state: IntegrationConnectionState
  /** One sentence for the administrator, already in the request language. */
  message: string
  /** ISO 8601 UTC moment the plugin was asked; a cached state keeps the moment of its check. */
  checkedAt: string
}

/** Metadata for one configuration field, returned by the detail endpoint. */
export interface FieldDefinitionDto {
  /** Storage key used in the Settings table (e.g. "secret_key"). */
  key: string
  /** Label shown above the input: finished text for a plugin, the catalogue's locale key for a built-in integration. */
  label: IntegrationLabel
  /** Input kind of the field. */
  type: IntegrationFieldType
  /** Whether the field is required before the integration can be enabled. */
  required: boolean
  /** Allowed values for "select" type fields; absent or null for all others. */
  options?: string[] | null
  /** Lowest accepted value of a "Number" field; absent or null when unbounded. */
  min?: number | null
  /** Highest accepted value of a "Number" field; absent or null when unbounded. */
  max?: number | null
  /** Help text shown under the field, already in the request language; absent or null for none. */
  description?: string | null
  /** Hint shown in the empty input, already in the request language; absent or null for none. */
  placeholder?: string | null
  /** The text shown for each option value, keyed by the value; absent when an option shows its value. */
  optionLabels?: Record<string, IntegrationLabel>
}

/** A field definition as the API sends it: a built-in integration's label is null and its option labels may be null. */
export type FieldDefinitionWire = Omit<FieldDefinitionDto, 'label' | 'optionLabels'> & {
  label?: string | null
  optionLabels?: Record<string, string> | null
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
  /** Display name: the manifest name for a plugin, the catalogue's locale key for a built-in integration. */
  name: IntegrationLabel
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** True if this integration comes from an installed plugin (not built-in). */
  isPlugin: boolean
  /** Address of the plugin's icon; null or absent for a built-in integration or a plugin without one. */
  iconUrl?: string | null
  /** Field key to its stored state; a secret field answers only whether it is set. */
  config: Record<string, IntegrationFieldValue>
  /**
   * Ordered field definitions for dynamic form rendering.
   * Source of truth for which fields to show and their input types.
   */
  fieldDefinitions: FieldDefinitionDto[]
  /** Whether Test Connection can check this integration; the button is hidden when it cannot. */
  canTestConnection: boolean
  /** Last known connection state of a plugin that implements the status capability; null otherwise. */
  connection: IntegrationConnection | null
}

/** An integration summary as the API sends it: a built-in integration's name is null, the store turns it into a key. */
export type IntegrationWireDto = Omit<IntegrationDto, 'name'> & { name: string | null }

/** An integration detail as the API sends it: a built-in integration's name and field labels are null. */
export type IntegrationDetailWireDto = Omit<IntegrationDetailDto, 'name' | 'fieldDefinitions'> & {
  name: string | null
  fieldDefinitions: FieldDefinitionWire[]
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
 * Static metadata of a built-in integration: its card and the wording of its form.
 * Not fetched from the API: defined client-side.
 */
export interface IntegrationMeta {
  /** Matches IntegrationDto.slug. */
  slug: string
  /** Locale key of the integration's display name. */
  labelKey: string
  /** Tailwind background color class for the logo block (e.g. "bg-[#635bff]"). */
  color: string
  /** Path to the SVG logo in /public/integrations/ (e.g. "/integrations/<slug>.svg"). */
  logo: string
  /** Short description shown on the integration card. */
  shortDescription: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** The wording of the integration's form fields, in form order. */
  fields: IntegrationFieldMeta[]
  /** Optional hint shown in the status sidebar (e.g. webhook URL instructions). */
  hint?: string
}

/** The wording of one form field of a built-in integration. */
export interface IntegrationFieldMeta {
  /** The field's key, as the API sends it. */
  key: IntegrationFieldKey
  /** Locale key of the field's label. */
  labelKey: string
  /** Wording of the options that show text other than their value; absent when every option shows its value. */
  options?: IntegrationOptionMeta[]
}

/** The wording of one option of a select field. */
export interface IntegrationOptionMeta {
  /** The option's stored value. */
  value: string
  /** Locale key of the text shown for the option. */
  labelKey: string
}

/** Closed set of the field keys the API sends for the built-in integrations; the strings are the setting names. */
export enum IntegrationFieldKey {
  /** Bank transfer: account holder name. */
  AccountName = 'account_name',
  /** Bank transfer: IBAN. */
  Iban = 'iban',
  /** Bank transfer: SWIFT code. */
  Swift = 'swift',
  /** Bank transfer: bank name. */
  BankName = 'bank_name',
  /** Bank transfer: payment instructions. */
  Instructions = 'instructions',
  /** Bank transfer: days an invoice stays payable. */
  ExpiryDays = 'expiry_days',
  /** Host name of a server (cPanel, SMTP). */
  Host = 'host',
  /** Port of a server (cPanel, SMTP). */
  Port = 'port',
  /** Account user name (cPanel, SMTP). */
  Username = 'username',
  /** Account password (SMTP). */
  Password = 'password',
  /** cPanel API token. */
  ApiToken = 'api_token',
  /** SMTP sender address. */
  FromAddress = 'from_address',
  /** SMTP connection encryption. */
  Encryption = 'encryption',
  /** Fraud screening account id. */
  AccountId = 'account_id',
  /** Fraud screening license key. */
  LicenseKey = 'license_key',
}

/** Closed set of input kinds an integration configuration field can use; the strings are the API's enum names. */
export enum IntegrationFieldType {
  /** Plain text. */
  Text = 'Text',
  /** A secret from the built-in catalogue, write-only in the form. */
  Password = 'Password',
  /** A secret declared by a plugin manifest, write-only in the form. */
  Secret = 'Secret',
  /** One of a fixed list of options. */
  Select = 'Select',
  /** Multi-line text. */
  Textarea = 'TextArea',
  /** A yes/no switch. */
  Toggle = 'Toggle',
  /** A web address. */
  Url = 'Url',
  /** A whole or decimal number, optionally bounded by the definition's min and max. */
  Number = 'Number',
}

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
