<template>
  <UiAccent :color="color">
    <!-- Features -->
    <UiStack v-if="features.length > 0" :as="UiStackTag.Section" gap="none" :class="[brandFallback, 'py-16 bg-ui-surface [.tpl-classic_&]:[--accent:rgb(var(--classic-brand))] [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:py-12']" data-testid="trial-features">
      <UiContainer class="w-full">
        <UiHeading :level="2" class="text-center mb-10 [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink">{{ $t('trial.featuresTitle', { product: name }) }}</UiHeading>
        <UiGrid cols="1" gap="md" class="md:grid-cols-2">
          <UiCard
            v-for="(feature, index) in features"
            :key="index"
            padding="sm"
            class="flex items-center gap-3 animate-reveal-up motion-reduce:animate-none"
            :class="revealDelay(index * 60)"
          >
            <CheckCircle :size="18" :stroke-width="2" :class="[accent.text, 'flex-shrink-0']" />
            <UiText :as="UiTextTag.Span" size="sm" tone="inherit" class="text-gray-700 dark:text-gray-300">{{ feature }}</UiText>
          </UiCard>
        </UiGrid>
      </UiContainer>
    </UiStack>

    <!-- Perks -->
    <UiStack :as="UiStackTag.Section" gap="none" :class="[brandFallback, 'py-16 [.tpl-classic_&]:[--accent:rgb(var(--classic-brand))] [.tpl-classic_&]:bg-classic-page [.tpl-classic_&]:py-12']" data-testid="trial-perks">
      <UiContainer class="w-full">
        <UiGrid cols="1" gap="lg" class="md:grid-cols-3">
          <UiCard
            v-for="(perk, index) in perks"
            :key="perk.key"
            class="text-center animate-reveal-up motion-reduce:animate-none"
            :data-testid="`trial-perk-${perk.key}`"
            :class="revealDelay(index * 100)"
          >
            <component :is="perk.icon" :size="32" :stroke-width="2" :class="[accent.text, 'mx-auto mb-3']" />
            <UiText weight="semibold" class="mb-1">{{ $t(`trial.perks.${perk.key}.title`) }}</UiText>
            <UiText size="sm" tone="subtle">{{ $t(`trial.perks.${perk.key}.desc`) }}</UiText>
          </UiCard>
        </UiGrid>

        <UiStack gap="none" align="center" class="mt-12 text-center">
          <UiAccentButton appearance="solid" :href="trialUrl" size="lg" data-testid="trial-cta-bottom">
            <Rocket :size="20" :stroke-width="2" />
            {{ $t('trial.cta') }}
          </UiAccentButton>
          <UiText size="sm" tone="subtle" class="mt-3">{{ $t('trial.noCc') }}</UiText>
        </UiStack>
      </UiContainer>
    </UiStack>
  </UiAccent>
</template>

<script setup lang="ts">
/**
 * Body of the trial page: the product's feature lines, the three trial perks and the closing
 * trial call to action, painted in the product's own accent colour.
 */

import { revealDelay } from '~/utils/reveal'
import { CheckCircle, Clock, CreditCard, Headphones, Rocket } from 'lucide-vue-next'
import { TrialPerk } from '~/types/trialperk'
import { ACCENT_BRAND_CLASS, ACCENT_CLASSES, isAccentColor } from '~/utils/ui/accent'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Props of TrialDetails. */
interface Props {
  /** Display name of the product. */
  name: string
  /** Accent colour of the product, as a hex string. */
  color: string
  /** Where the trial call to action leads. */
  trialUrl: string
  /** Feature lines of the product; the section is hidden when there are none. */
  features: string[]
}

/** The component props. */
const props = defineProps<Props>()

/** Accent classes the body reads (the colour itself is set by the surrounding UiAccent). */
const accent = ACCENT_CLASSES

/** Points the accent at the portal brand colour when the product has no valid colour of its own. */
const brandFallback = computed(() => (isAccentColor(props.color) ? '' : ACCENT_BRAND_CLASS))

/** The perks in display order, each with the icon that stands for it. */
const perks = [
  { key: TrialPerk.NoCc, icon: CreditCard },
  { key: TrialPerk.Duration, icon: Clock },
  { key: TrialPerk.Support, icon: Headphones },
]
</script>
