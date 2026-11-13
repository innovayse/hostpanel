<template>
  <span :class="classes" data-testid="ui-tile">
    <component :is="icon" v-if="icon" :size="iconSize" :stroke-width="2" />
    <slot />
  </span>
</template>

<script setup lang="ts">
/**
 * UiTile: a small square (or round) tile that centres an icon or a short label.
 *
 * Structure by default: the screen paints the tile (`bg-brand text-on-brand`, a gradient token)
 * through its class, so one component serves brand marks, service tiles, avatars and count
 * badges. With a `tone` the tile carries the soft tint of that tone instead (status panels,
 * section markers).
 */
import { computed } from 'vue'
import type { Component } from 'vue'
import { UiTileSize } from '~/utils/ui/enums'
import type { UiTone } from '~/utils/ui/enums'
import { ICON_TONE_CLASSES } from '~/utils/ui/tones'

/** Props of UiTile. */
interface Props {
  /** Optional icon drawn in the tile; the default slot holds a text label instead. */
  icon?: Component
  /** `sm` is a count badge, `md` a 32px tile, `lg` a 40px tile, `xl` a 48px tile. */
  size?: `${UiTileSize}`
  /** Fully round instead of softly rounded. */
  rounded?: boolean
  /** Colour family of the tile; when omitted the caller paints the tile with its own classes. */
  tone?: `${UiTone}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  icon: undefined,
  size: UiTileSize.Md,
  rounded: false,
  tone: undefined,
})

/** Box utilities per size. */
const SIZE_CLASSES: Record<UiTileSize, string> = {
  [UiTileSize.Sm]: 'h-5 min-w-[1.25rem] px-1 text-[11px]',
  [UiTileSize.Md]: 'h-8 w-8 text-sm',
  [UiTileSize.Lg]: 'h-10 w-10 text-base',
  [UiTileSize.Xl]: 'h-12 w-12 text-lg',
}

/** Icon edge in pixels per size. */
const ICON_SIZES: Record<UiTileSize, number> = {
  [UiTileSize.Sm]: 12,
  [UiTileSize.Md]: 16,
  [UiTileSize.Lg]: 20,
  [UiTileSize.Xl]: 24,
}

/** Icon edge for the current size. */
const iconSize = computed(() => ICON_SIZES[props.size as UiTileSize])

/** Class list of the root element: shape, size and, when a tone is set, its tint. */
const classes = computed(() => [
  'ui-tile grid shrink-0 place-items-center font-medium',
  SIZE_CLASSES[props.size as UiTileSize],
  props.rounded ? 'rounded-full' : 'rounded-xl',
  props.tone ? ICON_TONE_CLASSES[props.tone as UiTone] : '',
].filter(Boolean).join(' '))
</script>
