<template>
  <UiSection :id="HomeSectionId.Process" :contained="false" padding="lg" :class="[HOME_SECTION_SCROLL_MARGIN_CLASS, 'bg-classic-page']" data-testid="sections-process">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('process.badge')"
        :title="$t('process.title')"
        :subtitle="$t('process.description')"
      />

      <!-- Process steps: a numbered row of four flat cards -->
      <UiGrid cols="2" gap="lg" class="lg:grid-cols-4">
        <ClassicCard
          v-for="step in processSteps"
          :key="step.id"
          class="h-full border-t-4 !border-t-classic-brand"
          :data-testid="`process-step-${step.id}`"
        >
          <UiStack gap="sm">
            <UiStack direction="row" align="center" justify="between" gap="sm">
              <ClassicIconTile :icon="step.icon" />
              <UiText :as="UiTextTag.Span" tone="inherit" weight="bold" class="text-3xl tabular-nums text-classic-brand">{{ String(step.order).padStart(2, '0') }}</UiText>
            </UiStack>
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ step.title }}</UiHeading>
            <UiText tone="inherit" size="sm" class="leading-relaxed text-classic-muted">{{ step.description }}</UiText>
          </UiStack>
        </ClassicCard>
      </UiGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Process section: how an order goes from choosing a plan to a live site, as four numbered
 * steps in flat cards with a solid brand top edge, each with its icon on a flat brand tile. The steps (id, icon, order) come from
 * constants/processStep.ts; their text from the locale files.
 */

import { HomeSectionId } from '~/types/homesectionid'
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { PROCESS_STEPS } from '~/constants/processStep'
import { UiTextTag } from '~/utils/ui/enums'

/** Translation helpers and the active language. */
const { t } = useI18n()

/** The steps with their translated title and description. */
const processSteps = computed(() => PROCESS_STEPS.map((step) => ({
  ...step,
  title: t(`process.steps.${step.id}.title`),
  description: t(`process.steps.${step.id}.description`),
})))
</script>
