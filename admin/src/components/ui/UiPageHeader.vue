<script setup lang="ts">
/**
 * Header of a screen: title, optional subtitle, and an actions area on the right.
 *
 * The `actions` slot takes the screen's primary buttons and wraps onto further lines on a narrow screen instead of
 * overflowing it; the default slot renders under the title (breadcrumbs, filters summary). `loading` draws a
 * title bar (and a subtitle bar when a subtitle is passed) of the heading sizes instead of the text, for a title
 * that comes from data still loading; the actions stay where they will be. `subtitleLoading` is the smaller state of
 * the two: the title is known and drawn, and only a bar stands in for a subtitle that is made of data still loading.
 *
 * The header sits 22px (the concept's section gap) above the content on every screen: its bottom margin is 22px less the
 * gap of the stack it is in (`--stack-gap`, published by `gapClasses`), so a screen's own stack gap never adds to it.
 */
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for UiPageHeader. */
withDefaults(defineProps<{
  /** Screen title (already translated); may be left out while `loading`, when its bar stands in for it. */
  title?: string
  /** Line under the title. */
  subtitle?: string
  /** Shows placeholder bars in place of the title and the subtitle. */
  loading?: boolean
  /** Shows a placeholder bar in place of the subtitle only, the title being drawn as it is. */
  subtitleLoading?: boolean
}>(), {
  title: undefined,
  subtitle: undefined,
  loading: false,
  subtitleLoading: false,
})
</script>

<template>
  <header class="flex flex-wrap items-end justify-between gap-4 mb-[calc(1.375rem-var(--stack-gap,0rem))]" :aria-busy="loading || subtitleLoading ? 'true' : undefined">
    <div class="min-w-0">
      <div v-if="loading" aria-hidden="true" data-testid="ui-page-header-skeleton">
        <UiSkeletonBar class="h-9 w-64 max-w-full rounded-md" />
        <UiSkeletonBar v-if="subtitle" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" />
      </div>
      <template v-else>
        <UiHeading :level="1" variant="page">
          {{ title }}
        </UiHeading>
        <UiSkeletonBar v-if="subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" aria-hidden="true" data-testid="ui-page-header-subtitle-skeleton" />
        <UiText v-else-if="subtitle" tone="muted" size="md" class="mt-1.5">
          {{ subtitle }}
        </UiText>
      </template>
      <slot />
    </div>
    <div class="flex min-w-0 max-w-full flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>
