<script lang="ts">
/** Which side of a conversation a bubble belongs to. */
export const UiChatBubbleSide = {
  /** The reader's own message, on the right. */
  Own: 'own',
  /** The other party's message, on the left. */
  Other: 'other',
} as const

/** One of the {@link UiChatBubbleSide} values. */
export type UiChatBubbleSide = (typeof UiChatBubbleSide)[keyof typeof UiChatBubbleSide]
</script>

<script setup lang="ts">
/**
 * One message of a conversation: a rounded bubble on the reader's side (tinted, on the right) or the
 * other party's (neutral, on the left). The message is the default slot and is drawn as given, so a
 * caller passes text or kit components, never markup.
 */
import { computed } from 'vue'

/** Props for UiChatBubble. */
const props = withDefaults(defineProps<{
  /** The side the message belongs to. */
  side?: UiChatBubbleSide
}>(), {
  side: UiChatBubbleSide.Other,
})

/** Placement and colour classes of the side. */
const sideClasses = computed<string>(() =>
  props.side === UiChatBubbleSide.Own
    ? 'self-end rounded-br-sm bg-primary-500/15 text-text-primary'
    : 'self-start rounded-bl-sm border border-border bg-surface-card text-text-primary')
</script>

<template>
  <div class="max-w-[85%] whitespace-pre-line break-words rounded-2xl px-3 py-2 text-sm" :class="sideClasses" :data-side="side">
    <slot />
  </div>
</template>
