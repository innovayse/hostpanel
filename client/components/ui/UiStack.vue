<template>
  <component :is="as" :class="classes">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiStack: lays its children out in a column or a row with a consistent gap.
 *
 * The replacement for a hand-written `flex flex-col gap-4` div. Gap, alignment and distribution
 * are enums, so spacing across the portal comes from one scale.
 */
import { computed } from 'vue'
import { UiCrossAlign, UiDirection, UiGap, UiJustify } from '~/utils/ui/enums'

/** Props of UiStack. */
interface Props {
  /** Tag to render. */
  as?: string
  /** Column (default) or row. */
  direction?: `${UiDirection}`
  /** Space between children. */
  gap?: `${UiGap}`
  /** Cross-axis alignment. */
  align?: `${UiCrossAlign}`
  /** Main-axis distribution. */
  justify?: `${UiJustify}`
  /** Lets a row wrap onto more lines. */
  wrap?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  as: 'div',
  direction: UiDirection.Column,
  gap: UiGap.Md,
  align: UiCrossAlign.Stretch,
  justify: UiJustify.Start,
  wrap: false,
})

/** Gap utility per step. */
const GAP_CLASSES: Record<UiGap, string> = {
  [UiGap.None]: 'gap-0',
  [UiGap.Xs]: 'gap-2',
  [UiGap.Sm]: 'gap-3',
  [UiGap.Md]: 'gap-4',
  [UiGap.Lg]: 'gap-6',
  [UiGap.Xl]: 'gap-10',
}

/** Align-items utility per alignment. */
const ALIGN_CLASSES: Record<UiCrossAlign, string> = {
  [UiCrossAlign.Start]: 'items-start',
  [UiCrossAlign.Center]: 'items-center',
  [UiCrossAlign.End]: 'items-end',
  [UiCrossAlign.Stretch]: 'items-stretch',
}

/** Justify-content utility per distribution. */
const JUSTIFY_CLASSES: Record<UiJustify, string> = {
  [UiJustify.Start]: 'justify-start',
  [UiJustify.Center]: 'justify-center',
  [UiJustify.End]: 'justify-end',
  [UiJustify.Between]: 'justify-between',
}

/** Class list of the root element. */
const classes = computed(() => [
  'flex',
  props.direction === UiDirection.Row ? 'flex-row' : 'flex-col',
  GAP_CLASSES[props.gap as UiGap],
  ALIGN_CLASSES[props.align as UiCrossAlign],
  JUSTIFY_CLASSES[props.justify as UiJustify],
  props.wrap ? 'flex-wrap' : '',
].filter(Boolean).join(' '))
</script>
