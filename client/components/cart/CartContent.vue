<template>
  <UiSection :padding="UiSectionPadding.Md" :contained="false" class="relative overflow-hidden bg-ui-page" data-testid="cart-content">
    <UiDecor class="inset-0 [.tpl-classic_&]:hidden">
      <UiDecor class="inset-0 bg-gradient-to-bl from-primary-500/5 via-transparent to-cyan-500/5" />
    </UiDecor>

    <UiContainer class="relative z-10">
      <!-- Empty cart -->
      <UiEmptyState
        v-if="items.length === 0"
        :icon="ShoppingCart"
        :title="$t('cart.empty')"
        :description="$t('cart.emptyHint')"
        data-testid="cart-empty"
      >
        <UiButton :to="localePath(SITE_HOSTING_PATH)" :variant="UiButtonVariant.Primary" icon="mdi:server" data-testid="cart-browse">
          {{ $t('cart.browseHosting') }}
        </UiButton>
      </UiEmptyState>

      <UiStack v-else :gap="UiGap.Lg">
        <!-- Items priced in a currency the payer no longer holds -->
        <UiAlert v-if="staleCount > 0" :variant="UiAlertVariant.Warning" data-testid="cart-stale">
          {{ $t('cart.staleCurrencyNotice') }}
        </UiAlert>

        <!-- Lines -->
        <TransitionGroup
          tag="div"
          class="space-y-3"
          enter-active-class="transition-all duration-300 ease-out"
          leave-active-class="transition-all duration-300 ease-out"
          enter-from-class="opacity-0 -translate-x-5"
          leave-to-class="opacity-0 translate-x-5"
        >
          <UiCard
            v-for="item in items"
            :key="item.pid"
            hover
            :padding="UiCardPadding.Sm"
            class="flex items-center justify-between gap-4"
            data-testid="cart-item"
          >
            <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" class="min-w-0">
              <UiIconTile icon="mdi:server" />
              <UiStack :gap="UiGap.None" class="min-w-0">
                <UiText :weight="UiTextWeight.Semibold" truncate data-testid="cart-item-name">{{ item.name }}</UiText>
                <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="mt-0.5">{{ item.cycleLabel }}</UiText>
              </UiStack>
            </UiStack>

            <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" class="flex-shrink-0">
              <UiStack :gap="UiGap.None" class="text-right">
                <UiText :size="UiTextSize.Lg" :weight="UiTextWeight.Bold">{{ priceOf(item) }}</UiText>
                <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted">/ {{ item.cycleLabel }}</UiText>
              </UiStack>
              <UiIconButton
                :icon="Trash2"
                :label="$t('common.remove')"
                :variant="UiIconButtonVariant.Ghost"
                :size="UiSize.Sm"
                testid="cart-item-remove"
                @click="emit('remove', item)"
              />
            </UiStack>
          </UiCard>
        </TransitionGroup>

        <!-- Order summary -->
        <UiCard :title="$t('cart.summary')" data-testid="cart-summary">
          <UiStack :gap="UiGap.Xs">
            <UiKeyValue v-for="item in items" :key="item.pid" :label="item.name" :value="priceOf(item)" />
            <CheckoutBundleSummary :name="bundleName" :offer="bundleOffer" :discount="bundleDiscount" class="pt-1" />
            <UiDivider class="mt-3" />
            <UiStack :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.None" class="pt-3">
              <UiText :as="UiTextTag.Span" :weight="UiTextWeight.Bold">{{ $t('cart.total') }}</UiText>
              <UiText :as="UiTextTag.Span" :tone="UiTextTone.Inherit" :weight="UiTextWeight.Bold" class="text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-ink" data-testid="cart-total">{{ totalLabel }}</UiText>
            </UiStack>
          </UiStack>
        </UiCard>

        <!-- Actions -->
        <UiStack :gap="UiGap.Sm" class="sm:flex-row">
          <UiButton :variant="UiButtonVariant.Subtle" icon="mdi:delete-outline" class="order-2 sm:order-1 flex-shrink-0" data-testid="cart-clear" @click="emit('clear')">
            {{ $t('cart.clearCart') }}
          </UiButton>

          <UiButton
            :to="localePath(SITE_CHECKOUT_PATH)"
            :variant="UiButtonVariant.Primary"
            :size="UiSize.Lg"
            icon="mdi:credit-card-outline"
            class="order-1 sm:order-2 flex-1"
            data-testid="cart-checkout"
          >
            {{ $t('cart.checkout') }}
          </UiButton>
        </UiStack>

        <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Subtle" class="text-center flex items-center justify-center gap-1.5">
          <Lock :size="12" :stroke-width="2" />
          {{ $t('cart.secureCheckout') }}
        </UiText>
      </UiStack>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * The body of the cart page: the empty notice, or the lines, the order summary and the checkout
 * actions. Under the classic template (the `tpl-classic` root class) the glow wash is dropped.
 *
 * Presentational: the page owns the cart store and the currency, and passes the lines, how many of
 * them are priced in a stale currency, the formatted total, a function that formats one line and, for a
 * cart filled from a bundle, the bundle line (name, discount in words, estimated amount taken off).
 */
import { UiAlertVariant, UiButtonVariant, UiCardPadding, UiCrossAlign, UiDirection, UiGap, UiIconButtonVariant, UiJustify, UiSectionPadding, UiSize, UiTextSize, UiTextTag, UiTextTone, UiTextWeight } from '~/utils/ui/enums'
import { Lock, ShoppingCart, Trash2 } from 'lucide-vue-next'
import type { CartItem } from '~/types/cartitem'
import { SITE_CHECKOUT_PATH, SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Props of CartContent. */
interface Props {
  /** The cart lines. */
  items: CartItem[]
  /** How many lines are priced in a currency other than the payer's current one. */
  staleCount: number
  /** The total of the lines in the payer's currency, formatted. */
  totalLabel: string
  /** Formats one line's price in the currency it was added in. */
  priceOf: (item: CartItem) => string
  /** The bundle the lines came from; empty for a cart of plain lines. */
  bundleName?: string
  /** The bundle's discount in words, e.g. "10% off"; empty when it has none. */
  bundleOffer?: string
  /** The estimated discount taken off the total, formatted. */
  bundleDiscount?: string
}

withDefaults(defineProps<Props>(), { bundleName: '', bundleOffer: '', bundleDiscount: '' })

/** Events of CartContent. */
const emit = defineEmits<{
  /** The visitor removed a line. */
  remove: [item: CartItem]
  /** The visitor emptied the cart. */
  clear: []
}>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
