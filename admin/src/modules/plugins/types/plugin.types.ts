/** Summary of one installed plugin returned by GET /api/admin/plugins. */
export interface PluginListItemDto {
  /** Unique plugin identifier from plugin.json (e.g. "innovayse-cwp"). */
  id: string
  /** Human-readable display name. */
  name: string
  /** Semver version string (e.g. "1.0.0"). */
  version: string
  /** Author name or organisation. */
  author: string
  /** Short description shown in the plugin card. */
  description: string
  /** Plugin type: "provisioning" | "payment" | "registrar". */
  type: string
  /** Display category label (e.g. "Hosting / Provisioning"). */
  category: string
  /** Hex colour for the plugin logo block (e.g. "#1a73e8"). */
  color: string
  /** Whether the DLL was successfully loaded at the last startup. */
  isLoaded: boolean
}

/** Result returned by install and remove plugin operations. */
export interface PluginActionResultDto {
  /** Always true — a server restart is required for changes to take effect. */
  requiresRestart: boolean
}
