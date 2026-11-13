<script setup lang="ts">
/**
 * Surface card that groups a block of a screen.
 *
 * Draws the frosted `glass` surface (translucent fill, hairline border, backdrop blur) with the card radius (or the larger panel radius). `padding` is optional: the default
 * `none` leaves spacing to the caller's classes (`p-5`, `mb-5`, `overflow-hidden`), which fall
 * through to the root, while `sm`/`md`/`lg` give the card its own inner padding.
 *
 * `loading` keeps the card where it will be: the `header` slot (the title row) stays, and the default
 * slot is swapped for placeholder lines, so the page keeps its layout while the data loads. The card is
 * marked `aria-busy`. The `footer` slot is the strip under the body (a pager, a total) with the standard divider and
 * padding; it stays while the body loads.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { UiCardRadius } from '@/utils/ui/card'
import type { UiCardPadding } from '@/utils/ui/card'

/** Props for UiCard. */
const props = withDefaults(defineProps<{
  /** Inner padding of the card. */
  padding?: UiCardPadding
  /** Corner radius of the card. */
  radius?: UiCardRadius
  /** Raises the card onto the denser frosted surface with a deep shadow (menus, popovers). */
  elevated?: boolean
  /** Shows placeholder lines in place of the body; the `header` slot stays. */
  loading?: boolean
  /** Number of placeholder lines drawn while loading. */
  skeletonLines?: number
}>(), {
  padding: 'none',
  radius: UiCardRadius.Card,
  elevated: false,
  loading: false,
  skeletonLines: 3,
})

/** Indexes of the placeholder lines, which key them. */
const lineIndexes = computed<number[]>(() => Array.from({ length: Math.max(1, props.skeletonLines) }, (_, index) => index))

/** Padding classes. */
const paddingClasses: Record<UiCardPadding, string> = {
  none: '',
  sm: 'p-3',
  md: 'p-5',
  lg: 'p-8',
}

/** Radius classes, literal so Tailwind emits them. */
const radiusClasses: Record<UiCardRadius, string> = {
  [UiCardRadius.Card]: 'rounded-card',
  [UiCardRadius.Panel]: 'rounded-panel',
}

/** Surface, radius and padding classes. */
const classes = computed<string[]>(() => [
  props.elevated ? 'glass-strong shadow-lg' : 'glass',
  radiusClasses[props.radius],
  paddingClasses[props.padding],
])
</script>

<template>
  <div :class="classes" :aria-busy="loading ? 'true' : undefined">
    <slot name="header" />
    <div v-if="loading" class="flex flex-col gap-3" :class="$slots.header ? 'mt-4' : ''" aria-hidden="true" data-testid="ui-card-skeleton">
      <UiSkeletonBar
        v-for="index in lineIndexes"
        :key="index"
        class="h-3.5 rounded-md"
        :class="index === lineIndexes.length - 1 && lineIndexes.length > 1 ? 'w-2/3' : 'w-full'"
      />
    </div>
    <slot v-else />
    <div v-if="$slots.footer" class="border-t border-border px-4 py-3" data-testid="ui-card-footer">
      <slot name="footer" />
    </div>
  </div>
</template>
