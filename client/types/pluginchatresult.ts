/**
 * A result card inside a plugin chat reply.
 *
 * @module types/pluginchatresult
 */

import type { PluginActionBlock } from '~/types/pluginblock'
import type { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
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
  /** Sends the conversation's last message again; a card action with neither a link nor a call. */
  resend?: boolean
  /**
   * When set, the action shows only while this condition holds: read when the card is drawn and again whenever the blocks
   * the chat reads again are refreshed. Absent, the action always shows.
   */
  visibleWhen?: PluginViewCondition
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
  /** The lines behind the card's details switch; empty for no switch. */
  details: string[]
  /** The actions, in order. */
  actions: PluginChatResultAction[]
}
