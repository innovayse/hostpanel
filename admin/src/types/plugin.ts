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

/** Closed set of load statuses a listed plugin has; the strings are the API's enum names. */
export const PluginStatus = {
  /** The plugin passed every rule and was loaded (or waits for a restart to load). */
  Loaded: 'Loaded',
  /** The plugin broke a manifest rule, so the host skipped it; it is listed so it can be fixed or removed. */
  Refused: 'Refused',
  /** The package is gone but the plugin's stored data is still kept; the operator can purge it. */
  Removed: 'Removed',
} as const

/** One of the {@link PluginStatus} values. */
export type PluginStatus = (typeof PluginStatus)[keyof typeof PluginStatus]

/** Closed set of manifest rules a refused plugin names; the strings are the API's `PluginManifestRule` names. */
export const PluginManifestRule = {
  /** The manifest version is not one this host reads. */
  ManifestVersionUnknown: 'ManifestVersionUnknown',
  /** The plugin assembly was built against an older SDK major version. */
  SdkAssemblyOutdated: 'SdkAssemblyOutdated',
  /** A client entry names an unknown permission. */
  PermissionUnknown: 'PermissionUnknown',
  /** An admin entry declares a permission. */
  AdminPermissionDeclared: 'AdminPermissionDeclared',
  /** A route path is not valid. */
  RoutePathInvalid: 'RoutePathInvalid',
  /** The same method and path is declared twice. */
  RouteDuplicate: 'RouteDuplicate',
  /** A text key is missing from the English texts. */
  LabelKeyMissing: 'LabelKeyMissing',
  /** A slot entry names a slot the host does not offer. */
  SlotUnknown: 'SlotUnknown',
  /** A slot entry asks for a context key its slot does not offer. */
  SlotContextKeyNotOffered: 'SlotContextKeyNotOffered',
  /** A file in the plugin folder could not be read. */
  AssemblyUnreadable: 'AssemblyUnreadable',
  /** A slot widget id is not valid. */
  SlotWidgetIdInvalid: 'SlotWidgetIdInvalid',
  /** The same widget id is declared twice for one slot. */
  SlotWidgetDuplicate: 'SlotWidgetDuplicate',
  /** The manifest id is not a valid plugin id. */
  PluginIdInvalid: 'PluginIdInvalid',
  /** A plugin that is not an app plugin declares routes, pages or slots without the endpoints capability. */
  EndpointsNotDeclared: 'EndpointsNotDeclared',
  /** The manifest names a capability the server does not know or the plugin type may not carry. */
  CapabilityNotAllowed: 'CapabilityNotAllowed',
} as const

/** One of the {@link PluginManifestRule} values. */
export type PluginManifestRule = (typeof PluginManifestRule)[keyof typeof PluginManifestRule]

/** Error codes (`type` of the problem body) the plugin screens branch on. */
export const PluginErrorCode = {
  /** An upload is older than the installed version and was not forced. */
  DowngradeRefused: 'PLUGIN_DOWNGRADE_REFUSED',
} as const

/** One of the {@link PluginErrorCode} values. */
export type PluginErrorCode = (typeof PluginErrorCode)[keyof typeof PluginErrorCode]

/** Closed set of capabilities a manifest may declare; the strings are the SDK's `PluginCapabilities` names. */
export const PluginCapability = {
  /** A payment plugin that can charge a saved card or account. */
  StoredCredentials: 'storedCredentials',
  /** A plugin of any type that also answers routes and adds pages, menus and slots. */
  Endpoints: 'endpoints',
} as const

/** One of the {@link PluginCapability} values. */
export type PluginCapability = (typeof PluginCapability)[keyof typeof PluginCapability]

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
  /** Plugin type in lower case: "provisioning" | "payment" | "registrar" | "identity" | "fulfilment" | "hosted" | "app". */
  type: string
  /** Display category label (e.g. "Hosting / Provisioning"). */
  category: string
  /** Hex colour for the plugin logo block (e.g. "#1a73e8"). */
  color: string
  /** Whether the DLL was successfully loaded at the last startup. */
  isLoaded: boolean
  /** Whether the plugin ships with the server build; a built-in plugin cannot be removed. */
  isBuiltIn: boolean
  /** Whether the row is a loaded plugin, one refused at startup (never enabled) or a removed plugin whose data is kept. */
  status: PluginStatus
  /** The manifest rule a refused plugin broke, null for a loaded one. */
  refusedRule: PluginManifestRule | null
  /** The server's detail of the broken rule (the offending value), null for a loaded plugin. */
  refusedDetail: string | null
  /** Version of a newer upload staged to replace the running one at the next restart, null when none. */
  stagedVersion: string | null
  /** The capabilities the manifest declares, in manifest order; empty when it declares none. */
  capabilities: PluginCapability[]
}

/** Result returned by install and remove plugin operations. */
export interface PluginActionResultDto {
  /** True when files changed and a server restart is required for the change to take effect. */
  requiresRestart: boolean
  /** Version of an upgrade (or forced downgrade) staged for the next restart, null for every other result. */
  stagedVersion?: string | null
}

/** One slot widget a plugin declares, with the products the operator bound it to (GET /api/admin/plugins/{pluginId}/slot-widgets). */
export interface PluginSlotWidgetBindingDto {
  /** Id of the slot the widget sits in, such as `admin.service.panels`. */
  slotId: string
  /** Widget id, unique within the plugin. */
  widgetId: string
  /** Widget title in the request's language, null when the widget has none. */
  title: string | null
  /** Product ids the plugin's manifest binds the widget to; empty means every service. */
  manifestProductIds: number[]
  /** Product ids the operator bound the widget to; when not empty they replace the manifest ids, empty means the manifest applies. */
  productIds: number[]
  /** True when the operator bound the widget at all; then the operator binding decides, even when every bound product was deleted (productIds empty, the widget shows nowhere). */
  isBound: boolean
}

/** Body of PUT /api/admin/plugins/{pluginId}/slot-widgets/{slotId}/{widgetId}: the new binding of one widget. */
export interface SavePluginSlotWidgetProductsBody {
  /** Product ids to bind the widget to; empty clears the operator binding. */
  productIds: number[]
}
