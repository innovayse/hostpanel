/** One app-launcher entry, ready to render. */
export interface PortalApp {
  /** Stable key for the app, e.g. "tasks" — also the admin-panel setting name. */
  id: string
  /** Two-letter tile label. */
  tag: string
  /** CSS gradient for the tile, as the design specifies it. */
  tint: string
  /** App name shown beside the tile. */
  label: string
  /** One-line description shown under the name. */
  desc: string
  /** Where the tile links to. */
  url: string
}
