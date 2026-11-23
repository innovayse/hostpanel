<script setup lang="ts">
/**
 * Shell of the dashboard chart cards: the panel card with its title row (a title, an optional note under it and an
 * `aside` slot on the right for the headline figure) and the chart in the default slot. The title row stays while
 * the data loads, so every card keeps its place.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiCardPadding, UiCardRadius } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for DashboardChartCard. */
defineProps<{
  /** Title of the card (already translated). */
  title: string
  /** Line under the title explaining what the chart shows (already translated). */
  note?: string
}>()
</script>

<template>
  <UiCard :padding="UiCardPadding.None" :radius="UiCardRadius.Panel" class="p-5.5">
    <UiStack :gap="UiGap.Md">
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Start" :justify="UiJustify.Between">
        <UiStack :gap="UiGap.None">
          <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ title }}</UiHeading>
          <UiText v-if="note" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ note }}</UiText>
        </UiStack>
        <slot name="aside" />
      </UiStack>
      <slot />
    </UiStack>
  </UiCard>
</template>
