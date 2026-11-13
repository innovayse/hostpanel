<template>
  <NuxtLink :to="to" :data-testid="testid" :class="classes">
    <slot />
  </NuxtLink>
</template>

<script setup lang="ts">
/**
 * UiLinkCard: a whole card that is one link (a row of a list that opens its detail page).
 *
 * The root carries the `group` class, so content inside can react to hovering the card with
 * `group-hover:` utilities.
 */
import { computed } from 'vue'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiLinkCard. */
interface Props {
  /** In-app route the card opens. */
  to: string
  /** Draws the frame in the accent colour (an entry that still needs an action). */
  highlight?: boolean
  /** `data-testid` of the card. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  highlight: false,
  testid: undefined,
})

/** Class list of the card. */
const classes = computed(() => [
  'group block p-5 rounded-2xl bg-gray-50 dark:bg-white/5 border transition-all duration-200',
  FOCUS_RING,
  props.highlight
    ? 'border-blue-500/20 hover:border-blue-500/40 dark:border-blue-400/30 dark:hover:border-blue-400/50'
    : 'border-gray-200 dark:border-white/10 hover:border-primary-500/30 hover:bg-gray-100 dark:hover:bg-white/10',
].join(' '))
</script>
