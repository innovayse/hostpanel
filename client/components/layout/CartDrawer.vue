<template>
  <UiDrawer
    :model-value="cart.isOpen"
    :title="$t('cart.title')"
    @close="cart.close()"
  >
    <template #header>
      <UiStack direction="row" align="center" gap="sm">
        <UiStack direction="row" align="center" justify="center" gap="none" class="w-10 h-10 rounded-xl bg-primary-500/10 border border-primary-500/20 [html[data-template=classic]_&]:rounded-md [html[data-template=classic]_&]:border-classic-border [html[data-template=classic]_&]:bg-classic-brand-soft">
          <ShoppingCart :size="20" class="text-primary-400 [html[data-template=classic]_&]:text-classic-brand" />
        </UiStack>
        <UiStack gap="none">
          <UiHeading :level="UiHeadingLevel.H2" size="subsection" class="text-xl font-bold leading-tight">{{ $t('cart.title') }}</UiHeading>
          <UiText size="xs" tone="subtle" data-testid="cart-drawer-count">{{ cart.count }} {{ $t('cart.itemsCount') }}</UiText>
        </UiStack>
      </UiStack>
    </template>

    <!-- Empty state -->
    <UiEmptyState
      v-if="cart.isEmpty"
      :icon="ShoppingCart"
      :title="$t('cart.empty')"
      :description="$t('cart.emptyHint')"
      data-testid="cart-drawer-empty"
    >
      <UiButton variant="primary" size="lg" data-testid="cart-drawer-browse" @click="handleBrowse">
        {{ $t('cart.browseHosting') }}
      </UiButton>
    </UiEmptyState>

    <!-- Items -->
    <UiStack v-else gap="md" data-testid="cart-drawer-items">
      <!-- Notice for items priced in a currency the payer no longer holds -->
      <UiAlert v-if="staleItems.length" variant="warning" data-testid="cart-drawer-stale">
        {{ $t('cart.staleCurrencyNotice') }}
      </UiAlert>

      <UiCard
        v-for="item in visibleItems"
        :key="item.pid"
        padding="sm"
        hover
        class="group relative"
        data-testid="cart-drawer-item"
      >
        <UiStack direction="row" gap="md">
          <UiStack direction="row" align="center" justify="center" gap="none" class="w-12 h-12 flex-shrink-0 rounded-xl bg-cyan-500/10 border border-cyan-500/20 group-hover:scale-110 transition-transform [html[data-template=classic]_&]:rounded-md [html[data-template=classic]_&]:border-classic-border [html[data-template=classic]_&]:bg-classic-brand-soft [html[data-template=classic]_&]:group-hover:scale-100">
            <Server :size="22" class="text-cyan-400 [html[data-template=classic]_&]:text-classic-brand" />
          </UiStack>
          <UiStack gap="none" class="flex-1 min-w-0">
            <UiText weight="bold" truncate class="pr-6">{{ item.name }}</UiText>
            <UiText size="xs" tone="subtle" class="mb-2">{{ item.cycleLabel }}</UiText>
            <UiText size="lg" weight="bold" data-testid="cart-drawer-item-price">{{ itemPrice(item) }}</UiText>
          </UiStack>
        </UiStack>
        <UiIconButton
          :icon="Trash2"
          :label="$t('cart.remove')"
          size="sm"
          variant="danger"
          class="absolute top-3 right-3 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-all"
          testid="cart-drawer-remove"
          @click="cart.removeItem(item.pid, item.billingcycle, item.domain, item.domainAction)"
        />
      </UiCard>
    </UiStack>

    <template v-if="!cart.isEmpty" #footer>
      <UiStack gap="md">
        <UiStack gap="xs">
          <UiStack direction="row" align="center" justify="between" gap="none">
            <UiText size="sm" tone="subtle">{{ $t('cart.summary') }}</UiText>
            <UiText size="sm" weight="medium">{{ cart.count }} {{ $t('cart.items') }}</UiText>
          </UiStack>
          <CheckoutBundleSummary :name="bundleName" :offer="bundleOffer" :discount="bundleDiscount" />
          <UiStack direction="row" align="center" justify="between" gap="none" class="pt-2 border-t border-gray-200 dark:border-white/5">
            <UiText size="lg" weight="bold">{{ $t('cart.total') }}</UiText>
            <UiGradientText class="text-3xl font-black bg-gradient-to-r from-cyan-400 to-primary-400 [html[data-template=classic]_&]:bg-none [html[data-template=classic]_&]:text-classic-ink [html[data-template=classic]_&]:text-2xl [html[data-template=classic]_&]:font-bold" data-testid="cart-drawer-total">{{ totalLabel }}</UiGradientText>
          </UiStack>
        </UiStack>

        <UiStack gap="sm" class="pt-2">
          <UiButton
            variant="primary"
            size="lg"
            full-width
            class="!h-14 font-black uppercase tracking-widest shadow-xl shadow-cyan-500/20 [html[data-template=classic]_&]:!h-12 [html[data-template=classic]_&]:font-semibold [html[data-template=classic]_&]:normal-case [html[data-template=classic]_&]:tracking-normal [html[data-template=classic]_&]:shadow-none"
            data-testid="cart-drawer-checkout"
            @click="handleCheckout"
          >
            <CreditCard :size="20" class="mr-2" />
            {{ $t('cart.checkout') }}
          </UiButton>
          <UiButton
            variant="ghost"
            size="sm"
            class="text-xs font-bold text-gray-600 dark:text-gray-400 hover:text-red-400 uppercase tracking-widest [html[data-template=classic]_&]:normal-case [html[data-template=classic]_&]:tracking-normal"
            data-testid="cart-drawer-clear"
            @click="cart.clear()"
          >
            <Trash2 :size="14" class="mr-2" />
            {{ $t('cart.clearCart') }}
          </UiButton>
        </UiStack>

        <UiText size="xs" tone="subtle" class="flex items-center justify-center gap-1.5 pt-2 uppercase tracking-widest text-[10px]">
          <Lock :size="10" />
          {{ $t('cart.secureCheckout') }}
        </UiText>
      </UiStack>
    </template>
  </UiDrawer>
