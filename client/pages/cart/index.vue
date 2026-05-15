<template>
  <div>
    <!-- Hero -->
    <section class="relative py-14 md:py-20 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[140px] animate-blob" />
        <div class="absolute bottom-0 right-1/3 w-[400px] h-[400px] bg-cyan-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </div>
      <div class="container-custom relative z-10 text-center max-w-2xl mx-auto">
        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-primary-500/20 backdrop-blur-sm">
          <ShoppingCart :size="14" :stroke-width="2" class="text-primary-400" />
          <span class="text-sm font-medium text-gray-300">{{ $t('cart.title') }}</span>
        </div>
        <h1 class="text-4xl md:text-5xl font-bold text-white mb-4 leading-tight">{{ $t('cart.title') }}</h1>
        <p class="text-gray-400 text-lg">{{ $t('cart.subtitle') }}</p>
      </div>
    </section>

    <!-- Content -->
    <section class="py-10 md:py-16 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-bl from-primary-500/5 via-transparent to-cyan-500/5" />
      </div>
      <div class="container-custom relative z-10 max-w-3xl mx-auto">

      <!-- Empty cart -->
      <div v-if="cart.isEmpty" class="text-center py-24">
        <ShoppingCart :size="64" :stroke-width="1.5" class="text-gray-600 mx-auto mb-6" />
        <h2 class="text-xl font-semibold text-gray-400 mb-2">{{ $t('cart.empty') }}</h2>
        <p class="text-gray-500 mb-8">{{ $t('cart.emptyHint') }}</p>
        <NuxtLink
          :to="localePath('/hosting')"
          class="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-cyan-500 text-white font-semibold hover:bg-cyan-400 transition-all duration-200"
        >
          <Server :size="18" :stroke-width="2" />
          {{ $t('cart.browseHosting') }}
        </NuxtLink>
      </div>

      <div v-else class="space-y-6">
        <!-- Cart Items -->
        <div class="space-y-3">
          <TransitionGroup name="cart-item" tag="div" class="space-y-3">
            <div
              v-for="item in cart.items"
              :key="item.pid"
              class="flex items-center justify-between gap-4 p-4 rounded-xl bg-white/5 border border-white/10 hover:border-white/20 transition-colors"
            >
              <!-- Info -->
              <div class="flex items-center gap-4 min-w-0">
                <div class="w-10 h-10 rounded-lg bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0">
                  <Server :size="18" :stroke-width="2" class="text-cyan-400" />
                </div>
                <div class="min-w-0">
                  <div class="font-semibold text-white truncate">{{ item.name }}</div>
                  <div class="text-xs text-gray-500 mt-0.5">{{ item.cycleLabel }}</div>
                </div>
              </div>

              <!-- Price + Remove -->
              <div class="flex items-center gap-4 flex-shrink-0">
                <div class="text-right">
                  <div class="text-lg font-bold text-white">{{ item.price }}</div>
                  <div class="text-xs text-gray-500">/ {{ item.cycleLabel }}</div>
                </div>
                <button
                  class="w-8 h-8 rounded-lg flex items-center justify-center text-gray-500 hover:text-red-400 hover:bg-red-500/10 transition-all duration-200"
                  :title="$t('cart.remove')"
                  @click="cart.removeItem(item.pid)"
                >
                  <Trash2 :size="16" :stroke-width="2" />
                </button>
              </div>
            </div>
          </TransitionGroup>
        </div>

        <!-- Order Summary -->
        <UiCard :title="$t('cart.summary')">
          <div class="space-y-2">
            <div v-for="item in cart.items" :key="item.pid" class="flex justify-between text-sm">
              <span class="text-gray-400 truncate max-w-[60%]">{{ item.name }}</span>
              <span class="text-white">{{ item.price }}</span>
            </div>
            <div class="border-t border-white/10 pt-3 mt-3 flex justify-between font-bold text-base">
              <span class="text-white">{{ $t('cart.total') }}</span>
              <span class="text-cyan-400">{{ totalLabel }}</span>
            </div>
          </div>
        </UiCard>

        <!-- Checkout CTA -->
        <div class="flex flex-col sm:flex-row gap-3">
          <!-- Clear cart -->
          <button
            class="order-2 sm:order-1 flex-shrink-0 px-5 py-3 rounded-xl border border-white/10 text-gray-400 hover:text-red-400 hover:border-red-500/30 font-medium text-sm transition-all duration-200 flex items-center justify-center gap-2"
            @click="cart.clear()"
          >
            <Trash2 :size="16" :stroke-width="2" />
            {{ $t('cart.clearCart') }}
          </button>

          <!-- Checkout -->
          <NuxtLink
            :to="localePath('/checkout')"
            class="order-1 sm:order-2 flex-1 py-4 rounded-xl font-bold text-white text-center bg-gradient-to-r from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500 transition-all duration-300 hover:scale-[1.02] shadow-lg shadow-cyan-500/20 flex items-center justify-center gap-2"
          >
            <CreditCard :size="20" :stroke-width="2" />
            {{ $t('cart.checkout') }}
          </NuxtLink>
        </div>

        <!-- Trust line -->
        <p class="text-center text-xs text-gray-500 flex items-center justify-center gap-1.5">
          <Lock :size="12" :stroke-width="2" />
          {{ $t('cart.secureCheckout') }}
        </p>
      </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ShoppingCart, Server, Trash2, CreditCard, Lock } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

const localePath = useLocalePath()
const { t: $t } = useI18n()

const cart = useCartStore()

// Load cart from localStorage on mount
onMounted(() => cart.init())

/** Summary label: if all items have the same prefix, show total; otherwise show "Multiple currencies" */
const totalLabel = computed(() => {
  const items = cart.items
  if (!items.length) return ''
  const prefixes = [...new Set(items.map(i => i.prefix))]
  if (prefixes.length > 1) return $t('cart.multipleCurrencies')
  const prefix = prefixes[0] ?? ''
  const sum = items.reduce((acc, i) => acc + parseFloat(i.rawPrice || '0'), 0)
  return `${prefix}${sum.toFixed(2)}`
})

useHead({ title: computed(() => `${$t('cart.title')} — Innovayse`) })
</script>

<style scoped>
.cart-item-enter-active,
.cart-item-leave-active {
  transition: all 0.3s ease;
}
.cart-item-enter-from {
  opacity: 0;
  transform: translateX(-20px);
}
.cart-item-leave-to {
  opacity: 0;
  transform: translateX(20px);
}
</style>
