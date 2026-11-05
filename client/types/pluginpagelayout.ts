/**
 * The layouts a plugin page may declare (`ui.client.pages[].layout`).
 *
 * @module types/pluginpagelayout
 */

/** A plugin page's layout. */
export enum PluginPageLayout {
  /** Inside the client area's usual column. */
  Standard = 'standard',
  /** Full width with no side menu and a slim back link. */
  Workspace = 'workspace'
}
