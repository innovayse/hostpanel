/** The shapes a Nitro route parameter may have; `requireRouteParam` validates against one of them. */
export enum RouteParamKind {
  /** A positive 32-bit integer primary key, e.g. a service, invoice or ticket id. */
  Int = 'int',
  /** A GUID in its hyphenated form, e.g. an SSH key id. */
  Guid = 'guid',
  /** An opaque identifier of letters, digits, underscores and hyphens, up to 128 characters, e.g. a payment-method or person id. */
  Identifier = 'identifier',
  /** A dotted lower-case name, e.g. the plugin slot id `client.service.tabs`. */
  DottedName = 'dotted-name',
  /** A plugin id: lower-case letters and digits in words joined by single hyphens, up to 64 characters (the backend's `PluginIdentifier`). */
  PluginId = 'plugin-id',
  /** A plugin slot widget id: a lower-case letter or digit, then letters, digits, `_` or `-` (the manifest rule), up to 64 characters. */
  PluginWidgetId = 'plugin-widget-id'
}
