<template>
  <UiStack :as="UiStackTag.Section" gap="none" class="py-8 md:py-20 bg-ui-page relative overflow-hidden" data-testid="products-benefits">
    <UiDecor class="inset-0">
      <UiDecor class="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[500px] bg-secondary-500/15 rounded-full blur-[180px]" />
      <UiDecor class="top-20 left-1/4 w-2 h-2 bg-secondary-600/60 dark:bg-secondary-400/60 rounded-full animate-float [animation-delay:0.7s]" />
      <UiDecor class="bottom-1/4 right-1/3 w-3 h-3 bg-cyan-400/50 rounded-full animate-float [animation-delay:1.6s]" />
    </UiDecor>

    <UiContainer class="relative z-10 w-full">
      <MarketingSectionHeader class="mb-16" :subtitle="$t('products.benefits.subtitle')">
        <template #title>
          <i18n-t keypath="products.benefits.title" tag="span">
            <template #benefitsHighlight>
              <UiGradientText class="bg-gradient-to-r from-secondary-600 dark:from-secondary-400 to-cyan-400 [.tpl-classic_&]:bg-none [.tpl-classic_&]:text-classic-brand">{{ $t('products.benefits.benefitsHighlight') }}</UiGradientText>
            </template>
          </i18n-t>
        </template>
      </MarketingSectionHeader>

      <!-- Benefits -->
      <UiGrid cols="2" gap="lg" class="mb-10 lg:grid-cols-4">
        <UiCard
          v-for="(benefit, index) in benefits"
          :key="benefit.key"
          :class="revealDelay(index * 100)"
          padding="lg"
          class="relative group h-full text-center animate-reveal-up motion-reduce:animate-none hover:border-secondary-500/40 hover:-translate-y-1 hover:shadow-lg dark:hover:shadow-none transition-all duration-300 [.tpl-classic_&]:hover:border-classic-border [.tpl-classic_&]:hover:translate-y-0 [.tpl-classic_&]:hover:shadow-sm"
          :data-testid="`benefit-${benefit.key}`"
        >
          <UiDecor class="inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-secondary-500/10 to-cyan-500/5" />

          <UiStack :as="UiStackTag.Span" direction="row" align="center" justify="center" gap="none" class="relative mx-auto w-12 h-12 rounded-xl bg-gradient-to-br from-secondary-500/25 to-cyan-500/15 mb-4 group-hover:scale-105 transition-transform duration-300 border border-secondary-500/25 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-0 [.tpl-classic_&]:bg-none [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:group-hover:scale-100">
            <Icon :name="benefit.icon" class="text-2xl text-secondary-700 dark:text-secondary-400 [.tpl-classic_&]:text-classic-brand" />
          </UiStack>

          <UiHeading :level="3" class="relative text-lg mb-3 [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink">{{ benefit.title }}</UiHeading>
          <UiText tone="muted" size="sm" class="relative leading-relaxed [.tpl-classic_&]:text-classic-muted">{{ benefit.description }}</UiText>

          <UiDecor class="bottom-0 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-24 bg-gradient-to-r from-secondary-500 to-cyan-500 transition-all duration-500 rounded-full" />
        </UiCard>
      </UiGrid>

      <!-- Reassurances -->
      <UiGrid cols="1" gap="lg" class="md:grid-cols-3">
        <UiCard
          v-for="(item, index) in reassurances"
          :key="item.key"
          padding="md"
          :class="[item.hoverBorder, 'group text-center transition-colors animate-reveal-up motion-reduce:animate-none', revealDelay((index + 1) * 100)]"
          :data-testid="`reassurance-${item.key}`"
        >
          <component :is="item.icon" :size="32" :stroke-width="2" :class="[item.iconColor, 'mx-auto mb-3 group-hover:scale-110 transition-transform [.tpl-classic_&]:text-classic-brand [.tpl-classic_&]:group-hover:scale-100']" />
          <UiText size="sm" weight="semibold" class="mb-1">{{ $t(`products.benefits.additional.${item.key}.title`) }}</UiText>
          <UiText size="xs" tone="subtle">{{ $t(`products.benefits.additional.${item.key}.description`) }}</UiText>
        </UiCard>
      </UiGrid>
    </UiContainer>

    <!-- Corner decorations -->
    <UiDecor class="top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-cyan-500/25" />
    <UiDecor class="bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-secondary-500/25" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The benefits block of the products page: four reasons to host with us (speed, security, human
 * support, an easy start) and three reassurances (trial, refund policy, clear pricing).
 *
 * The cards enter with a CSS keyframe (`animate-reveal-up`) rather than a scroll-triggered
 * directive: the markup is never rendered hidden, so the content shows in server HTML, before
 * hydration, in both themes and when nothing ever scrolls it into view.
 */

import { revealDelay } from '~/utils/reveal'
import { BadgeCheck, CheckCircle, ShieldCheck } from 'lucide-vue-next'
import { UiStackTag } from '~/utils/ui/enums'

/** Translation helpers and the active language. */
const { t } = useI18n()

/** The four customer benefits, in display order. */
const benefits = computed(() => [
  { key: 'speed', icon: 'mdi:rocket-launch', title: t('products.benefits.speed.title'), description: t('products.benefits.speed.description') },
  { key: 'security', icon: 'mdi:shield-lock', title: t('products.benefits.security.title'), description: t('products.benefits.security.description') },
  { key: 'support', icon: 'mdi:headset', title: t('products.benefits.support.title'), description: t('products.benefits.support.description') },
  { key: 'migration', icon: 'mdi:cloud-upload', title: t('products.benefits.migration.title'), description: t('products.benefits.migration.description') },
])

/** The three reassurances under the benefits; each names the `products.benefits.additional.*` group it reads. */
const reassurances = [
  { key: 'trial', icon: CheckCircle, iconColor: 'text-green-600 dark:text-green-400', hoverBorder: 'hover:border-green-500/30' },
  { key: 'guarantee', icon: ShieldCheck, iconColor: 'text-blue-600 dark:text-blue-400', hoverBorder: 'hover:border-blue-500/30' },
  { key: 'pricing', icon: BadgeCheck, iconColor: 'text-purple-600 dark:text-purple-400', hoverBorder: 'hover:border-purple-500/30' },
]
</script>
