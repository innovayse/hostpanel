<script setup lang="ts">
/**
 * Preview of an uploaded picture, drawn on a neutral checkerboard that an icon button flips between light and dark.
 *
 * A logo or an illustration is judged on the stage it will be seen on, not on the theme's card: a dark SVG vanishes on
 * the dark card and a white one on the light card. The stage is a transparency checkerboard (light by default), so the
 * picture always reads on one of the two. The box takes its size from the caller's classes; the picture is contained
 * in it unless `fit` says otherwise. The flip button sits on the box's bottom-left corner and always shows, because a
 * touch screen has no hover to reveal it; the caller keeps any other overlay on the opposite corner.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiImage from '@/components/ui/UiImage.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiImageBackdrop, UiImageFit } from '@/utils/ui/image'
import { UiSize } from '@/utils/ui/size'

/** Resolves the flip button's accessible names. */
const { t } = useI18n()

/** Props for UiImagePreview. */
withDefaults(defineProps<{
  /** Address of the picture. */
  src: string
  /** Alternative text of the picture (already translated). */
  alt: string
  /** How the picture fills the box. */
  fit?: UiImageFit
  /** `data-testid` of the picture itself; the box takes the caller's own `data-testid`. */
  imageTestid?: string
}>(), {
  fit: UiImageFit.Contain,
  imageTestid: 'ui-image-preview-image',
})

/** Classes of the stage each backdrop paints (the checkerboard utilities of `main.css`). */
const BACKDROP_CLASSES: Record<UiImageBackdrop, string> = {
  [UiImageBackdrop.Light]: 'checkerboard-light',
  [UiImageBackdrop.Dark]: 'checkerboard-dark',
}

/** The stage the picture is on now. */
const backdrop = ref<UiImageBackdrop>(UiImageBackdrop.Light)

/** The stage the flip button switches to, which also names its label and icon. */
const otherBackdrop = computed<UiImageBackdrop>(() => (backdrop.value === UiImageBackdrop.Light ? UiImageBackdrop.Dark : UiImageBackdrop.Light))

/** Accessible name of the flip button: the stage it switches to. */
const flipLabel = computed<string>(() => (otherBackdrop.value === UiImageBackdrop.Dark ? t('ui.imagePreview.showOnDark') : t('ui.imagePreview.showOnLight')))

/** Icon of the flip button: the moon for the dark stage, the sun for the light one. */
const flipIcon = computed<string>(() => (otherBackdrop.value === UiImageBackdrop.Dark ? Icon.Moon : Icon.Sun))

/** Switches the picture to the other stage. */
const flip = (): void => {
  backdrop.value = otherBackdrop.value
}
</script>

<template>
  <div class="relative overflow-hidden" :class="BACKDROP_CLASSES[backdrop]">
    <UiImage :src="src" :alt="alt" :fit="fit" class="h-full w-full" :data-testid="imageTestid" />
    <UiIconButton
      :label="flipLabel"
      :variant="UiButtonVariant.Secondary"
      :size="UiSize.Sm"
      class="absolute bottom-1 left-1"
      data-testid="ui-image-preview-flip"
      @click="flip"
    >
      <UiIcon :markup="flipIcon" :size="UiSize.Sm" />
    </UiIconButton>
  </div>
</template>
