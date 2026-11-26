<script setup lang="ts">
/**
 * One record of a list, drawn as a card: the phone shape of a table row, also usable on its own for a list of records.
 *
 * It is a `UiCard`. The first line holds the `select` checkbox, the `title` (which takes the free width), the `badge` and the
 * row `actions` (the "…" trigger, or one icon button), each as tall as a finger needs; the `subtitle` is a muted line under it
 * and the default slot is the body, a stack of captioned field lines. `selected` marks the chosen record with the accent ring.
 * `loading` draws placeholder bars of the same parts instead (`skeleton` names which parts the card has and how many field
 * lines), so the list keeps its shape while the data loads. The card is a list item for assistive technology. On a phone the
 * `badge` leaves the first line for a row of its own under it, so a status pill never competes with the title for width.
 */
import { computed } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { UiCardPadding } from '@/utils/ui/card'
import type { UiRecordCardSkeleton } from '@/utils/ui/card'

/** Props for UiRecordCard. */
const props = withDefaults(defineProps<{
  /** Marks the record as chosen. */
  selected?: boolean
  /** Draws placeholders instead of the slots. */
  loading?: boolean
  /** The parts of the placeholder card; a heading alone when omitted. */
  skeleton?: UiRecordCardSkeleton
}>(), {
  selected: false,
  loading: false,
  /**
   * Default for the placeholder shape.
   *
   * @returns A shape with every part off, so instances never share one object.
   */
  skeleton: () => ({ select: false, subtitle: false, badge: false, actions: false, fields: 0 }),
})

/** Indexes of the placeholder field lines, which key them. */
const fieldIndexes = computed<number[]>(() => Array.from({ length: props.skeleton.fields }, (_, index) => index))

/** Ring that marks a chosen card. */
const selectedClasses = 'bg-tint-blue ring-1 ring-text-accent'
</script>

<template>
  <UiCard
    role="listitem"
    :padding="UiCardPadding.Sm"
    class="flex flex-col gap-1.5"
    :class="selected ? selectedClasses : ''"
    :aria-busy="loading ? 'true' : undefined"
  >
    <template v-if="loading">
      <div class="flex min-h-10 items-center gap-3" aria-hidden="true" data-testid="ui-record-card-skeleton">
        <UiSkeletonBar v-if="skeleton.select" class="size-5 rounded" />
        <UiSkeletonBar class="h-4 w-3/5 rounded" />
        <span class="flex-1" />
        <UiSkeletonBar v-if="skeleton.badge" class="h-5 w-16 rounded-full" />
        <UiSkeletonBar v-if="skeleton.actions" class="size-6 rounded" />
      </div>
      <UiSkeletonBar v-if="skeleton.subtitle" class="h-3 w-2/5 rounded" aria-hidden="true" />
      <div v-if="skeleton.fields > 0" class="flex flex-col gap-2" aria-hidden="true">
        <div v-for="index in fieldIndexes" :key="index" class="flex items-center justify-between gap-3">
          <UiSkeletonBar class="h-3 w-1/4 rounded" />
          <UiSkeletonBar class="h-3 w-1/3 rounded" />
        </div>
      </div>
    </template>
    <template v-else>
      <!-- The line wraps only on a phone, where the badge (last in order, a whole line wide) drops under the title and the actions. -->
      <div class="flex min-h-10 items-center gap-3 max-sm:flex-wrap max-sm:gap-y-1">
        <div v-if="$slots.select" class="flex shrink-0 items-center">
          <slot name="select" />
        </div>
        <div class="min-w-0 flex-1 break-words font-semibold text-text-primary">
          <slot name="title" />
        </div>
        <div v-if="$slots.badge" class="shrink-0 max-sm:order-last max-sm:basis-full">
          <slot name="badge" />
        </div>
        <div v-if="$slots.actions" class="flex shrink-0 items-center justify-end">
          <slot name="actions" />
        </div>
      </div>
      <div v-if="$slots.subtitle" class="min-w-0 break-words text-sm text-text-muted">
        <slot name="subtitle" />
      </div>
      <div v-if="$slots.default" class="flex flex-col gap-1.5">
        <slot />
      </div>
    </template>
  </UiCard>
</template>
