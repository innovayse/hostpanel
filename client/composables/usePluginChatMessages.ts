/**
 * The messages of one plugin chat and the rules that turn a plugin's events into them: the list itself (capped, so a
 * long history cannot grow the page), the history rows, and each stream event applied to the reply being written.
 *
 * It holds no transport and no screen state: the chat block sends and renders, this keeps what was said. A link is added
 * only when the link-block rule admits its address, a step or card only when it parses, so a reply can never show more
 * than a block could.
 *
 * @module composables/usePluginChatMessages
 */

import type { Ref } from 'vue'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import { PluginChatEventType } from '~/types/pluginchateventtype'
import { PluginChatRole } from '~/types/pluginchatrole'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import type { PluginViewContext } from '~/types/pluginviewcontext'
import { parsePluginChatResult, parsePluginChatStep, pluginLinkTarget, readPluginField } from '~/utils/plugins'

/** The most messages the chat keeps on screen; older ones drop off the top, so a long history cannot grow the page. */
const MAX_MESSAGES = 200

/** The most steps, cards or issues one reply keeps, so a stream cannot grow the page without end. */
const MAX_REPLY_PARTS = 100

/** What {@link usePluginChatMessages} hands out. */
export interface PluginChatMessages {
  /** The messages shown, oldest first. */
  messages: Ref<PluginChatMessage[]>
  /**
   * Adds a message written in this session.
   *
   * @param role - Who wrote it.
   * @param text - The text.
   * @returns The message as the reactive list holds it, so later events extend it in place.
   */
  add: (role: PluginChatRole, text: string) => PluginChatMessage
  /**
   * Adds the rows of the history route's answer; rows of another shape are skipped.
   *
   * @param entries - The answer.
   * @returns Nothing.
   */
  addHistory: (entries: unknown) => void
  /**
   * Applies one event of the reply being written.
   *
   * @param target - The reply.
   * @param event - The event.
   * @returns Nothing.
   */
  apply: (target: PluginChatMessage, event: PluginStreamEvent) => void
  /**
   * Whether a reply holds anything the reader can see: text, a link, a step, a card or an issue.
   *
   * @param message - The reply.
   * @returns False for a reply that never wrote a word.
   */
  hasContent: (message: PluginChatMessage) => boolean
  /**
   * Removes the message with a key and everything after it, so a retried turn replaces the failed one.
   *
   * @param key - The first message to remove.
   * @returns Nothing.
   */
  dropFrom: (key: number) => void
  /**
   * Removes the messages with the given keys.
   *
   * @param keys - The keys.
   * @returns Nothing.
   */
  remove: (...keys: number[]) => void
  /**
   * Forgets every message.
   *
   * @returns Nothing.
   */
  clear: () => void
}

/**
 * The text an event carries: the plugin's `text`, else the host's `message` (its own error frame).
 *
 * @param event - The event.
 * @returns The text, or empty.
 */
const textOf = (event: PluginStreamEvent): string => {
  const text = readPluginField(event.data, 'text')
  if (typeof text === 'string') {
    return text
  }
  const message = readPluginField(event.data, 'message')
  return typeof message === 'string' ? message : ''
}

/**
 * Keeps the messages of one chat.
 *
 * @param context - The view's context, whose values and allowed origins the links and cards are checked against.
 * @param refresh - The ids of the blocks the chat reads again after a turn, which a card's actions are told.
 * @param onError - Told the sentence of an `error` event, which is the chat's to show and not a part of a reply.
 * @returns The list and what changes it.
 */
