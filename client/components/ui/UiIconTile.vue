<template>
  <span :class="boxClasses" aria-hidden="true" data-testid="ui-icon-tile">
    <Icon :name="icon" :class="iconClasses" />
  </span>
</template>

<script setup lang="ts">
/**
 * UiIconTile: a small tinted square that carries an icon, the mark of a navigation row.
 *
 * Decorative: the row's text names the link, so the tile is hidden from assistive technology. The
 * resting `soft` tone fills in when the parent row (a `group`) is hovered or keyboard-focused. The
 * icon is a Nuxt Icon name (`mdi:server`).
 */
import { computed } from 'vue'
import { ICON_TILE_SIZE_CLASSES, ICON_TILE_TONE_CLASSES, UiIconTileSize, UiIconTileTone } from '~/utils/ui/iconTile'

/** Props of UiIconTile. */
interface Props {
  /** Nuxt Icon name, e.g. `mdi:server`. */
  icon: string
  /** Edge length of the tile. */
  size?: `${UiIconTileSize}`
  /** Colouring of the tile. */
  tone?: `${UiIconTileTone}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  size: UiIconTileSize.Md,
  tone: UiIconTileTone.Soft,
})

/** Class list of the square. */
const boxClasses = computed(() => [
  'flex shrink-0 items-center justify-center rounded-lg transition-colors',
  ICON_TILE_SIZE_CLASSES[props.size as UiIconTileSize].box,
  ICON_TILE_TONE_CLASSES[props.tone as UiIconTileTone],
].join(' '))

/** Class list of the icon. */
const iconClasses = computed(() => ICON_TILE_SIZE_CLASSES[props.size as UiIconTileSize].icon)
</script>
