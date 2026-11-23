<script setup lang="ts">
/**
 * One filled block of a key (a legend): a small square for one entry, or a step of a colour scale.
 *
 * The fill is a Tailwind background class chosen by the screen (a literal from its own scale), so the block follows
 * the theme. It is decorative: the caption beside it names the thing, so it is hidden from assistive technology.
 */
import { legendSwatchClasses, UiLegendSwatchShape } from '@/utils/ui/svgMap'

/** Props for UiLegendSwatch. */
withDefaults(defineProps<{
  /** Tailwind background class of the block, e.g. `bg-border`. */
  fill: string
  /** Shape of the block. */
  shape?: UiLegendSwatchShape
}>(), {
  shape: UiLegendSwatchShape.Square,
})
</script>

<template>
  <span class="block" :class="[legendSwatchClasses[shape], fill]" aria-hidden="true" />
</template>
