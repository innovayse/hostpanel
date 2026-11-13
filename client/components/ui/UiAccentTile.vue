<template>
  <div
    :class="boxClasses"
    data-testid="ui-accent-tile"
  >
    <Icon :name="icon" :class="iconClasses" />
  </div>
</template>

<script setup lang="ts">
/**
 * UiAccentTile: a square icon tile tinted with the colour of the nearest `UiAccent`.
 *
 * The tile is the product mark of the catalogue: a faint accent fill, a medium accent border, a
 * soft glow and the icon in the accent colour. The icon is a Nuxt Icon name (`mdi:cloud-check`).
 */
import { computed } from 'vue'
import { ACCENT_CLASSES, ACCENT_TILE_CLASSES, UiAccentTileSize } from '~/utils/ui/accent'

/** Props of UiAccentTile. */
interface Props {
  /** Nuxt Icon name, e.g. `mdi:school`. */
  icon: string
  /** Edge length of the tile. */
  size?: `${UiAccentTileSize}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  size: UiAccentTileSize.Md,
})

/** Class list of the square. */
const boxClasses = computed(() => [
  'ui-accent-tile flex items-center justify-center flex-shrink-0 transition-transform duration-500',
  ACCENT_TILE_CLASSES[props.size as UiAccentTileSize].box,
  ACCENT_CLASSES.tint,
  ACCENT_CLASSES.borderMedium,
  ACCENT_CLASSES.glow,
].join(' '))

/** Class list of the icon. */
const iconClasses = computed(() => [
  ACCENT_TILE_CLASSES[props.size as UiAccentTileSize].icon,
  ACCENT_CLASSES.text,
].join(' '))
</script>
