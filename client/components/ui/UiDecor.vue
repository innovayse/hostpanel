<template>
  <component :is="as" aria-hidden="true" data-testid="ui-decor" class="ui-decor absolute pointer-events-none">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiDecor. A decorative, non-interactive layer: a glow, a floating dot, a corner frame, a line.
 *
 * It is absolutely positioned inside the nearest positioned ancestor, never takes pointer events
 * and is hidden from assistive technology. Position, size, colour, blur and motion come from the
 * classes the caller passes; the component only guarantees the three properties above, so a
 * screen never draws a bare `div` for ornament and the "card-styled div" rule stays meaningful.
 */

/** Props of UiDecor. */
interface Props {
  /** Tag to render. */
  as?: string
}

withDefaults(defineProps<Props>(), {
  as: 'div',
})
</script>
