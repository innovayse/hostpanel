<script setup lang="ts">
/**
 * A titled group of fields inside a {@link SettingsSection} card (for example Figures, Contacts, Messengers).
 *
 * It spans the whole row of the card's grid, carries a small heading and lays its own fields out in the given number of
 * columns, so a long card reads as a few short groups. Every subsection after the first is set off by a hairline.
 * The columns follow the width of the card, not of the screen. The root is a stack with `role="group"` named by its heading.
 */
import { useId } from 'vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiGap } from '@/utils/ui/layout'
import type { UiGridColumns } from '@/utils/ui/layout'
import { UiHeadingVariant } from '@/utils/ui/text'

/** Props for SettingsSubsection. */
withDefaults(defineProps<{
  /** Heading of the group (already translated). */
  title: string
  /** Column count of the fields once the card is wide enough; one column below that (the card is the `@container`). */
  columns?: UiGridColumns
}>(), {
  columns: 2,
})

/** Id of the heading, which names the group. */
const titleId = useId()
</script>

<template>
  <UiStack
    role="group"
    :gap="UiGap.Sm"
    :aria-labelledby="titleId"
    class="col-span-full border-t border-border pt-3 first:border-t-0 first:pt-0"
  >
    <UiHeading :id="titleId" :level="3" :variant="UiHeadingVariant.Card">
      {{ title }}
    </UiHeading>
    <UiGrid :columns="columns" :gap="UiGap.Sm" container class="gap-x-4">
      <slot />
    </UiGrid>
  </UiStack>
</template>
