<template>
  <UiSection v-if="items.length" padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-guarantees">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac2']">{{ t('aurora.guarantees.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.guarantees.title') }}
    </UiHeading>

    <UiBalancedGrid :count="items.length" class="mt-8" data-testid="aurora-guarantees-grid">
      <UiCard
        v-for="item in items"
        :key="item.key"
        tone="bare"
        :data-testid="`guarantee-${item.key}`"
        class="border-line bg-surf shadow-card"
      >
        <UiStack gap="sm">
          <UiTile size="lg" :icon="ICONS[item.key]" class="bg-tile-mint text-white" />
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-[18px] text-tx">{{ t(`aurora.guarantees.${item.key}Title`, { days: item.days ?? 0 }) }}</UiText>
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[15px] leading-relaxed text-mut">{{ t(`aurora.guarantees.${item.key}Body`, { days: item.days ?? 0 }) }}</UiText>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora guarantees: free migration, the money-back period and no hidden fees, each as a card
 * when the operator set it. With none set the section renders nothing.
 */
import type { Component } from 'vue'
import { BadgePercent, RotateCcw, Truck } from 'lucide-vue-next'
import { GuaranteeKey } from '~/constants/guaranteeKey'
import { useGuarantees } from '~/composables/useGuarantees'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import UiStack from '~/components/ui/UiStack.vue'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the active locale. */
const { t } = useI18n()

/** The guarantees that are set, in display order. */
const items = useGuarantees()

/** The tile icon of each guarantee. */
const ICONS: Record<GuaranteeKey, Component> = {
  [GuaranteeKey.FreeMigration]: Truck,
  [GuaranteeKey.MoneyBack]: RotateCcw,
  [GuaranteeKey.NoHiddenFees]: BadgePercent,
}
</script>
