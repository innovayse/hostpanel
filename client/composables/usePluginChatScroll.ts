/**
 * The scrolling of a plugin chat's log: it follows the newest message as the chat loads and a reply streams, unless the
 * reader scrolled up, and the reader can jump back to the end.
 *
 * @module composables/usePluginChatScroll
 */

import type { Ref, WatchSource } from 'vue'

/** How close to the end, in pixels, still counts as reading the end of the log. */
const FOLLOW_THRESHOLD_PX = 40

/** What {@link usePluginChatScroll} hands out. */
export interface PluginChatScroll {
  /** The scrolling element; bind it as the log's template ref. */
  log: Ref<HTMLElement | null>
  /** Whether the log follows the newest message; false once the reader scrolled up. */
  following: Ref<boolean>
  /**
   * Notes whether the reader is at the end of the log.
   *
   * @returns Nothing.
   */
  onScroll: () => void
  /**
   * Scrolls to the newest message and follows it again.
   *
   * @returns Nothing.
   */
  jumpToLatest: () => void
}

/**
 * Makes a log follow its newest message.
 *
 * @param content - What changes when the log grows (the messages); the log is scrolled after the render that shows it.
 * @returns The log's ref, whether it follows, and what moves it.
 */
export const usePluginChatScroll = (content: WatchSource): PluginChatScroll => {
  /** The scrolling element. */
  const log = ref<HTMLElement | null>(null)
  /** Whether the log follows the newest message. */
  const following = ref(true)

  /**
   * Notes whether the reader is at the end of the log.
   *
   * @returns Nothing.
   */
  const onScroll = (): void => {
    const element = log.value
    if (element) {
      following.value = element.scrollHeight - element.scrollTop - element.clientHeight <= FOLLOW_THRESHOLD_PX
    }
  }

  /**
   * Moves the log to its end.
   *
   * @returns Nothing.
   */
  const scrollToEnd = (): void => {
    if (log.value) {
      log.value.scrollTop = log.value.scrollHeight
    }
  }

  /**
   * Scrolls to the newest message and follows it again.
   *
   * @returns Nothing.
   */
  const jumpToLatest = (): void => {
    following.value = true
    scrollToEnd()
  }

  // After the render, when the new height is known; a reader who scrolled up keeps their place.
  watch(content, () => {
    if (following.value) {
      scrollToEnd()
    }
  }, { deep: true, flush: 'post' })

  return { log, following, onScroll, jumpToLatest }
}
