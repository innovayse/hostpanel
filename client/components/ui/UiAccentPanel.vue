<template>
  <div :class="classes" data-testid="ui-accent-panel">
    <slot />
  </div>
</template>

<script setup lang="ts">
/**
 * UiAccentPanel: a pricing-style panel that is neutral by default and takes the colour of the
 * nearest `UiAccent` for its frame and glow when it is `highlighted` (the recommended plan).
 */
import { computed } from 'vue'
import { ACCENT_CLASSES } from '~/utils/ui/accent'

/** Props of UiAccentPanel. */
interface Props {
  /** Draws the accent frame, tint and glow and enlarges the panel on wide screens. */
  highlighted?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  highlighted: false,
})

/** Class list of the root element. */
const classes = computed(() => [
  'ui-accent-panel relative flex flex-col h-full p-6 rounded-2xl border-2 transition-all duration-300 hover:-translate-y-2',
  props.highlighted
    ? `md:scale-105 shadow-[0_0_50px_color-mix(in_srgb,var(--accent)_21%,transparent)] ${ACCENT_CLASSES.border} ${ACCENT_CLASSES.tint}`
    : 'border-gray-200 dark:border-white/10 bg-white dark:bg-white/[0.03]',
].join(' '))
</script>
