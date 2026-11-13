<template>
  <component :is="as" class="ui-gradient-text bg-clip-text text-transparent" data-testid="ui-gradient-text">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiGradientText: text painted by a background gradient.
 *
 * The component only turns the text transparent and clips the background to it; the caller
 * supplies the gradient itself as classes (`bg-gradient-to-r from-cyan-400 to-primary-400`).
 * It exists because `UiText` always sets a text colour, and a text colour would paint over the
 * clipped gradient.
 */

/** Props of UiGradientText. */
interface Props {
  /** Tag to render; a span by default so the text can sit inside a line. */
  as?: string
}

withDefaults(defineProps<Props>(), {
  as: 'span',
})
</script>
