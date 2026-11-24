<template>
  <UiStack
    gap="none"
    align="center"
    class="text-center animate-reveal-up motion-reduce:animate-none"
    data-testid="section-header"
  >
    <UiBadge v-if="badge" variant="primary" rounded class="px-4 py-1.5 mb-4" data-testid="section-header-badge">
      {{ badge }}
    </UiBadge>
    <UiHeading :level="2" class="text-3xl md:text-5xl mb-4" data-testid="section-header-title">
      <slot name="title">{{ title }}</slot>
    </UiHeading>
    <UiText v-if="subtitle" tone="muted" size="lg" class="max-w-[75ch] mx-auto" data-testid="section-header-subtitle">
      {{ subtitle }}
    </UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The header every marketing section opens with: a pill label, a large title and a lead line.
 *
 * The title is plain text through `title`, or rich (a highlighted word) through the `title` slot.
 * The block fades up when it scrolls into view. Spacing below it is the caller's (`class="mb-12"`).
 */

/** Props of MarketingSectionHeader. */
interface Props {
  /** Small pill above the title. */
  badge?: string
  /** Plain-text title; ignored when the `title` slot is given. */
  title?: string
  /** Lead line under the title. */
  subtitle?: string
}

withDefaults(defineProps<Props>(), {
  badge: undefined,
  title: undefined,
  subtitle: undefined,
})
</script>
