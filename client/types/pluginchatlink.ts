/**
 * A link a plugin chat reply carries, already checked by the same rule as a link block.
 *
 * @module types/pluginchatlink
 */

/** One link of a chat reply. */
export interface PluginChatLink {
  /** A key unique within the reply. */
  key: number
  /** The link text, rendered as text. */
  text: string
  /** The address `pluginLinkTarget` admitted. */
  href: string
  /** Whether it leaves the panel (new tab, `noopener noreferrer`). */
  external: boolean
}