</template>

<script setup lang="ts">
/**
 * CartDrawer. The slide-in cart: the items in the payer's currency, a notice for items left in
 * another currency, the total, and the way on to checkout.
 *
 * The panel, backdrop, close button and Escape handling are the kit's `UiDrawer`; this component
 * supplies the cart content and closes the drawer whenever the route changes.
 *
 * The drawer is teleported to the body, outside the layout root, so its classic styling is scoped on
 * `html[data-template=classic]` rather than on the `tpl-classic` root class.
 */
import { ShoppingCart, Server, Trash2, CreditCard, Lock } from 'lucide-vue-next'
import { useCartBundle } from '~/composables/useCartBundle'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { formatMoney } from '~/utils/formatMoney'
import { UiHeadingLevel } from '~/utils/ui/enums'

/** The cart store: items, totals and the open flag the drawer follows. */
const cart = useCartStore()

/** The currency store: which currency the payer is billed in. */
const currencyStore = useCurrencyStore()

/** The cart's bundle as display text: its name, its discount in words and the estimated amount taken off. */
const { bundleName, bundleOffer, bundleDiscount } = useCartBundle()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Router used to leave the drawer for the hosting list or the checkout. */
const router = useRouter()

/** Current route; a change closes the drawer. */
const route = useRoute()

/**
 * Returns the display price for a cart item, in the currency it was actually added in, never
 * converted, never re-labelled by the page language. See `stores/cart.ts`'s module note.
 *
 * @param item - Cart item.
 * @returns Formatted price string.
 */
const itemPrice = (item: typeof cart.items[number]): string =>
  formatMoney(item.amount, currencyStore.moneyCurrencyFor(item.currency))

/** Items priced in a currency other than the payer's current one, excluded from the total. */
const staleItems = computed(() => cart.staleItems(currencyStore.payerCode))

/** Items priced in the payer's current currency, the ones listed and totalled. */
const visibleItems = computed(() => cart.itemsInCurrency(currencyStore.payerCode))

/** The formatted total of the listed items, empty while the cart has none. */
const totalLabel = computed(() => {
  if (!cart.items.length) {
    return ''
  }
  return formatMoney(cart.total(currencyStore.payerCode), currencyStore.moneyCurrencyFor(currencyStore.payerCode))
})

/** Closes the drawer and goes to the hosting plans. */
const handleBrowse = (): void => {
  cart.close()
  router.push(localePath('/hosting'))
}

/** Closes the drawer and goes to the checkout. */
const handleCheckout = (): void => {
  cart.close()
  router.push(localePath('/checkout'))
}

onMounted(() => {
  currencyStore.init()
  currencyStore.load()
})

// Close the drawer when the route changes
watch(() => route.path, () => {
  cart.close()
})
</script>
