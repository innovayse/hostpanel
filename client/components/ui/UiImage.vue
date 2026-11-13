<template>
  <img
    v-if="unoptimised"
    :src="src"
    :alt="alt"
    :width="width"
    :height="height"
    :loading="loading"
    :data-testid="testid"
    :class="classes"
  >
  <NuxtImg
    v-else
    :src="src"
    :alt="alt"
    :width="width"
    :height="height"
    :loading="loading"
    :data-testid="testid"
    :class="classes"
  />
</template>

<script setup lang="ts">
/**
 * UiImage. The portal's image: optimised through NuxtImg, always with an `alt`.
 *
 * `alt` is required; pass an empty string only for a purely decorative image. Fit and corner
 * rounding are enums so a screen never hand-writes object-fit or radius classes.
 */
import { computed } from 'vue'
import { UiImageFit, UiImageLoading, UiImageRounding } from '~/utils/ui/enums'

/** Props of UiImage. */
interface Props {
  /** Image source. */
  src: string
  /** Alternative text; empty for a decorative image. */
  alt: string
  /** Intrinsic width in px (also sets the optimised size). */
  width?: number | string
  /** Intrinsic height in px. */
  height?: number | string
  /** Lazy by default; eager for above-the-fold images. */
  loading?: `${UiImageLoading}`
  /** How the image fills its box. */
  fit?: `${UiImageFit}`
  /** Corner rounding. */
  rounded?: `${UiImageRounding}`
  /** Renders a plain `img` for a URL the image optimiser cannot fetch, such as an upload proxied from the API. */
  unoptimised?: boolean
  /** `data-testid` of the image. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  width: undefined,
  height: undefined,
  loading: UiImageLoading.Lazy,
  fit: UiImageFit.Cover,
  rounded: UiImageRounding.None,
  unoptimised: false,
  testid: undefined,
})

/** Object-fit utility per fit. */
const FIT_CLASSES: Record<UiImageFit, string> = {
  [UiImageFit.Cover]: 'object-cover',
  [UiImageFit.Contain]: 'object-contain',
}

/** Radius utility per rounding. */
const ROUNDING_CLASSES: Record<UiImageRounding, string> = {
  [UiImageRounding.None]: '',
  [UiImageRounding.Md]: 'rounded-xl',
  [UiImageRounding.Full]: 'rounded-full',
}

/** Class list of the image. */
const classes = computed(() => [
  FIT_CLASSES[props.fit as UiImageFit],
  ROUNDING_CLASSES[props.rounded as UiImageRounding],
].filter(Boolean).join(' '))
</script>
