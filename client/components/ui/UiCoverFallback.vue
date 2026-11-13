<template>
  <div class="relative flex items-center justify-center overflow-hidden" data-testid="ui-cover-fallback">
    <!-- Soft dotted pattern and a pair of faint rings, all in the colour of the text. -->
    <UiDecor class="inset-0 opacity-20 [background-image:radial-gradient(currentColor_1.5px,transparent_1.5px)] [background-size:18px_18px]" />
    <UiDecor class="-right-10 -top-10 h-40 w-40 rounded-full border-[14px] border-current opacity-10" />
    <UiDecor class="-bottom-12 -left-8 h-36 w-36 rounded-full border-[12px] border-current opacity-10" />

    <UiStack align="center" gap="sm" class="relative">
      <UiText as="span" tone="inherit" size="inherit" class="font-display text-4xl font-bold tracking-wide" data-testid="ui-cover-fallback-initials">{{ initials }}</UiText>
      <component :is="icon" :size="22" :stroke-width="2" class="opacity-80" data-testid="ui-cover-fallback-icon" :data-site-type="siteType" />
    </UiStack>
  </div>
</template>

<script setup lang="ts">
/**
 * UiCoverFallback. The picture of a project that has none: a soft dotted pattern, the project's
 * initials and an icon for the kind of site it is.
 *
 * The caller paints the background and the text colour (`bg-brand text-on-brand`) and the size
 * through its classes; the pattern and the icon follow the text colour, so any template theme fits.
 */
import { computed } from 'vue'
import { SITE_TYPE_ICONS, initialsOf, siteTypeOf } from '~/utils/ui/siteType'

/** Props of UiCoverFallback. */
interface Props {
  /** The project's title; its first letters become the initials. */
  title: string
  /** The project's slug; decides the site type icon. */
  slug: string
}

/** The component props. */
const props = defineProps<Props>()

/** The initials drawn on the cover. */
const initials = computed(() => initialsOf(props.title))

/** What kind of site the project is. */
const siteType = computed(() => siteTypeOf(props.slug))

/** The icon of that site type. */
const icon = computed(() => SITE_TYPE_ICONS[siteType.value])
</script>
