/**
 * A result card inside a plugin chat reply.
 *
 * @module types/pluginchatresult
 */

import type { PluginActionBlock } from '~/types/pluginblock'
import type { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginLinkTarget } from '~/utils/plugins'

/** One action of a result card: a checked link, or a call drawn as an action block. */
export interface PluginChatResultAction {
  /** Its place in the card. */
  key: number
  /** The label. */
  label: string
  /** The checked link; null for a call. */
  link: PluginLinkTarget | null
  /** The call; null for a link. */
  block: PluginActionBlock | null
}

/** One result card. */
export interface PluginChatResult {
  /** A key unique within the chat. */
  key: number
  /** The tone of the card's edge. */
  tone: PluginBlockTone
  /** The title; empty for none. */
  title: string
  /** The sentence; empty for none. */
  text: string
  /** The actions, in order. */
  actions: PluginChatResultAction[]
}
