/**
 * The unsent message of a plugin chat, kept in the tab's `sessionStorage` so a refused send (the session expired and the
 * reader signs in again) does not lose what was typed.
 *
 * The draft is keyed by plugin, chat route and the view's values, so two chats never share one. It is restored when the
 * chat is mounted again, held while a send is out, and forgotten once a reply arrived. A send that got no reply gives the
 * text back to the field. Signing out forgets every draft in the tab ({@link clearPluginChatDrafts}).
 *
 * @module composables/usePluginChatDraft
 */

import { onMounted, ref, watch } from 'vue'
import type { Ref } from 'vue'

/** The prefix of every stored draft. */
const STORAGE_PREFIX = 'hostpanel.plugin-chat-draft'

/** What {@link usePluginChatDraft} hands out. */
export interface PluginChatDraft {
  /** The message field's text. */
  draft: Ref<string>
  /**
   * Takes the text out of the field for a send. The stored copy stays until {@link PluginChatDraft.forget}, so the text
   * survives a page that is left while the send is out.
   *
   * @returns The text, trimmed.
   */
  take: () => string
  /**
   * Forgets the text that was taken: the stored draft is whatever the field holds now, which is none when it is empty.
   *
   * @returns Nothing.
   */
  forget: () => void
  /**
   * Gives the text of a send that got no reply back to the field, in front of anything typed since.
   *
   * @param text - The text that was taken.
   * @returns Nothing.
   */
  giveBack: (text: string) => void
}

/**
 * Orders two names by their code units, the same in every locale, so one set of values always makes one key.
 *
 * @param a - The first name.
 * @param b - The second name.
 * @returns Negative when `a` comes first, positive when `b` does, zero when they are equal.
 */
const compareOrdinal = (a: string, b: string): number => {
  if (a === b) {
    return 0
  }
  return a < b ? -1 : 1
}

/**
 * The storage key of a chat.
 *
 * @param pluginId - The plugin.
 * @param path - The chat's send route path.
 * @param values - The view's values, which tell two instances of the same page apart.
 * @returns The key.
 */
const storageKey = (pluginId: string, path: string, values: Record<string, string>): string =>
  `${STORAGE_PREFIX}:${pluginId}:${path}:${JSON.stringify(Object.entries(values).sort(([a], [b]) => compareOrdinal(a, b)))}`

/**
 * Reads a stored draft.
 *
 * @param key - The storage key.
 * @returns The draft, or empty when there is none or the tab's storage is not available.
 */
const read = (key: string): string => {
  try {
    return window.sessionStorage.getItem(key) ?? ''
  } catch {
    // Storage is refused in some private modes: the chat then works as before, just without a kept draft.
    return ''
  }
}

/**
 * Stores a draft, or removes the entry when it is empty.
 *
 * @param key - The storage key.
 * @param text - The draft.
 * @returns Nothing.
 */
const write = (key: string, text: string): void => {
  try {
    if (text === '') {
      window.sessionStorage.removeItem(key)
    } else {
      window.sessionStorage.setItem(key, text)
    }
  } catch {
    // The same refusal as in `read`; a draft that cannot be kept is not worth an error.
  }
}

/**
 * Forgets every unsent chat draft in the tab. The drafts are keyed by plugin, route and values and carry no account, so
 * they must not outlive the person who typed them: sign-out calls this.
 *
 * @returns Nothing.
 */
export const clearPluginChatDrafts = (): void => {
  try {
    const doomed: string[] = []
    for (let i = 0; i < window.sessionStorage.length; i += 1) {
      const name = window.sessionStorage.key(i)
      if (name !== null && name.startsWith(STORAGE_PREFIX)) {
        doomed.push(name)
      }
    }
    doomed.forEach(name => window.sessionStorage.removeItem(name))
  } catch {
    // The same refusal as in `read`: there is no kept draft to forget.
  }
}

/**
 * Keeps the unsent draft of one chat.
 *
 * Call it from the chat's setup. The stored draft is read once mounted, never during setup, so server and browser render
 * the same empty field.
 *
 * @param pluginId - The plugin.
 * @param path - The chat's send route path.
 * @param values - The view's values.
 * @returns The field's text and what moves it.
 */
export const usePluginChatDraft = (pluginId: string, path: string, values: Record<string, string>): PluginChatDraft => {
  /** The storage key of this chat. */
  const key = storageKey(pluginId, path, values)
  /** The message field's text. */
  const draft = ref('')
  /** Whether the next change of the field is a send taking its text, which must not erase the stored copy. */
  let taking = false

  /**
   * Takes the text out of the field for a send, leaving the stored copy.
   *
   * @returns The text, trimmed.
   */
  const take = (): string => {
    const text = draft.value.trim()
    taking = true
    draft.value = ''
    taking = false
    return text
  }

  /**
   * Forgets the text that was taken: the stored draft is what the field holds now.
   *
   * @returns Nothing.
   */
  const forget = (): void => {
    write(key, draft.value)
  }

  /**
   * Gives the text of a send that got no reply back to the field, in front of anything typed since.
   *
   * @param text - The text that was taken.
   * @returns Nothing.
   */
  const giveBack = (text: string): void => {
    draft.value = draft.value.trim() === '' ? text : `${text}\n${draft.value}`
  }

  // Synchronous, so `taking` still holds when the field is emptied for a send.
  watch(draft, (text) => {
    if (!taking) {
      write(key, text)
    }
  }, { flush: 'sync' })

  onMounted(() => {
    const stored = read(key)
    if (stored !== '' && draft.value === '') {
      draft.value = stored
    }
  })

  return { draft, take, forget, giveBack }
}
