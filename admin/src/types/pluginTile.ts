/**
 * Model of the plugin tile, the one card the catalogue grid and the installed grid both draw.
 */
import type { UiTone } from '@/utils/ui/tone'

/** Closed set of badge kinds a tile shows; each is the middle part of the badge's test id. */
export const PluginTileBadgeKey = {
  /** Loaded and running, or waiting for the restart. */
  Status: 'status',
  /** The host skipped the plugin because it broke a manifest rule. */
  Refused: 'refused',
  /** A newer upload waits for the restart. */
  Staged: 'staged',
  /** The registry offers a newer version. */
  Update: 'update',
  /** The plugin ships with the server build. */
  BuiltIn: 'builtin',
  /** The registry listing is installed on this server. */
  Installed: 'installed',
} as const

/** One of the {@link PluginTileBadgeKey} values. */
export type PluginTileBadgeKey = (typeof PluginTileBadgeKey)[keyof typeof PluginTileBadgeKey]

/** One badge on a tile, already worded for the interface language. */
export interface PluginTileBadge {
  /** What the badge says: a {@link PluginTileBadgeKey}, or `capability-<name>` for a declared capability. */
  key: string
  /** Translated text of the badge. */
  label: string
  /** Colour of the badge. */
  tone: UiTone
}
