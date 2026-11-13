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
 * One message of a conversation: a rounded glass bubble on the reader's side (tinted, on the right) or the
 * other party's (frosted, on the left). The message is the default slot and is drawn as given, so a caller
 * passes text or kit components, never markup.
 *
 * An optional header line sits above the message inside the bubble: the sender (the `sender` prop, or the
 * `sender` slot when it needs more than a name, such as a badge) and the time (the `time` prop, or the `time`
 * slot). Without either the bubble is the message alone.
 *
 * `loading` keeps the side and draws a sender line and two message lines of bars in place of the content, for a
 * conversation that is still being read.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for UiChatBubble. */
const props = withDefaults(defineProps<{
  /** The side the message belongs to. */
  side?: UiChatBubbleSide
  /** Name of the sender (already resolved for display); the `sender` slot replaces it. */
  sender?: string
  /** The time the message was sent at (already formatted); the `time` slot replaces it. */
  time?: string
  /** Draws placeholder bars instead of the sender, the time and the message. */
  loading?: boolean
}>(), {
  side: UiChatBubbleSide.Other,
  sender: undefined,
  time: undefined,
  loading: false,
})

/** Placement and colour classes of the side; both are glass, the reader's own tinted with the brand colour. */
const sideClasses = computed<string>(() =>
  props.side === UiChatBubbleSide.Own
    ? 'self-end rounded-br-sm glass bg-primary-500/15 text-text-primary'
    : 'self-start rounded-bl-sm glass text-text-primary')
</script>

<template>
  <div
    class="flex max-w-[85%] flex-col gap-1 break-words rounded-2xl px-3 py-2 text-sm"
    :class="sideClasses"
    :data-side="side"
    :aria-busy="loading ? 'true' : undefined"
  >
    <div v-if="loading" class="flex w-56 max-w-full flex-col gap-2 py-0.5" aria-hidden="true" data-testid="ui-chat-bubble-skeleton">
      <UiSkeletonBar class="h-3 w-24 rounded" />
      <UiSkeletonBar class="h-3 w-full rounded" />
      <UiSkeletonBar class="h-3 w-2/3 rounded" />
    </div>
    <template v-else>
      <div v-if="sender || time || $slots.sender || $slots.time" class="flex flex-wrap items-center gap-x-2 gap-y-0.5" data-testid="ui-chat-bubble-head">
        <slot name="sender">
          <UiText v-if="sender" :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="font-medium" data-testid="ui-chat-bubble-sender">
            {{ sender }}
          </UiText>
        </slot>
        <slot name="time">
          <UiText v-if="time" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" data-testid="ui-chat-bubble-time">{{ time }}</UiText>
        </slot>
      </div>
      <div class="whitespace-pre-line" data-testid="ui-chat-bubble-body">
        <slot />
      </div>
    </template>
  </div>
</template>
