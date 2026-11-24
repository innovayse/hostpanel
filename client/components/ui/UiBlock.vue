<template>
  <component :is="as">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiBlock: a plain element in normal flow, with no layout of its own.
 *
 * The kit's stand-in for a bare `div` or a landmark (`header`, `footer`, `main`) that only carries
 * the caller's classes: a frame, a gutter, a position. Anything that lays children out in a row, a
 * column or a grid is a UiStack, UiGrid or UiSplit instead.
 */
import { UiBlockTag } from '~/utils/ui/enums'

/** Props of UiBlock. */
interface Props {
  /** Tag to render. */
  as?: `${UiBlockTag}`
}

withDefaults(defineProps<Props>(), {
  as: UiBlockTag.Div,
})
</script>
