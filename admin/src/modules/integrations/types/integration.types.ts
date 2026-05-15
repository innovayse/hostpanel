/** Category grouping for an integration. */
export type IntegrationCategory =
  | 'payments'
  | 'registrars'
  | 'provisioning'
  | 'email'
  | 'fraud'

/** All valid integration slugs. */
export type IntegrationSlug =
  | 'stripe'
  | 'paypal'
  | 'bank-transfer'
  | 'namecheap'
  | 'resellerclub'
  | 'enom'
  | 'cpanel'
  | 'plesk'
  | 'cwp'
  | 'smtp'
  | 'maxmind'

/** Summary of a single integration returned by GET /api/admin/integrations. */
export interface IntegrationDto {
  /** URL-safe slug used in routes and API calls (e.g. "stripe", "cpanel"). */
  slug: string
  /** Human-readable name (e.g. "Stripe"). */
  name: string
  /** Short description shown in the integration row. */
  description: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** ISO 8601 timestamp of the last successful connection test, or null. */
  lastTestedAt: string | null
  /** True if this integration comes from an installed plugin (not built-in). */
  isPlugin?: boolean
}

/** Metadata for one configuration field, returned by the detail endpoint. */
export interface FieldDefinitionDto {
  /** Storage key used in the Settings table (e.g. "secret_key"). */
  key: string
  /** Human-readable label shown above the input. */
  label: string
  /** Input type — "text", "password", "select", or "textarea". */
  type: 'text' | 'password' | 'select' | 'textarea'
  /** Whether the field is required before the integration can be enabled. */
  required: boolean
  /** Allowed values for "select" type fields; absent for all others. */
  options?: string[]
}

/** Full config for a single integration returned by GET /api/admin/integrations/:slug. */
export interface IntegrationDetailDto extends IntegrationDto {
  /**
   * Key-value pairs of configuration fields.
   * Secret values are masked (e.g. "sk_live_••••••••").
   */
  config: Record<string, string>
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
  /** Full (unmasked) config values the admin has edited. */
  config: Record<string, string>
}

/** Result returned by POST /api/admin/integrations/:slug/test. */
export interface IntegrationTestResult {
  /** Whether the connection test succeeded. */
  success: boolean
  /** Human-readable message (error detail or "Connection OK"). */
  message: string
}

/**
 * Static metadata for rendering each integration card.
 * Not fetched from the API — defined client-side.
 */
export interface IntegrationMeta {
  /** Matches IntegrationDto.slug. */
  slug: string
  /** Tailwind background color class for the logo block (e.g. "bg-[#635bff]"). */
  color: string
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
  /** Input type — "text" for most, "password" for secrets, "select" for enums. */
  type: 'text' | 'password' | 'select' | 'textarea'
  /** Options for "select" type fields. */
  options?: string[]
}
