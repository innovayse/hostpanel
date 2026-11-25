<script setup lang="ts">
/**
 * The panel's wordmark in the variant that reads on the current theme.
 *
 * Both images are rendered and the `light:` variant shows one: the dark-background logo
 * by default (the panel is dark unless `<html class="light">`), the light-background logo
 * in the light theme. A CSS swap rather than a computed `src`, so the logo can never
 * disagree with the palette, which keys on the same class.
 *
 * The operator's uploads win; one upload is used in both themes (see the branding store);
 * with none, the built-in `logo-dark.svg` / `logo.svg` pair is shown.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBrandingStore } from '@/stores/branding'
import UiImage from '@/components/ui/UiImage.vue'

/** Props of the logo. */
withDefaults(defineProps<{
  /** Size classes for both images. */
  imgClass?: string
}>(), { imgClass: 'h-7 w-auto' })

/** Translation function, for the alt text. */
const { t } = useI18n()

/** The operator's logo URLs. */
const branding = useBrandingStore()

/** Logo for the dark theme: the operator's, else the built-in light-text wordmark. */
const darkSrc = computed<string>(() => branding.logoOnDarkUrl || '/logo-dark.svg')

/** Logo for the light theme: the operator's, else the built-in dark-text wordmark. */
const lightSrc = computed<string>(() => branding.logoOnLightUrl || '/logo.svg')

/**
 * A light plate behind the dark-theme image when the operator's only upload is the
 * light-background logo, which would otherwise sit on the dark sidebar unreadable. A dark
 * variant, or no upload at all (the built-in light-text wordmark), needs none.
 */
const darkPlate = computed<string>(() =>
  branding.logoUrl && !branding.logoDarkUrl ? 'rounded-md bg-surface-paper/90 px-2 py-1' : '',
)
</script>

<template>
  <UiImage :src="darkSrc" :alt="t('shared.brandName')" :class="[imgClass, darkPlate, 'light:hidden']" data-testid="brand-logo-dark" />
  <UiImage :src="lightSrc" :alt="t('shared.brandName')" :class="[imgClass, 'hidden light:block']" data-testid="brand-logo-light" />
</template>
