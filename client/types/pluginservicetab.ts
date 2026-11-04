/**
 * A tab a plugin adds to the service detail page's side menu.
 *
 * @module types/pluginservicetab
 */

/** One plugin tab of the service side menu. */
export interface PluginServiceTab {
  /** The menu key, `plugin.<pluginId>.<widgetId>`, never a `ServiceTab` value; dots because neither id may hold one, so the key (and its `service-tab-<key>` test id) is unambiguous. */
  key: string
  /** The label, the widget's title in the reader's language, or its id when it has none. */
  label: string
}
