<template>
  <component :is="tag" :class="classes">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiHeading: a heading whose semantic level and visual size are chosen separately.
 *
 * `level` picks h1 to h6 (what assistive technology and search engines read); `size` picks how
 * it looks, defaulting to the size that normally goes with that level. A card title can be an
 * h2 for the outline yet look like a subsection title.
 */
import { computed } from 'vue'
import { UiHeadingLevel, UiHeadingSize } from '~/utils/ui/enums'

/** Props of UiHeading. */
interface Props {
  /** Semantic level, 1 to 6. */
  level?: UiHeadingLevel
  /** Visual size; defaults from the level. */
  size?: `${UiHeadingSize}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  level: UiHeadingLevel.H2,
  size: undefined,
})

/** Size that goes with each level when none is given. */
const DEFAULT_SIZES: Record<UiHeadingLevel, UiHeadingSize> = {
  [UiHeadingLevel.H1]: UiHeadingSize.Title,
  [UiHeadingLevel.H2]: UiHeadingSize.Section,
  [UiHeadingLevel.H3]: UiHeadingSize.Subsection,
  [UiHeadingLevel.H4]: UiHeadingSize.Subsection,
  [UiHeadingLevel.H5]: UiHeadingSize.Label,
  [UiHeadingLevel.H6]: UiHeadingSize.Label,
}

/** Type utilities per visual size. */
const SIZE_CLASSES: Record<UiHeadingSize, string> = {
  [UiHeadingSize.Display]: 'text-4xl md:text-6xl font-bold tracking-tight text-gray-900 dark:text-white',
  [UiHeadingSize.Title]: 'text-3xl md:text-4xl font-bold text-gray-900 dark:text-white',
  [UiHeadingSize.Section]: 'text-2xl md:text-3xl font-bold text-gray-900 dark:text-white',
  [UiHeadingSize.Subsection]: 'text-lg font-semibold text-gray-900 dark:text-white',
  [UiHeadingSize.Label]: 'text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400',
  [UiHeadingSize.Bare]: '',
}

/** The element to render, h1 to h6. */
const tag = computed(() => 'h' + String(Number(props.level)))

/** Class list of the heading. */
const classes = computed(() =>
  SIZE_CLASSES[(props.size as UiHeadingSize | undefined) ?? DEFAULT_SIZES[Number(props.level) as UiHeadingLevel]])
</script>
