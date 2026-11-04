/**
 * One message of a plugin chat as the chat block shows it.
 *
 * @module types/pluginchatmessage
 */

import type { PluginChatLink } from '~/types/pluginchatlink'
import type { PluginChatRole } from '~/types/pluginchatrole'

/** One chat message. */
export interface PluginChatMessage {
  /** A key unique within the chat. */
  key: number
  /** Who wrote it. */
  role: PluginChatRole
  /** The text, rendered as text. */
  text: string
  /** The links the reply carries, in arrival order; empty for most messages. */
  links: PluginChatLink[]
}