export const usePluginChatMessages = (
  context: PluginViewContext,
  refresh: () => readonly string[],
  onError: (sentence: string) => void,
): PluginChatMessages => {
  /** The messages shown, oldest first. */
  const messages = ref<PluginChatMessage[]>([])
  /** The next key; shared by messages, links, cards and issues, so no two things of the chat repeat one. */
  let nextKey = 0

  /**
   * Adds a link to a message when the link-block rule admits its address: host-relative, or https on one of the view's
   * allowed origins. Anything else is dropped, so a reply can never link where a link block could not.
   *
   * @param message - The message the link belongs to.
   * @param text - The link text.
   * @param url - The address, as the plugin wrote it.
   * @returns Nothing.
   */
  const addLink = (message: PluginChatMessage, text: unknown, url: unknown): void => {
    const link = pluginLinkTarget(url, context.externalOrigins)
    if (link && typeof text === 'string' && text !== '') {
      message.links.push({ key: nextKey++, text, href: link.href, external: link.external })
    }
  }

  /**
   * Adds a message.
   *
   * @param role - Who wrote it.
   * @param text - The text.
   * @param sentAt - When this session wrote it; absent for a message read from the history.
   * @returns The message as the reactive list holds it.
   */
  const push = (role: PluginChatRole, text: string, sentAt?: number): PluginChatMessage => {
    messages.value.push({ key: nextKey++, role, text, links: [], steps: [], results: [], issues: [], status: '', sentAt })
    if (messages.value.length > MAX_MESSAGES) {
      messages.value.splice(0, messages.value.length - MAX_MESSAGES)
    }
    // A push never leaves the list empty, so the last row exists.
    return messages.value[messages.value.length - 1] as PluginChatMessage
  }

  /**
   * Adds a message written in this session, stamped with the time.
   *
   * @param role - Who wrote it.
   * @param text - The text.
   * @returns The message as the reactive list holds it.
   */
  const add = (role: PluginChatRole, text: string): PluginChatMessage => push(role, text, Date.now())

  /**
   * Adds the rows of the history route's answer; rows of another shape are skipped.
   *
   * @param entries - The answer.
   * @returns Nothing.
   */
  const addHistory = (entries: unknown): void => {
    for (const entry of Array.isArray(entries) ? entries : []) {
      const text = readPluginField(entry, 'text')
      if (typeof text === 'string') {
        const message = push(readPluginField(entry, 'role') === PluginChatRole.User ? PluginChatRole.User : PluginChatRole.Assistant, text)
        // A history entry may keep the links its reply carried, `[{ text, url }]`; they pass the same rule as live ones.
        const links = readPluginField(entry, 'links')
        for (const link of Array.isArray(links) ? links : []) {
          addLink(message, readPluginField(link, 'text'), readPluginField(link, 'url'))
        }
      }
    }
  }

  /**
   * Adds a step or updates the row its key names.
   *
   * @param target - The reply being written.
   * @param data - The event's data.
   * @returns Nothing.
   */
  const applyStep = (target: PluginChatMessage, data: unknown): void => {
    const step = parsePluginChatStep(data)
    if (!step) {
      return
    }
    const row = target.steps.find(candidate => candidate.key === step.key)
    if (row) {
      row.label = step.label
      row.state = step.state
    } else if (target.steps.length < MAX_REPLY_PARTS) {
      target.steps.push(step)
    }
  }

  /**
   * Adds a result card.
   *
   * @param target - The reply being written.
   * @param data - The event's data.
   * @returns Nothing.
   */
  const applyResult = (target: PluginChatMessage, data: unknown): void => {
    const result = parsePluginChatResult(data, context.values, context.externalOrigins, refresh())
    if (result && target.results.length < MAX_REPLY_PARTS) {
      target.results.push({ ...result, key: nextKey++ })
    }
  }

  /**
   * Adds the plugin's own failure.
   *
   * @param target - The reply being written.
   * @param data - The event's data.
   * @returns Nothing.
   */
  const applyIssue = (target: PluginChatMessage, data: unknown): void => {
    const text = readPluginField(data, 'text')
    if (typeof text === 'string' && text !== '' && target.issues.length < MAX_REPLY_PARTS) {
      target.issues.push({ key: nextKey++, text })
    }
  }

  /**
   * Applies one event of the reply being written.
   *
   * @param target - The reply.
   * @param event - The event.
   * @returns Nothing.
   */
  const apply = (target: PluginChatMessage, event: PluginStreamEvent): void => {
    switch (event.type) {
      case PluginChatEventType.Delta:
        target.text += textOf(event)
        break
      case PluginChatEventType.Message:
        target.text = textOf(event)
        break
      case PluginChatEventType.Status:
        target.status = textOf(event)
        break
      case PluginChatEventType.Error:
        onError(textOf(event))
        break
      case PluginChatEventType.Link:
        addLink(target, textOf(event), readPluginField(event.data, 'url'))
        break
      case PluginChatEventType.Done:
        target.status = ''
        break
      case PluginChatEventType.Step:
        applyStep(target, event.data)
        break
      case PluginChatEventType.Result:
        applyResult(target, event.data)
        break
      case PluginChatEventType.Issue:
        applyIssue(target, event.data)
        break
      default:
        // An event this host does not know is ignored, so a plugin written for a newer host still chats here.
        break
    }
  }

  /**
   * Whether a reply holds anything the reader can see.
   *
   * @param message - The reply.
   * @returns False for a reply that never wrote a word.
   */
  const hasContent = (message: PluginChatMessage): boolean =>
    message.text !== '' || message.links.length > 0 || message.steps.length > 0 || message.results.length > 0 || message.issues.length > 0

  /**
   * Removes the message with a key and everything after it.
   *
   * @param key - The first message to remove.
   * @returns Nothing.
   */
  const dropFrom = (key: number): void => {
    const at = messages.value.findIndex(message => message.key === key)
    if (at !== -1) {
      messages.value.splice(at)
    }
  }

  /**
   * Removes the messages with the given keys.
   *
   * @param keys - The keys.
   * @returns Nothing.
   */
  const remove = (...keys: number[]): void => {
    messages.value = messages.value.filter(message => !keys.includes(message.key))
  }

  /**
   * Forgets every message.
   *
   * @returns Nothing.
   */
  const clear = (): void => {
    messages.value = []
  }

  return { messages, add, addHistory, apply, hasContent, dropFrom, remove, clear }
}
