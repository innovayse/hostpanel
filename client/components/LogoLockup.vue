<template>
  <Logo v-if="uploaded" :img-class="imgClass" />
  <template v-else>
    <!-- Decorative: the site name beside it is the accessible name. -->
    <UiImage
      :src="STATIC_LOGO_MARK"
      alt=""
      :width="36"
      :height="36"
      :loading="UiImageLoading.Eager"
      :fit="UiImageFit.Contain"
      testid="app-logo-mark"
      :class="markClass"
      aria-hidden="true"
    />
    <UiText :as="UiTextTag.Span" tone="inherit" size="inherit" data-testid="app-logo-name" :class="nameClass">{{ name }}</UiText>
  </template>
</template>

<script setup lang="ts">
/**
 * The brand lockup a template header or footer draws: the operator's uploaded wordmark when
 * there is one, otherwise the bundled cube mark beside the operator's site name.
 *
 * The mark plus the name rather than the bundled wordmark, so an install that renamed itself
 * (a demo, an operator who has not uploaded a logo yet) still reads as its own name. The one
 * place that rule lives; the templates only size it.
 */
import Logo from '~/components/Logo.vue'
import { STATIC_LOGO_MARK } from '~/utils/appLogo'
import { UiImageFit, UiImageLoading, UiTextTag } from '~/utils/ui/enums'

/** Props of the lockup with their defaults applied. */
withDefaults(
  defineProps<{
    /** Tailwind classes for the uploaded wordmark. */
    imgClass?: string
    /** Tailwind classes for the bundled mark. */
    markClass?: string
    /** Tailwind classes for the site name beside the mark. */
    nameClass?: string
  }>(),
  { imgClass: '', markClass: 'h-9 w-9', nameClass: '' },
)

/** The uploaded wordmark (`''` when none; it already falls back to the dark upload). */
const { light } = useBrandLogo()
/** The operator's site name, never empty. */
const { name } = useSiteIdentity()

/** Whether the operator uploaded a wordmark, which then wins over the bundled mark. */
const uploaded = computed(() => Boolean(light.value))
</script>
