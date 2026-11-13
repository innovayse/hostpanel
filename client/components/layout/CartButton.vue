<template>
  <UiIconButton
    :icon="ShoppingCart"
    :label="$t('cart.title')"
    variant="bare"
    size="lg"
    :class="[HEADER_CONTROL, 'relative']"
    testid="layout-cart-button"
    @click="cart.open()"
  >
    <ClientOnly>
      <UiStack
        v-if="cart.count > 0"
        as="span"
        direction="row"
        align="center"
        justify="center"
        gap="none"
        class="absolute -top-1.5 -right-1.5 min-w-[18px] h-[18px] px-1 rounded-full bg-classic-fill text-classic-on-brand text-[10px] font-bold"
        data-testid="layout-cart-count"
      >
        {{ cart.count }}
      </UiStack>
    </ClientOnly>
  </UiIconButton>
</template>

<script setup lang="ts">
/**
 * LayoutCartButton: the header's cart icon with the item-count badge; a click opens the cart
 * drawer. The badge is client-only because the cart lives in localStorage and the server render
 * cannot know it. It wears the classic header's shared control frame (height, radius, border).
 */
import { ShoppingCart } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'
import { HEADER_CONTROL } from '~/utils/headerControl'

/** The cart store: the count shown in the badge and the action that opens the drawer. */
const cart = useCartStore()
</script>
