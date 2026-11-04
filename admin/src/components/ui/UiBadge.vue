<script setup lang="ts">
/**
 * Small status pill (e.g. "Active", "Overdue").
 *
 * `tone` colours it from the theme tokens; `dot` prepends a status dot; `marker` draws the dot alone, with
 * no text, for a place too small for one (an unread marker on an icon).
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

/** Padding and type size. */
const sizeClasses = computed<string>(() => (props.small ? 'px-1.5 py-0 text-[0.65rem]' : 'px-2.5 py-0.5 text-[0.72rem]'))
</script>

<template>
  <span v-if="marker" class="block h-2.5 w-2.5 rounded-full" :class="dotClasses" />
  <span
    v-else
    class="inline-flex w-fit max-w-full items-center gap-1.5 rounded-full border font-medium whitespace-nowrap"
    :class="[toneClasses, sizeClasses]"
  >
    <span v-if="dot" class="w-1.5 h-1.5 rounded-full" :class="dotClasses" />
    <slot />
  </span>
</template>
