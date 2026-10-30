/** Closed set of plugin types a manifest declares; the strings are the API's enum names. */
export const PluginType = {
  /** Hosting and server provisioning through a hosting panel. */
  Provisioning: 'Provisioning',
  /** Payment gateway. */
  Payment: 'Payment',
  /** Domain registrar. */
  Registrar: 'Registrar',
  /** Decides which client a signed-in person acts for; at most one is enabled. */
  Identity: 'Identity',
  /** Delivers digital products after an invoice is paid. */
  Fulfilment: 'Fulfilment',
  /** A background worker the server starts and stops with itself. */
  Hosted: 'Hosted',
} as const

/** One of the {@link PluginType} values. */
export type PluginType = (typeof PluginType)[keyof typeof PluginType]

/** Summary of one installed plugin returned by GET /api/admin/plugins. */
export interface PluginListItemDto {
  /** Unique plugin identifier, the `id` field of plugin.json. */
  id: string
  /** Human-readable display name. */
  name: string
  /** Semver version string (e.g. "1.0.0"). */
  version: string
  /** Author name or organisation. */
  author: string
  /** Short description shown in the plugin card. */
  description: string
  /** Plugin type in lower case: "provisioning" | "payment" | "registrar" | "identity" | "fulfilment" | "hosted". */
  type: string
  /** Display category label (e.g. "Hosting / Provisioning"). */
  category: string
  /** Hex colour for the plugin logo block (e.g. "#1a73e8"). */
  color: string
  /** Whether the DLL was successfully loaded at the last startup. */
  isLoaded: boolean
  /** Whether the plugin ships with the server build; a built-in plugin cannot be removed. */
  isBuiltIn: boolean
}

/** Result returned by install and remove plugin operations. */
export interface PluginActionResultDto {
  /** Always true: a server restart is required for changes to take effect. */
  requiresRestart: boolean
}
