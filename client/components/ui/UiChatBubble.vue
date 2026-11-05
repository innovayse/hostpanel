<template>
  <div :class="['flex', side === UiChatBubbleSide.Own ? 'justify-end' : 'justify-start']" data-testid="ui-chat-bubble">
    <p :class="['max-w-[85%] whitespace-pre-line rounded-xl px-3 py-2 text-sm', SIDE_CLASSES[side]]" :data-side="side">
      <slot />
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * UiChatBubble - one message of a conversation, aligned and tinted by who wrote it. The content is the default
 * slot, so callers interpolate text and nothing is bound as HTML; line breaks are kept.
 */
import { UiChatBubbleSide } from '~/utils/ui/enums'

/** Props of UiChatBubble. */
interface Props {
  /** Who wrote the message. */
  side?: `${UiChatBubbleSide}`
}

withDefaults(defineProps<Props>(), {
  side: UiChatBubbleSide.Other,
})

/** Surface and text colours per side, from the theme tokens. */
const SIDE_CLASSES: Readonly<Record<`${UiChatBubbleSide}`, string>> = {
  [UiChatBubbleSide.Own]: 'bg-primary-500/10 text-gray-900 dark:text-white',
  [UiChatBubbleSide.Other]: 'bg-gray-100 text-gray-800 dark:bg-white/5 dark:text-gray-200',
}
</script>
