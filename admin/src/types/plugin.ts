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
  /** The plugin registry is switched off on this server; the Browse tab is hidden. */
  RegistryDisabled: 'PLUGIN_REGISTRY_DISABLED',
  /** The registry lists no such plugin (or version); on the details page it is a not-found state, not a failure. */
  RegistryPluginNotFound: 'PLUGIN_REGISTRY_PLUGIN_NOT_FOUND',
  /** The registry wants a licence key for this plugin and the server has none; the details page then offers the key card. */
  RegistryLicenceRequired: 'PLUGIN_REGISTRY_LICENCE_REQUIRED',
  /** The registry refused the licence key the server holds; the details page then offers the key card. */
  RegistryLicenceInvalid: 'PLUGIN_REGISTRY_LICENCE_INVALID',
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
  /** Address of the plugin's icon (GET /api/admin/plugins/{id}/icon), null when the plugin ships none. */
  iconUrl: string | null
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

/** Closed set of registry categories; the strings are the API's enum names. */
export const PluginCategory = {
  /** Payment gateway. */
  Payment: 'Payment',
  /** Hosting and server provisioning through a hosting panel. */
  Provisioning: 'Provisioning',
  /** Domain registrar. */
  Registrar: 'Registrar',
  /** An app plugin that adds its own pages and routes. */
  App: 'App',
  /** Anything that fits no other category. */
  Other: 'Other',
} as const

/** One of the {@link PluginCategory} values. */
export type PluginCategory = (typeof PluginCategory)[keyof typeof PluginCategory]

/** The registry filter's "every category" choice: the empty string, which the query leaves out. */
export const PluginCategoryAll = '' as const

/** What the category filter holds: one category, or {@link PluginCategoryAll}. */
export type PluginCategoryFilter = PluginCategory | typeof PluginCategoryAll

/** Closed set of prices a registry listing declares; the strings are the API's enum names. */
export const PluginPricing = {
  /** Free to install and use. */
  Free: 'Free',
  /** The publisher charges for it. */
  Paid: 'Paid',
  /** Offered by the publisher to chosen customers only. */
  Private: 'Private',
} as const

/** One of the {@link PluginPricing} values. */
export type PluginPricing = (typeof PluginPricing)[keyof typeof PluginPricing]

/** Closed set of values of the `plugins.registry_trust` setting; the strings are the stored values. */
export const PluginRegistryTrust = {
  /** Only publishers the registry has verified may be installed. */
  VerifiedOnly: 'VerifiedOnly',
  /** Any publisher the registry lists may be installed. */
  AnyRegistryPublisher: 'AnyRegistryPublisher',
  /** Only packages signed with a key the operator trusts may be installed. */
  OwnKeysOnly: 'OwnKeysOnly',
} as const

/** One of the {@link PluginRegistryTrust} values. */
export type PluginRegistryTrust = (typeof PluginRegistryTrust)[keyof typeof PluginRegistryTrust]

/** One plugin listed by the registry (GET /api/admin/plugins/registry). */
export interface PluginRegistryItemDto {
  /** Unique plugin identifier, the `id` field of plugin.json. */
  id: string
  /** Human-readable display name. */
  name: string
  /** Publisher name. */
  author: string
  /** Short description. */
  description: string
  /** Address of the plugin's icon, null when it has none. */
  iconUrl: string | null
  /** Registry category. */
  category: PluginCategory
  /** Newest version the registry offers. */
  latestVersion: string
  /** SDK version the newest version was built against. */
  sdkVersion: string
  /** Whether the registry has verified the publisher. */
  verified: boolean
  /** Whether the plugin is free or paid. */
  pricing: PluginPricing
  /** Whether this server has the plugin installed. */
  installed: boolean
  /** Installed version, null when the plugin is not installed. */
  installedVersion: string | null
  /** Whether the registry offers a newer version than the installed one. */
  updateAvailable: boolean
}

/** One page of registry listings. */
export interface PluginRegistryPageDto {
  /** Listings on this page. */
  items: PluginRegistryItemDto[]
  /** Listings matching the filter across all pages. */
  total: number
  /** Listings per page, as the server pages them. */
  pageSize: number
}

/** Filter of the registry browser: the query string parameters of GET /api/admin/plugins/registry. */
export interface PluginRegistryQuery {
  /** Search text; empty for no text filter. */
  q: string
  /** Category filter; empty for every category. */
  category: PluginCategoryFilter
  /** Page, starting at 1. */
  page: number
}

