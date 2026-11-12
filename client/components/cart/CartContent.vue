<template>
  <section class="py-10 md:py-16 bg-ui-page relative overflow-hidden" data-testid="cart-content">
    <UiDecor class="inset-0 [.tpl-classic_&]:hidden">
      <UiDecor class="inset-0 bg-gradient-to-bl from-primary-500/5 via-transparent to-cyan-500/5" />
    </UiDecor>

    <UiContainer class="relative z-10">
      <div>
        <!-- Empty cart -->
        <UiEmptyState
          v-if="items.length === 0"
          :icon="ShoppingCart"
          :title="$t('cart.empty')"
          :description="$t('cart.emptyHint')"
          data-testid="cart-empty"
        >
          <UiButton :to="localePath('/hosting')" data-testid="cart-browse">
            <Server :size="18" :stroke-width="2" class="mr-2" />
            {{ $t('cart.browseHosting') }}
          </UiButton>
        </UiEmptyState>

        <UiStack v-else gap="lg">
          <!-- Items priced in a currency the payer no longer holds -->
          <UiAlert v-if="staleCount > 0" variant="warning" data-testid="cart-stale">
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
              padding="sm"
              class="flex items-center justify-between gap-4"
              data-testid="cart-item"
            >
              <div class="flex items-center gap-4 min-w-0">
                <span class="w-10 h-10 rounded-lg bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft">
                  <Server :size="18" :stroke-width="2" class="text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-brand" />
                </span>
                <div class="min-w-0">
                  <div class="font-semibold text-gray-900 dark:text-white truncate" data-testid="cart-item-name">{{ item.name }}</div>
                  <div class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">{{ item.cycleLabel }}</div>
                </div>
              </div>

              <div class="flex items-center gap-4 flex-shrink-0">
                <div class="text-right">
                  <div class="text-lg font-bold text-gray-900 dark:text-white">{{ priceOf(item) }}</div>
                  <div class="text-xs text-gray-600 dark:text-gray-400">/ {{ item.cycleLabel }}</div>
                </div>
                <UiIconButton
                  :icon="Trash2"
                  :label="$t('cart.remove')"
                  variant="ghost"
                  size="sm"
                  testid="cart-item-remove"
                  @click="emit('remove', item)"
                />
              </div>
            </UiCard>
          </TransitionGroup>

          <!-- Order summary -->
          <UiCard :title="$t('cart.summary')" data-testid="cart-summary">
            <div class="space-y-2">
              <div v-for="item in items" :key="item.pid" class="flex justify-between text-sm">
                <span class="text-gray-600 dark:text-gray-400 truncate max-w-[60%]">{{ item.name }}</span>
                <span class="text-gray-900 dark:text-white">{{ priceOf(item) }}</span>
              </div>
              <CheckoutBundleSummary :name="bundleName" :offer="bundleOffer" :discount="bundleDiscount" class="pt-1" />
              <div class="border-t border-gray-200 dark:border-white/10 pt-3 mt-3 flex justify-between font-bold text-base">
                <span class="text-gray-900 dark:text-white">{{ $t('cart.total') }}</span>
                <span class="text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-ink" data-testid="cart-total">{{ totalLabel }}</span>
              </div>
            </div>
          </UiCard>

          <!-- Actions -->
          <div class="flex flex-col sm:flex-row gap-3">
            <UiButton variant="subtle" class="order-2 sm:order-1 flex-shrink-0" data-testid="cart-clear" @click="emit('clear')">
              <Trash2 :size="16" :stroke-width="2" class="mr-2" />
              {{ $t('cart.clearCart') }}
            </UiButton>

            <UiButton
              :to="localePath('/checkout')"
              size="lg"
              class="order-1 sm:order-2 flex-1 py-4 font-bold bg-gradient-to-r from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500 hover:scale-[1.02] shadow-lg shadow-cyan-500/20 [.tpl-classic_&]:bg-none [.tpl-classic_&]:shadow-none hover:[.tpl-classic_&]:scale-100"
              data-testid="cart-checkout"
            >
              <CreditCard :size="20" :stroke-width="2" class="mr-2" />
              {{ $t('cart.checkout') }}
            </UiButton>
          </div>

          <UiText size="xs" tone="subtle" class="text-center flex items-center justify-center gap-1.5">
            <Lock :size="12" :stroke-width="2" />
            {{ $t('cart.secureCheckout') }}
          </UiText>
        </UiStack>
      </div>
    </UiContainer>
  </section>
</template>

<script setup lang="ts">
/**
 * The body of the cart page: the empty notice, or the lines, the order summary and the checkout
 * actions. Under the classic template (the `tpl-classic` root class) the glow wash is dropped and
 * the checkout button is a solid brand button.
 *
 * Presentational: the page owns the cart store and the currency, and passes the lines, how many of
 * them are priced in a stale currency, the formatted total, a function that formats one line and, for a
 * cart filled from a bundle, the bundle line (name, discount in words, estimated amount taken off).
 */
import { CreditCard, Lock, Server, ShoppingCart, Trash2 } from 'lucide-vue-next'
import type { CartItem } from '~/types/cartitem'

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
