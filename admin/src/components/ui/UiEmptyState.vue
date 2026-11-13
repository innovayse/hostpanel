<script setup lang="ts">
/**
 * "Nothing here" block for an empty list or an unconfigured area.
 *
 * The optional `icon` slot sits in a round frosted tile above the title; the optional `actions`
 * slot below the description holds the call to action. Both are left out of the layout when not
 * given, so a bare title and description do not carry empty gaps.
 */
import UiHeading from '@/components/ui/UiHeading.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for UiEmptyState. */
defineProps<{
  /** Headline (already translated). */
  title: string
  /** Explanation under the headline. */
  description?: string
}>()
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-3 py-12 text-center">
    <div
      v-if="$slots.icon"
      class="flex h-14 w-14 items-center justify-center rounded-full border border-border bg-surface-hover text-text-accent"
      data-testid="ui-empty-state-icon"
    >
      <slot name="icon" />
    </div>
    <UiHeading :level="3" variant="card">
      {{ title }}
    </UiHeading>
    <UiText v-if="description" tone="secondary" size="md" class="max-w-md">
      {{ description }}
    </UiText>
    <div v-if="$slots.actions" class="mt-1 flex items-center gap-2" data-testid="ui-empty-state-actions">
      <slot name="actions" />
    </div>
  </div>
</template>
