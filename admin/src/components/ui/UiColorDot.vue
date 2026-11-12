<script setup lang="ts">
/**
 * A small round dot in a colour that is only known at run time (a client group's colour).
 *
 * A Tailwind class cannot carry a colour the operator typed and an inline `style` is not allowed,
 * so the dot is an SVG whose `fill` attribute takes the value. It is decorative: the text beside
 * it names the thing, so it is hidden from assistive technology.
 *
 * Why not `UiColorSwatch`: the swatch draws a rectangle, never a circle (its corners are rounded only
 * when it carries a label), its height is fixed at `h-8` and its width is the full row or sized to
 * its label. It cannot be a 10px round marker inside a badge, and overriding its size with extra
 * classes would fight the ones it sets.
 */

/** Props for UiColorDot. */
defineProps<{
  /** Fill of the dot, as a CSS colour such as `#3B82F6`. */
  color: string
}>()
</script>

<template>
  <svg class="block h-2.5 w-2.5 shrink-0" viewBox="0 0 10 10" aria-hidden="true">
    <circle cx="5" cy="5" r="5" :fill="color" />
  </svg>
</template>
