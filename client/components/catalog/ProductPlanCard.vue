<template>
  <UiStack gap="none" class="relative" :data-testid="`product-plan-${plan.id}`">
    <!-- Outside the card, which would clip a badge sitting on its edge. -->
    <UiText
      v-if="popular"
      :as="UiTextTag.Span"
      size="xs"
      tone="inherit"
      weight="semibold"
      :class="BADGE_CLASSES"
      data-testid="product-plan-popular"
    >
      {{ $t('products.pricing.popular') }}
    </UiText>

    <UiCard :class="[CARD_CLASSES, popular ? POPULAR_CARD_CLASSES : '']">
      <UiHeading
        :level="UiHeadingLevel.H3"
        size="bare"
        class="text-xl font-semibold text-gray-900 dark:text-white md:text-2xl [.tpl-classic_&]:text-classic-ink"
        data-testid="product-plan-name"
      >
        {{ plan.tier }}
      </UiHeading>

      <!-- Interpolated, never HTML: `parseDescription` has already stripped the operator's tags. The box keeps its height without copy, so the price lines up across a row. -->
      <UiText
        size="sm"
        tone="muted"
        class="mt-2 min-h-[3rem] line-clamp-2 leading-relaxed [.tpl-classic_&]:text-classic-muted"
        data-testid="product-plan-description"
      >
        {{ plan.description }}
      </UiText>

      <UiStack direction="row" wrap align="end" gap="xs" class="mt-5">
        <UiText
          :as="UiTextTag.Span"
          size="inherit"
          tone="inherit"
          weight="bold"
          class="text-4xl leading-none tabular-nums text-gray-900 dark:text-white [.tpl-classic_&]:text-classic-ink"
          data-testid="product-plan-price"
        >
          {{ price || '—' }}
        </UiText>
        <UiText
          v-if="price"
          :as="UiTextTag.Span"
          size="sm"
          tone="muted"
          weight="medium"
          class="[.tpl-classic_&]:text-classic-muted"
          data-testid="product-plan-period"
        >
          {{ $t(`products.pricing.period.${PRICING_CYCLE_I18N_KEY[cycle]}`) }}
        </UiText>
      </UiStack>

      <!-- Always present, so a card without a saving is as tall as one with it. -->
      <UiStack direction="row" align="center" class="mt-2 min-h-6">
        <UiBadge v-if="saving !== null" variant="success" size="sm" data-testid="product-plan-saving">
          {{ $t('products.pricing.save', { percent: saving }) }}
        </UiBadge>
      </UiStack>

      <UiList v-if="visibleFeatures.length" list-style="plain" class="mt-5 space-y-3" data-testid="product-plan-features">
        <UiListItem
          v-for="feature in visibleFeatures"
          :key="feature"
          class="flex items-start gap-3 text-sm text-gray-700 dark:text-gray-300 [.tpl-classic_&]:text-classic-ink"
        >
          <Check :size="16" :stroke-width="2.5" :class="[ACCENT_CLASSES.text, 'mt-0.5 flex-shrink-0']" />
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="min-w-0 break-words">{{ feature }}</UiText>
        </UiListItem>
        <UiListItem
          v-if="hiddenCount > 0"
          class="text-sm font-medium text-gray-600 dark:text-gray-400 [.tpl-classic_&]:text-classic-muted"
          data-testid="product-plan-more"
        >
          {{ $t('products.pricing.more', { count: hiddenCount }) }}
        </UiListItem>
      </UiList>

      <!-- mt-auto pins the buttons to the card bottom whatever the height of the lists above. -->
      <UiStack gap="sm" class="mt-auto pt-6">
        <UiButton
          :variant="inCart ? UiButtonVariant.Secondary : UiButtonVariant.Primary"
          full-width
          :data-testid="`product-add-${plan.id}`"
          @click="emit('addToCart', plan)"
        >
          <Check v-if="inCart" :size="16" :stroke-width="2.5" class="mr-2" />
          <ShoppingCart v-else :size="16" :stroke-width="2" class="mr-2" />
          {{ inCart ? $t('hosting.inCart') : $t('hosting.addToCart') }}
        </UiButton>

        <!-- Only when this deployment has an external order page to send the visitor to. -->
        <UiButton
          v-if="trialUrl"
          :variant="UiButtonVariant.Outline"
          full-width
          :data-testid="`product-trial-${plan.id}`"
          @click="emit('trial', plan)"
        >
          {{ $t('trial.cta') }}
        </UiButton>
      </UiStack>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * One plan of a product family: name, short description, the price for the chosen cycle with its
 * period and (for a year) the saving, the plan's own feature lines up to `maxFeatures` with a
 * "+N more" line for the rest, and the cart and trial buttons pinned to the card bottom.
 *
 * Presentational and painted in the colour of the surrounding `UiAccent`. Every card in a row is
 * the same height: the description and saving rows reserve their space and the buttons sit at
 * the bottom. The size scale is that of the hosting plan cards (p-6, a 4xl price, default buttons).
 */
