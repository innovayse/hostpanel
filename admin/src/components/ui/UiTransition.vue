<script setup lang="ts">
/**
 * The kit's open and close animation of one kind of surface. Wrap the element that carries `v-if` (or
 * `v-show`) in it; the classes come from `transitionSet`, so every dropdown, dialog, drawer and collapsible
 * block moves the same way, and nothing moves under reduced motion.
 */
import { transitionSet } from '@/utils/ui/transition'
import type { UiTransitionKind } from '@/utils/ui/transition'

/** Props for UiTransition. */
const props = defineProps<{
  /** Which kind of surface animates. */
  kind: UiTransitionKind
  /** Whether the enter also runs when the surface is already open on first render. */
  appear?: boolean
}>()
</script>

<template>
  <Transition v-bind="transitionSet[props.kind]" :appear="props.appear">
    <slot />
  </Transition>
</template>
