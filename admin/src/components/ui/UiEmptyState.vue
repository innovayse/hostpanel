<script setup lang="ts">
/**
 * "Nothing here" block for an empty list or an unconfigured area.
 *
 * The optional `icon` slot sits in a round frosted tile above the title; the optional `actions`
 * slot below the description holds the call to action. Both are left out of the layout when not
 * given, so a bare title and description do not carry empty gaps. `compact` trims the vertical padding and the
 * icon tile, for a block that stands inside a screen beside other content instead of filling it.
 */
import UiHeading from '@/components/ui/UiHeading.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for UiEmptyState. */
withDefaults(defineProps<{
  /** Headline (already translated). */
  title: string
  /** Explanation under the headline. */
  description?: string
  /** Smaller padding and icon tile. */
  compact?: boolean
}>(), {
  description: undefined,
  compact: false,
})
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-3 text-center" :class="compact ? 'py-6' : 'py-12'">
    <div
      v-if="$slots.icon"
      class="flex items-center justify-center rounded-full border border-border bg-surface-hover text-text-accent"
      :class="compact ? 'h-10 w-10' : 'h-14 w-14'"
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
