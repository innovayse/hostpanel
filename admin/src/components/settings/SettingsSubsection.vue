<script setup lang="ts">
/**
 * A titled group of fields inside a {@link SettingsSection} card (for example Figures, Contacts, Messengers).
 *
 * It spans the whole row of the card's grid, carries a small heading and lays its own fields out in the given number of
 * columns, so a long card reads as a few short groups. Every subsection after the first is set off by a hairline.
 * The root is a `<div role="group">` named by its heading.
 */
import { useId } from 'vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import type { UiGridColumns } from '@/utils/ui/layout'

/** Props for SettingsSubsection. */
withDefaults(defineProps<{
  /** Heading of the group (already translated). */
  title: string
  /** Column count of the fields from `md` up; one column on phones. */
  columns?: UiGridColumns
}>(), {
  columns: 2,
})

/** Id of the heading, which names the group. */
const titleId = useId()
</script>

<template>
  <div
    role="group"
    :aria-labelledby="titleId"
    class="col-span-full flex flex-col gap-3 border-t border-border pt-3 first:border-t-0 first:pt-0"
  >
    <UiHeading :id="titleId" :level="3" variant="card">
      {{ title }}
    </UiHeading>
    <UiGrid :columns="columns" gap="sm" class="gap-x-4">
      <slot />
    </UiGrid>
  </div>
</template>
