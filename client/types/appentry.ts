/** One app as the central portal API lists it. */
export interface AppEntry {
  /** Stable key for the app, e.g. "sheets" — also what picks its icon. */
  id: string
  /** App name shown beside the tile. */
  name: string
  /** One-line description shown under the name. */
  desc: string
  /** Where the tile links to. */
  url: string
  /** True for an app that is announced but not reachable yet. */
  comingSoon: boolean
}
