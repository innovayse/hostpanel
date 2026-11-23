<template>
  <UiSection v-if="guarantees.length" :contained="false" padding="lg" class="bg-classic-surface border-y border-classic-border" data-testid="sections-guarantees">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.guarantees.badge')"
        :title="$t('hostingSections.guarantees.title')"
        :subtitle="$t('hostingSections.guarantees.subtitle')"
      />

      <UiBalancedGrid :count="guarantees.length" data-testid="sections-guarantees-grid">
        <ClassicCard
          v-for="guarantee in guarantees"
          :key="guarantee.key"
          hover
          :data-testid="`guarantee-${guarantee.key}`"
        >
          <UiStack gap="sm">
            <ClassicIconTile :icon="GUARANTEE_ICONS[guarantee.key]" />
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ $t(`hostingSections.guarantees.items.${guarantee.key}.title`, { days: guarantee.days ?? 0 }) }}</UiHeading>
            <UiText tone="inherit" size="sm" class="text-classic-muted">{{ $t(`hostingSections.guarantees.items.${guarantee.key}.description`, { days: guarantee.days ?? 0 }) }}</UiText>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Guarantees section: free migration, the money-back period and no hidden fees, each as a card
 * when the operator set it. With none set the section renders nothing.
 */
import { GuaranteeKey } from '~/constants/guaranteeKey'

/** The guarantees on offer, in display order. */
const guarantees = useGuarantees()

/** Icon of each guarantee. */
const GUARANTEE_ICONS: Record<GuaranteeKey, string> = {
  [GuaranteeKey.FreeMigration]: 'mdi:truck-fast-outline',
  [GuaranteeKey.MoneyBack]: 'mdi:cash-refund',
  [GuaranteeKey.NoHiddenFees]: 'mdi:receipt-text-check-outline',
}
</script>