import { Check, ShoppingCart } from 'lucide-vue-next'
import { PLAN_FEATURE_LIMIT } from '~/constants/planFeatureLimit'
import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { PricingCycle } from '~/types/pricingcycle'
import type { ProductPlan } from '~/types/productplan'
import { ACCENT_CLASSES } from '~/utils/ui/accent'
import { UiButtonVariant, UiHeadingLevel, UiTextTag } from '~/utils/ui/enums'
import { yearlySavingPercent } from '~/utils/yearlySaving'

/** Props of ProductPlanCard. */
interface Props {
  /** The plan to show. */
  plan: ProductPlan
  /** The plan's price for the chosen cycle, formatted; empty when it has none for that cycle. */
  price: string
  /** The billing cycle the price is for. */
  cycle: PricingCycle
  /** Whether the plan is already in the cart for that cycle. */
  inCart: boolean
  /** Whether the card wears the recommended badge and frame. */
  popular?: boolean
  /** The external order page of the plan, or null when this deployment has none. */
  trialUrl?: string | null
  /** Most feature lines the card lists; the rest collapse into a "+N more" line. */
  maxFeatures?: number
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  popular: false,
  trialUrl: null,
  maxFeatures: PLAN_FEATURE_LIMIT,
})

/** Events of ProductPlanCard. */
const emit = defineEmits<{
  /** The visitor asked to add the plan to the cart. */
  addToCart: [plan: ProductPlan]
  /** The visitor asked to start a trial of the plan. */
  trial: [plan: ProductPlan]
}>()

/** The badge on the card's top edge, in the accent colour (the brand colour under classic). */
const BADGE_CLASSES = 'absolute -top-3 left-1/2 z-10 -translate-x-1/2 whitespace-nowrap rounded-full bg-[color:var(--accent)] px-3 py-1 uppercase tracking-wider text-[color:var(--accent-ink,white)] [.tpl-classic_&]:rounded-md [.tpl-classic_&]:text-classic-on-brand'

/** Structure of the card: it fills the height of its row and stacks its parts. */
const CARD_CLASSES = 'relative flex h-full flex-col'

/** The recommended card's frame; important so it beats the kit's hairline in every template. */
const POPULAR_CARD_CLASSES = '!border-2 !border-[color:var(--accent)]'

/** The feature lines the card lists. */
const visibleFeatures = computed(() => props.plan.features.slice(0, props.maxFeatures))

/** How many feature lines the card leaves out. */
const hiddenCount = computed(() => Math.max(0, props.plan.features.length - props.maxFeatures))

/** The yearly saving, announced only while the yearly price is the one on show. */
const saving = computed<number | null>(() => (props.cycle === PricingCycle.Annual ? yearlySavingPercent(props.plan.pricing) : null))
</script>
