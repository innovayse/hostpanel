<template>
  <UiSection :id="HomeSectionId.Process" padding="lg" :contained="false" :class="[HOME_SECTION_SCROLL_MARGIN_CLASS, 'relative border-t border-line']">
    <UiSplit ratio="equal" gap="xl">
      <template #start>
        <UiStack gap="none" :class="STICKY_COLUMN_CLASS" data-testid="process-aside">
          <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac1']">{{ t('aurora.process.eyebrow') }}</UiText>
          <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
            {{ t('aurora.process.title') }}
          </UiHeading>
          <UiText tone="inherit" class="mt-[18px] text-[17px] leading-relaxed text-mut">{{ t('aurora.process.lead') }}</UiText>
          <UiStack direction="row" wrap class="mt-8">
            <UiButton
              variant="bare"
              :to="localePath(SITE_HOSTING_PATH)"
              data-testid="process-start"
              :class="[BRAND_BUTTON, 'px-6 py-3 text-[15px]']"
            >
              {{ t('aurora.process.start') }}
            </UiButton>
            <UiButton
              variant="bare"
              :to="localePath(SITE_CONTACT_PATH)"
              data-testid="process-talk"
              :class="[LINE_BUTTON, 'px-6 py-3 text-[15px]']"
            >
              {{ t('aurora.process.talk') }}
            </UiButton>
          </UiStack>
          <UiText tone="inherit" class="mt-5 text-[14px] leading-relaxed text-mut" data-testid="process-note">
            {{ t('aurora.process.note') }}
          </UiText>
        </UiStack>
      </template>

      <template #end>
        <UiStack gap="md">
          <UiCard
            v-for="step in steps"
            :key="step.n"
            tone="bare"
            padding="md"
            :data-testid="`process-step-${step.n}`"
            class="border-line bg-surf shadow-card"
          >
            <UiStack direction="row" align="start" gap="md">
              <UiText
                as="span"
                size="inherit"
                tone="inherit"
                class="w-[46px] shrink-0 bg-hero-grad bg-clip-text font-mono text-[26px] font-medium text-transparent"
              >
                {{ step.n }}
              </UiText>
              <UiStack gap="none" class="min-w-0">
                <UiText as="span" size="inherit" tone="inherit" weight="bold" class="text-[17px] text-tx">{{ step.title }}</UiText>
                <UiText as="span" size="inherit" tone="inherit" class="mt-1.5 text-[15px] leading-relaxed text-mut">{{ step.body }}</UiText>
              </UiStack>
            </UiStack>
          </UiCard>
        </UiStack>
      </template>
    </UiSplit>
  </UiSection>
</template>

<script setup lang="ts">
/** aurora process section: four numbered steps beside a sticky intro with two calls to action. */
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { HomeSectionId } from '~/types/homesectionid'
import { BRAND_BUTTON, LINE_BUTTON } from '~/templates/aurora/buttons'
import { STICKY_COLUMN_CLASS } from '~/templates/aurora/layoutClasses'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import { SITE_CONTACT_PATH, SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Translator for the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The four steps with their numbers; the numbers are ordinals, not copy. */
const steps = computed(() => [
  { n: '01', title: t('aurora.process.step1Title'), body: t('aurora.process.step1Body') },
  { n: '02', title: t('aurora.process.step2Title'), body: t('aurora.process.step2Body') },
  { n: '03', title: t('aurora.process.step3Title'), body: t('aurora.process.step3Body') },
  { n: '04', title: t('aurora.process.step4Title'), body: t('aurora.process.step4Body') },
])
</script>
