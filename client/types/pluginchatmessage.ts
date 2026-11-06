/**
 * One message of a plugin chat as the chat block shows it.
 *
 * @module types/pluginchatmessage
 */

import type { PluginChatLink } from '~/types/pluginchatlink'
import type { PluginChatIssue } from '~/types/pluginchatissue'
import type { PluginChatResult } from '~/types/pluginchatresult'
import type { PluginChatRole } from '~/types/pluginchatrole'
import type { PluginChatStep } from '~/types/pluginchatstep'

import type { PluginSelectionClaim } from '~/types/pluginpreviewselection'

/** One chat message. */
export interface PluginChatMessage {
  /** A key unique within the chat. */
  key: number
  /** The elements a reader's message was sent with, so a "try again" sends the same; absent without any. */
  selection?: PluginSelectionClaim
  /** The labels of those elements as the plugin named them. */
  selectionLabels?: readonly string[]
  /** Who wrote it. */
  role: PluginChatRole
  /** The text, rendered as text. */
  text: string
  /** The links the reply carries, in arrival order; empty for most messages. */
  links: PluginChatLink[]
  /** The reply's steps, in the order they first arrived. */
  steps: PluginChatStep[]
  /** The reply's result cards, in arrival order. */
  results: PluginChatResult[]
  /** The plugin's own failures during the reply, in arrival order. */
  issues: PluginChatIssue[]
}