/** Body of POST /api/admin/plugins/registry/{id}/install. */
export interface InstallRegistryPluginBody {
  /** Version to install. */
  version: string
  /** True to allow a version older than the installed one. */
  allowDowngrade: boolean
}

/** Closed set of statuses a registry version has; the strings are the API's enum names. */
export const PluginVersionStatus = {
  /** The version is current and may be installed. */
  Published: 'Published',
  /** The publisher advises against it; a replacement may be named. */
  Deprecated: 'Deprecated',
  /** The publisher withdrew it; it must not be installed. */
  Yanked: 'Yanked',
} as const

/** One of the {@link PluginVersionStatus} values. */
export type PluginVersionStatus = (typeof PluginVersionStatus)[keyof typeof PluginVersionStatus]

/** One screenshot of a registry version. */
export interface RegistryScreenshotDto {
  /** Address of the image. */
  url: string
  /** Media type of the image. */
  contentType: string
  /** Width in pixels. */
  width: number
  /** Height in pixels. */
  height: number
}

/** One version of a plugin in its registry details. */
export interface RegistryPluginVersionDto {
  /** Version number. */
  version: string
  /** When the publisher released it, an ISO 8601 timestamp. */
  publishedAt: string
  /** Whether it is published, deprecated or yanked. */
  status: PluginVersionStatus
  /** The publisher's reason for a deprecated or yanked version, null otherwise. */
  statusReason: string | null
  /** Version the publisher points to instead, null when none. */
  replacementVersion: string | null
  /** Release notes, null when none. */
  changelog: string | null
  /** Oldest host version it runs on, null when unbounded. */
  minHostVersion: string | null
  /** Newest host version it runs on, null when unbounded. */
  maxHostVersion: string | null
  /** Whether it runs on this host. */
  compatible: boolean
  /** Permissions the version asks for. */
  permissions: string[]
  /** Screenshots of the version. */
  screenshots: RegistryScreenshotDto[]
}

/** Details of one registry plugin (GET /api/admin/plugins/registry/{id}). */
export interface RegistryPluginDetailsDto {
  /** Unique plugin identifier. */
  id: string
  /** Human-readable display name. */
  name: string
  /** Publisher name. */
  author: string
  /** Description. */
  description: string
  /** Address of the plugin's icon, null when it has none. */
  iconUrl: string | null
  /** Registry category. */
  category: PluginCategory
  /** Whether the plugin is free, paid or private. */
  pricing: PluginPricing
  /** Whether the registry has verified the publisher. */
  verified: boolean
  /** Newest version the registry offers. */
  latestVersion: string
  /** Whether this server has the plugin installed. */
  installed: boolean
  /** Installed version, null when not installed. */
  installedVersion: string | null
  /** Version staged for the next restart, null when none. */
  stagedVersion: string | null
  /** Whether the registry offers a newer version than the installed one. */
  updateAvailable: boolean
  /** Whether the plugin needs a licence key to install. */
  requiresLicence: boolean
  /** Whether this server holds a licence key for it. */
  hasLicenceKey: boolean
  /** Where to buy a licence, null when none. */
  buyUrl: string | null
  /** Documentation address, null when none. */
  documentationUrl: string | null
  /** Support address, null when none. */
  supportUrl: string | null
  /** Source repository address, null when none. */
  repositoryUrl: string | null
  /** Licence name of the plugin, null when none. */
  licence: string | null
  /** Every version the registry lists, newest first. */
  versions: RegistryPluginVersionDto[]
}

/** Body of PUT /api/admin/plugins/registry/{id}/licence. */
export interface SaveLicenceKeyBody {
  /** The licence key to store. */
  key: string
}

/** Load state of the details of one registry plugin, kept by plugin id in the store. */
export interface RegistryDetailsState {
  /** The details, null until loaded. */
  details: RegistryPluginDetailsDto | null
  /** True while the details are loading. */
  loading: boolean
  /** Sentence for the last failed load, null when none. */
  error: string | null
  /** Machine-readable code of the last failed load (see {@link PluginErrorCode}), null when it succeeded or gave none. */
  errorCode: string | null
  /** True when the registry answered that it has no such plugin (404 or its not-found code); not a retryable failure. */
  notFound: boolean
}
