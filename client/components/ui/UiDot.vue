<template>
  <span :class="classes" aria-hidden="true" data-testid="ui-dot" />
</template>

<script setup lang="ts">
/**
 * UiDot: a small round status dot, decorative only.
 *
 * Structure by default: the screen paints it with a background colour class (`bg-ok`, a status
 * token), so one dot serves every availability and presence marker.
 */
import { computed } from 'vue'
import { UiDotSize } from '~/utils/ui/enums'

/** Props of UiDot. */
interface Props {
  /** Edge length of the dot. */
  size?: `${UiDotSize}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  size: UiDotSize.Md,
})

/** Box utilities per size. */
const SIZE_CLASSES: Record<`${UiDotSize}`, string> = {
  [UiDotSize.Sm]: 'h-[7px] w-[7px]',
  [UiDotSize.Md]: 'h-2 w-2',
  [UiDotSize.Lg]: 'h-[9px] w-[9px]',
}

/** Class list of the root element. */
const classes = computed(() => `inline-block shrink-0 rounded-full ${SIZE_CLASSES[props.size]}`)
</script>
