<script setup lang="ts">
/**
 * Image.
 *
 * `fit` decides how the picture fills its box and `rounded` the corner radius. `alt` defaults to
 * empty (decorative); pass real text when the picture carries meaning. Other attributes
 * (`width`, `height`, classes) land on the native `<img>`. With `fallback` and no `src` the picture is
 * replaced by a framed box of the same size holding an image icon, so a list of thumbnails keeps its rhythm.
 */
import { computed } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import type { UiImageFit, UiImageRounded } from '@/utils/ui/image'

/** Props for UiImage. */
const props = withDefaults(defineProps<{
  /** Address of the picture. */
  src?: string
  /** Alternative text; empty marks the picture decorative. */
  alt?: string
  /** How the picture fills its box. */
  fit?: UiImageFit
  /** Corner radius. */
  rounded?: UiImageRounded
  /** Draws the framed icon box instead of the picture while there is no `src`. */
  fallback?: boolean
}>(), {
  src: undefined,
  alt: '',
  fit: 'none',
  rounded: 'none',
  fallback: false,
})

/** Fit classes. */
const fitClasses: Record<UiImageFit, string> = {
  cover: 'object-cover',
  contain: 'object-contain',
  none: '',
}

/** Radius classes. */
const roundedClasses: Record<UiImageRounded, string> = {
  none: '',
  md: 'rounded-control',
  lg: 'rounded-card',
  full: 'rounded-full',
}

/** Classes of the chosen fit and radius. */
const classes = computed<string[]>(() => [fitClasses[props.fit], roundedClasses[props.rounded]])
</script>

<template>
  <div
    v-if="fallback && !src"
    class="flex items-center justify-center border border-border bg-surface-field text-text-muted"
    :class="classes"
    data-testid="ui-image-fallback"
  >
    <UiIcon :markup="Icon.Image" size="sm" />
  </div>
  <img v-else :src="src" :alt="alt" :class="classes">
</template>
