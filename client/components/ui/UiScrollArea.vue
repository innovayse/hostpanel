<template>
  <div ref="root" tabindex="0" class="overflow-y-auto">
    <slot />
  </div>
</template>

<script setup lang="ts">
/**
 * UiScrollArea: a vertically scrolling, keyboard-focusable region that hands its element to the
 * parent, for a log or a list whose scroll position the screen follows (a chat transcript).
 *
 * Size and height limits come from the caller's classes (a `max-h-*`), so the content below it (a composer)
 * stays in view. ARIA attributes and the `scroll` listener fall through to the element. `v-model:element` is
 * the element itself, set once it is mounted and cleared when it goes, so a composable that measures and moves
 * the scroll position can take it as its ref.
 */
import { onBeforeUnmount, onMounted, useTemplateRef } from 'vue'

/** The scrolling element, null until mounted and after unmount. */
const element = defineModel<HTMLElement | null>('element', { default: null })

/** Template ref of the root element. */
const root = useTemplateRef<HTMLElement>('root')

/** Hands the mounted element to the parent. */
onMounted(() => {
  element.value = root.value
})

/** Takes the element back so the parent never holds a detached node. */
onBeforeUnmount(() => {
  element.value = null
})
</script>
