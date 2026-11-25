<script setup lang="ts">
/**
 * Small status pill (e.g. "Active", "Overdue").
 *
 * A tint of the status colour behind a readable label (12px, semibold, no border), so the status is always named in text and never
 * carried by colour alone. `tone` colours it from the theme tokens; `dot` prepends a status dot; `marker` draws the dot alone, with
 * no text, for a place too small for one (an unread marker on an icon). A badge is always one line: the label never wraps and the
 * height is fixed per size (24px, 16px small), so every status badge in a list is the same height whatever its text. A column
 * that holds badges is as wide as its longest label.
 */
import { computed } from 'vue'
import { toneBadgeClasses, toneDotClasses } from '@/utils/ui/tone'
import type { UiTone } from '@/utils/ui/tone'

/** Props for UiBadge. */
const props = withDefaults(defineProps<{
  /** Semantic colour. */
  tone?: UiTone
  /** Shows a coloured dot before the text. */
  dot?: boolean
  /** Compact padding and type size. */
  small?: boolean
  /** Draws a solid dot of the tone instead of the pill, and no text; the slot is not rendered. */
  marker?: boolean
}>(), {
  tone: 'neutral',
  dot: false,
  small: false,
  marker: false,
})

/** Colour classes of the tone. */
const toneClasses = computed<string>(() => toneBadgeClasses[props.tone])

/** Colour class of the dot. */
const dotClasses = computed<string>(() => toneDotClasses[props.tone])

/** Fixed height, padding and type size. */
const sizeClasses = computed<string>(() => (props.small ? 'h-4 px-1.5 text-[0.65rem]' : 'h-6 px-2.5 text-xs'))
</script>

<template>
  <span v-if="marker" class="block h-2.5 w-2.5 rounded-full" :class="dotClasses" />
  <span
    v-else
    class="inline-flex w-fit shrink-0 items-center gap-1.5 rounded-2xl font-semibold whitespace-nowrap"
    :class="[toneClasses, sizeClasses]"
  >
    <span v-if="dot" class="h-1.5 w-1.5 shrink-0 rounded-full" :class="dotClasses" />
    <slot />
  </span>
</template>
