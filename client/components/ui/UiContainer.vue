<template>
  <component :is="as" class="container-custom">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiContainer: the one shared page-width wrapper.
 *
 * Renders the `container-custom` class, defined once in `assets/css/main.css`
 * (`max-w-7xl mx-auto px-4 sm:px-6 lg:px-8`, i.e. 1280px with 16/24/32px gutters).
 * This is the same measure the main site uses, so the header, every page section
 * and the footer share identical left and right edges. Do not add a width or
 * padding override on or around it: a narrower reading column belongs on an inner
 * element, never on the container itself.
 */
import { UiBlockTag } from '~/utils/ui/enums'

/** Props of the container. */
interface Props {
  /** HTML tag to render; defaults to a plain `div`. */
  as?: `${UiBlockTag}`
}

withDefaults(defineProps<Props>(), {
  as: UiBlockTag.Div,
})
</script>
