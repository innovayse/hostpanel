<template>
  <UiCard tone="bare" :padding="padding" :class="classes">
    <slot />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ClassicCard. The classic template's card: a white (charcoal in dark mode) surface, a hairline
 * border, a modest radius and a restrained shadow. It wraps the kit's bare `UiCard`, which keeps
 * the border width and padding and leaves every colour to the caller; the important modifier on
 * the radius beats the bare card's larger one.
 */
import { computed } from 'vue'
import { UiCardPadding } from '~/utils/ui/enums'

/** Props of ClassicCard. */
interface Props {
  /** Inner padding step of the card. */
  padding?: `${UiCardPadding}`
  /** Darkens the border and lifts the shadow on hover. */
  hover?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  padding: UiCardPadding.Lg,
  hover: false,
})

/** Class list of the card. */
const classes = computed(() => [
  '!rounded-lg border-classic-border bg-classic-surface shadow-sm',
  props.hover ? 'transition-shadow duration-200 hover:border-classic-brand/40 hover:shadow-md' : '',
].filter(Boolean).join(' '))
</script>
