<template>
  <section class="relative py-12 md:py-28 bg-ui-page overflow-hidden [.tpl-classic_&]:border-b [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:py-10 md:[.tpl-classic_&]:py-14" data-testid="hosting-plan-hero">
    <UiDecor class="inset-0">
      <UiDecor class="inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
      <UiDecor class="top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
      <UiDecor class="bottom-0 right-1/4 w-[400px] h-[400px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
      <UiDecor class="inset-0 opacity-[0.02] pattern-grid-60" />
      <UiDecor class="top-1/4 left-1/3 w-2 h-2 bg-primary-400/60 rounded-full animate-float [animation-delay:0.5s]" />
      <UiDecor class="bottom-1/3 right-1/4 w-3 h-3 bg-secondary-600/50 dark:bg-secondary-400/50 rounded-full animate-float [animation-delay:1.5s]" />
    </UiDecor>
    <UiDecor class="top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30" />
    <UiDecor class="bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30" />

    <UiContainer class="relative z-10">
      <UiLink :to="localePath('/hosting')" tone="muted" :underline="false" class="inline-flex items-center gap-2 mb-10" testid="hosting-plan-back">
        <ArrowLeft :size="16" :stroke-width="2" />
        {{ $t('hosting.backToPlans') }}
      </UiLink>

      <!-- Loading -->
      <div v-if="pending" class="space-y-4" data-testid="hosting-plan-loading">
        <div class="w-48"><UiSkeleton shape="block" class="h-8" /></div>
        <div class="w-80 max-w-full"><UiSkeleton shape="block" class="h-16" /></div>
        <div class="w-96 max-w-full"><UiSkeleton shape="block" class="h-5" /></div>
        <div class="w-40 pt-4"><UiSkeleton shape="block" class="h-14" /></div>
      </div>

      <!-- Not found -->
      <UiEmptyState v-else-if="!plan" :icon="AlertCircle" :title="$t('hosting.planNotFound')" data-testid="hosting-plan-missing">
        <UiLink :to="localePath('/hosting')" testid="hosting-plan-all">{{ $t('hosting.viewAllPlans') }}</UiLink>
      </UiEmptyState>

      <!-- Plan -->
      <div
        v-else
        class="animate-reveal-up motion-reduce:animate-none"
      >
        <!-- There is no "popular" badge or tagline: `ProductDto` has no field for either. -->
        <UiHeading :level="1" size="display" class="mb-4 leading-tight [.tpl-classic_&]:text-3xl [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink md:[.tpl-classic_&]:text-4xl" data-testid="hosting-plan-name">{{ plan.name }}</UiHeading>

        <!-- Summary parsed out of the description; interpolated, never HTML. -->
        <UiText v-if="summary" tone="muted" size="lg" class="mb-8 max-w-[75ch] [.tpl-classic_&]:text-classic-muted" data-testid="hosting-plan-summary">{{ summary }}</UiText>

        <div class="flex flex-wrap items-end gap-6">
          <div class="flex items-baseline gap-1.5">
            <div class="text-5xl font-bold text-gray-900 dark:text-white [.tpl-classic_&]:text-classic-ink" data-testid="hosting-plan-price">{{ price }}</div>
            <div class="text-gray-600 dark:text-gray-400 text-sm [.tpl-classic_&]:text-classic-muted">{{ period }}</div>
          </div>
          <UiButton
            size="lg"
            class="px-8 py-4 font-bold bg-gradient-to-r from-primary-600 to-primary-500 hover:from-primary-500 hover:to-primary-400 hover:scale-[1.02] shadow-lg shadow-primary-500/30 [.tpl-classic_&]:bg-none [.tpl-classic_&]:shadow-none [.tpl-classic_&]:hover:scale-100 [.tpl-classic_&]:font-semibold"
            data-testid="hosting-plan-order"
            @click="emit('order')"
          >
            <ShoppingCart :size="20" :stroke-width="2" class="mr-2" />
            {{ inCart ? $t('hosting.inCart') : $t('hosting.orderNow') }}
          </UiButton>
        </div>
      </div>
    </UiContainer>
  </section>
</template>

<script setup lang="ts">
/**
 * Hero of a hosting plan page: the way back, then (while loading) a skeleton, (when the plan does
 * not exist) a notice, or the plan's name, summary, price and the order button.
 *
 * Presentational: the page decides which plan this is and what the order button does.
 */
import { AlertCircle, ArrowLeft, ShoppingCart } from 'lucide-vue-next'
import type { PortalProduct } from '~/types/portalproduct'

/** Props of HostingPlanHero. */
interface Props {
  /** Whether the catalogue is still being read. */
  pending: boolean
  /** The plan, or null when no plan matches the address. */
  plan: PortalProduct | null
  /** Leading prose of the plan description; empty for plans whose copy is bullets only. */
  summary: string
  /** The plan's price for the selected cycle, formatted. */
  price: string
  /** The unit the price is per, already translated ("/mo", "/qtr", "/yr"...), for the cycle the price is of. */
  period: string
  /** Whether the plan is already in the cart. */
  inCart: boolean
}

defineProps<Props>()

/** Events of HostingPlanHero. */
const emit = defineEmits<{
  /** The visitor pressed the order button. */
  order: []
}>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
