<template>
  <UiSection v-if="USE_CASES.length" padding="lg" class="border-b border-nova-border bg-nova-surface">
    <NovaSectionHeader :eyebrow="t('nova.useCases.eyebrow')" :title="t('nova.useCases.title')" />

    <UiGrid :as="UiGridTag.Ul" cols="3" gap="lg" class="mt-10">
      <UiStack v-for="useCase in USE_CASES" :key="useCase.key" :as="UiStackTag.Li" gap="none" class="h-full">
        <UiCard
          padding="lg"
          class="flex h-full gap-4 transition-colors !border-nova-border !bg-nova-bg hover:!border-nova-brand"
        >
          <UiIconTile :icon="useCase.icon" size="lg" tone="nova" />
          <UiStack gap="none" class="min-w-0">
            <UiHeading :level="3" size="bare" class="text-[17px] font-bold text-nova-ink">
              {{ t(useCase.titleKey) }}
            </UiHeading>
            <UiText size="inherit" tone="inherit" class="mt-2 text-[15px] leading-relaxed text-nova-muted">
              {{ t(useCase.bodyKey) }}
            </UiText>
          </UiStack>
        </UiCard>
      </UiStack>
    </UiGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova use-cases grid. Adding an audience is one entry in `content.ts` and its
 * two i18n keys; this file does not change.
 */
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { USE_CASES } from '~/templates/nova/content'
import { UiGridTag, UiStackTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()
</script>
