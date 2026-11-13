<template>
  <div :class="frameClasses" data-testid="ui-embed">
    <iframe
      :src="src"
      :title="title"
      loading="lazy"
      allowfullscreen
      class="h-full w-full border-0"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * UiEmbed: a framed third-party page (a map, a video) inside the layout.
 *
 * `title` is required: a frame without one is announced by screen readers as "frame" and nothing
 * else. The frame is lazy-loaded so an embed below the fold costs nothing until it is reached.
 */
import { computed } from 'vue'
import { EMBED_HEIGHT_CLASSES, UiEmbedHeight } from '~/utils/ui/embed'

/** Props of UiEmbed. */
interface Props {
  /** Address of the page to show. */
  src: string
  /** Accessible name of the frame. */
  title: string
  /** Height step. */
  height?: `${UiEmbedHeight}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  height: UiEmbedHeight.Md,
})

/** Class list of the frame wrapper. */
const frameClasses = computed(() => [
  'relative w-full min-w-0 overflow-hidden rounded-xl border border-gray-200 bg-gray-50 dark:border-white/10 dark:bg-white/5 sm:rounded-2xl',
  EMBED_HEIGHT_CLASSES[props.height as UiEmbedHeight],
].join(' '))
</script>
