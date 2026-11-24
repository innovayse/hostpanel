<template>
  <div
    v-if="isMultiLine"
    class="space-y-2"
    role="status"
    :aria-label="$t('ui.loading')"
    data-testid="ui-skeleton"
  >
    <div
      v-for="index in lines"
      :key="index"
      :class="[BASE_CLASSES, 'h-4', index === lines ? 'w-2/3' : 'w-full']"
      data-testid="ui-skeleton-line"
    />
  </div>
  <div
    v-else
    role="status"
    :aria-label="$t('ui.loading')"
    data-testid="ui-skeleton"
    :class="[BASE_CLASSES, shapeClasses]"
  />
</template>

<script setup lang="ts">
/**
 * UiSkeleton: a pulsing placeholder shown while content loads.
 *
 * `line` is a text line (several with `lines`, the last one shorter), `block` fills the width and
 * a height you give by class (for example `h-32`), `circle` is a round avatar-sized shape. Size a
 * block or circle from the outside with `class`.
 */
import { computed } from 'vue'
import { UiSkeletonShape, UiSkeletonWidth } from '~/utils/ui/enums'

/** Props of UiSkeleton. */
interface Props {
  /** Placeholder shape. */
  shape?: `${UiSkeletonShape}`
  /** Number of text lines when the shape is a line. */
  lines?: number
  /** Width of a single line or block; a circle ignores it. */
  width?: `${UiSkeletonWidth}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  shape: UiSkeletonShape.Line,
  lines: 1,
  width: UiSkeletonWidth.Full,
})

/** Whether several text lines are drawn. */
const isMultiLine = computed(() => props.shape === UiSkeletonShape.Line && props.lines > 1)

/** Colour and animation shared by every placeholder. */
const BASE_CLASSES = 'animate-pulse bg-gray-200 dark:bg-white/10'

/** Width utility per step, written out so Tailwind emits them. */
const WIDTH_CLASSES: Record<`${UiSkeletonWidth}`, string> = {
  [UiSkeletonWidth.Full]: 'w-full',
  [UiSkeletonWidth.ThreeQuarters]: 'w-3/4',
  [UiSkeletonWidth.Heading]: 'w-64 max-w-full',
}

/** Shape utilities for a single placeholder. */
const shapeClasses = computed(() => {
  if (props.shape === UiSkeletonShape.Circle) {
    return 'h-10 w-10 rounded-full'
  }
  if (props.shape === UiSkeletonShape.Block) {
    return `${WIDTH_CLASSES[props.width]} rounded-xl`
  }
  return `h-4 ${WIDTH_CLASSES[props.width]} rounded`
})
</script>
