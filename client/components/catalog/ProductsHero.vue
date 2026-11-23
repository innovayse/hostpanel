<template>
  <UiStack :as="UiStackTag.Section" :gap="UiGap.None" class="relative py-12 md:py-32 lg:py-40 bg-ui-page overflow-hidden [.tpl-classic_&]:border-b [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:py-12 md:[.tpl-classic_&]:py-14 lg:[.tpl-classic_&]:py-16" data-testid="products-hero">
    <!-- Animated background -->
    <UiDecor class="inset-0">
      <UiDecor class="top-0 right-1/4 w-[600px] h-[600px] bg-secondary-500/25 rounded-full blur-[150px] animate-blob" />
      <UiDecor class="bottom-0 left-1/3 w-[500px] h-[500px] bg-cyan-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
      <UiDecor class="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-4000" />
      <UiDecor class="inset-0 opacity-[0.025] pattern-grid-80" />
      <UiDecor class="top-1/4 left-1/4 w-3 h-3 bg-secondary-600/60 dark:bg-secondary-400/60 rounded-full animate-float [animation-delay:0.5s]" />
      <UiDecor class="top-1/3 right-1/3 w-2 h-2 bg-cyan-400/70 rounded-full animate-float [animation-delay:1.2s]" />
      <UiDecor class="bottom-1/3 left-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float [animation-delay:2s]" />
    </UiDecor>

    <UiContainer class="relative z-10 w-full">
      <UiStack :gap="UiGap.None" class="text-center">
        <!-- Badge -->
        <UiStack :as="UiStackTag.Span" :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Xs" class="mx-auto w-fit px-4 py-2 mb-6 rounded-full glass border border-secondary-500/20 backdrop-blur-sm [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-0 [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:backdrop-blur-none">
          <UiStack :as="UiStackTag.Span" :gap="UiGap.None" class="relative h-2 w-2 [.tpl-classic_&]:hidden">
            <UiDecor :as="UiDecorTag.Span" class="animate-ping inline-flex h-full w-full rounded-full bg-secondary-600 dark:bg-secondary-400 opacity-75" />
            <UiStack :as="UiStackTag.Span" :gap="UiGap.None" class="relative rounded-full h-2 w-2 bg-secondary-500" />
          </UiStack>
          <UiText :as="UiTextTag.Span" :size="UiTextSize.Sm" :weight="UiTextWeight.Medium" :tone="UiTextTone.Muted" class="[.tpl-classic_&]:text-classic-brand">{{ $t('products.hero.badge') }}</UiText>
        </UiStack>

        <!-- Headline -->
        <UiHeading :level="UiHeadingLevel.H1" class="text-5xl md:text-6xl lg:text-7xl mb-8 leading-[1.1] [.tpl-classic_&]:mb-4 [.tpl-classic_&]:text-4xl [.tpl-classic_&]:font-semibold md:[.tpl-classic_&]:text-5xl lg:[.tpl-classic_&]:text-5xl">
          <UiStack :as="UiStackTag.Span" :gap="UiGap.None" class="text-primary-600 dark:text-primary-400 [.tpl-classic_&]:text-classic-brand">{{ $t('products.hero.headline1') }}</UiStack>
          <UiStack :as="UiStackTag.Span" :gap="UiGap.None" class="text-gray-900 dark:text-white mt-4 [.tpl-classic_&]:mt-1 [.tpl-classic_&]:text-classic-ink">{{ $t('products.hero.headline2') }}</UiStack>
        </UiHeading>

        <!-- Subtitle -->
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Lg" class="text-xl mb-12 leading-relaxed max-w-[75ch] mx-auto [.tpl-classic_&]:mb-8 [.tpl-classic_&]:text-lg [.tpl-classic_&]:text-classic-muted">{{ $t('products.hero.subtitle') }}</UiText>

        <!-- Calls to action -->
        <UiStack :align="UiCrossAlign.Center" :justify="UiJustify.Center" :gap="UiGap.Sm" class="sm:flex-row sm:gap-4 mb-12 sm:mb-14 md:mb-16 [.tpl-classic_&]:mb-8 sm:[.tpl-classic_&]:mb-8 md:[.tpl-classic_&]:mb-8">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :size="UiSize.Lg"
            class="w-full sm:w-auto group"
            data-testid="products-hero-view"
            @click="scrollToProducts"
          >
            {{ $t('products.hero.cta.viewProducts') }}
            <ArrowDown :size="20" :stroke-width="2" class="ml-2 group-hover:translate-y-1 transition-transform" />
          </UiButton>

          <UiButton
            :variant="UiButtonVariant.Outline"
            :size="UiSize.Lg"
            :to="localePath(SITE_HOSTING_PATH)"
            class="w-full sm:w-auto"
            data-testid="products-hero-compare"
          >
            <Server :size="20" :stroke-width="2" class="mr-2" />
            {{ $t('products.hero.cta.comparePlans') }}
          </UiButton>
        </UiStack>

        <!-- Trust badges -->
        <UiStack
          :direction="UiDirection.Row"
          :align="UiCrossAlign.Center"
          :justify="UiJustify.Center"
          :gap="UiGap.None"
          wrap
          class="gap-8 text-sm text-gray-600 dark:text-gray-400 animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:text-classic-muted"
          :class="revealDelay(400)"
        >
          <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Xs">
            <Headphones :size="20" :stroke-width="2" class="text-green-600 dark:text-green-400 [.tpl-classic_&]:text-classic-brand" />
            <UiText :as="UiTextTag.Span" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit">{{ $t('products.hero.trustBadges.support') }}</UiText>
          </UiStack>
          <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Xs">
            <Lock :size="20" :stroke-width="2" class="text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-brand" />
            <UiText :as="UiTextTag.Span" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit">{{ $t('products.hero.trustBadges.encryption') }}</UiText>
          </UiStack>
          <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Xs">
            <Trophy :size="20" :stroke-width="2" class="text-secondary-700 dark:text-secondary-400 [.tpl-classic_&]:text-classic-brand" />
            <UiText :as="UiTextTag.Span" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit">{{ $t('products.hero.trustBadges.uptime') }}</UiText>
          </UiStack>
        </UiStack>
      </UiStack>
    </UiContainer>

    <!-- Corner decorations -->
    <UiDecor class="top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-cyan-500/30" />
    <UiDecor class="bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-secondary-500/30" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Hero of the products page: badge, headline, the two calls to action and the trust badges.
 *
 * "View Products" scrolls to the catalogue section of the same page (`products-section`); "Compare hosting plans" goes to the hosting page. Everything it shows is a locale key.
 */

import { UiButtonVariant, UiCrossAlign, UiDecorTag, UiDirection, UiGap, UiHeadingLevel, UiJustify, UiSize, UiStackTag, UiTextSize, UiTextTag, UiTextTone, UiTextWeight } from '~/utils/ui/enums'
import { revealDelay } from '~/utils/reveal'
import { ArrowDown, Headphones, Lock, Server, Trophy } from 'lucide-vue-next'
import { SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Id of the catalogue section the first call to action scrolls to. */
const CATALOG_SECTION_ID = 'products-section'

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** Scrolls the catalogue section into view. */
const scrollToProducts = (): void => {
  document.getElementById(CATALOG_SECTION_ID)?.scrollIntoView({ behavior: 'smooth' })
}
</script>
