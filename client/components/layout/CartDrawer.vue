<template>
  <Teleport to="body">
    <div v-if="cart.isOpen" class="fixed inset-0 z-50 overflow-hidden">
      <!-- Backdrop -->
      <div 
        class="absolute inset-0 bg-black/60 backdrop-blur-sm transition-opacity" 
        @click="cart.close()"
      />

      <!-- Drawer Panel -->
      <div class="absolute inset-y-0 right-0 max-w-full flex pl-10">
        <div 
          v-motion
          :initial="{ x: '100%' }"
          :enter="{ x: 0, transition: { type: 'spring', damping: 25, stiffness: 200 } }"
          :leave="{ x: '100%', transition: { duration: 300 } }"
          class="w-screen max-w-md h-full bg-[#0a0a0f] border-l border-white/10 shadow-2xl flex flex-col"
        >
          <!-- Header -->
          <div class="px-6 py-6 border-b border-white/10 flex items-center justify-between bg-white/[0.02]">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center">
                <ShoppingCart :size="20" class="text-primary-400" />
              </div>
              <div>
                <h2 class="text-xl font-bold text-white leading-tight">{{ $t('cart.title') }}</h2>
                <p class="text-xs text-gray-500">{{ cart.count }} {{ $t('cart.itemsCount') || 'items' }}</p>
              </div>
            </div>
            <button 
              class="w-10 h-10 rounded-xl flex items-center justify-center text-gray-400 hover:text-white hover:bg-white/5 transition-all"
              @click="cart.close()"
            >
              <X :size="24" />
            </button>
          </div>

          <!-- Items list -->
          <div class="flex-1 overflow-y-auto p-6 space-y-4 no-scrollbar">
            <!-- Empty state -->
            <div v-if="cart.isEmpty" class="h-full flex flex-col items-center justify-center text-center py-12">
              <div class="w-20 h-20 rounded-full bg-white/5 border border-white/10 flex items-center justify-center mb-6">
                <ShoppingCart :size="32" class="text-gray-600" />
              </div>
              <h3 class="text-lg font-bold text-gray-400 mb-2">{{ $t('cart.empty') }}</h3>
              <p class="text-sm text-gray-500 mb-8 max-w-[240px]">{{ $t('cart.emptyHint') }}</p>
              <UiButton variant="primary" size="lg" @click="handleBrowse">
                {{ $t('cart.browseHosting') || 'Browse Hosting' }}
              </UiButton>
            </div>

            <!-- Items -->
            <template v-else>
              <div 
                v-for="item in cart.items" 
                :key="item.pid"
                class="group relative p-4 rounded-2xl bg-white/[0.03] border border-white/10 hover:border-primary-500/30 transition-all duration-300"
              >
                <div class="flex gap-4">
                   <div class="w-12 h-12 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                      <Server :size="22" class="text-cyan-400" />
                   </div>
                   <div class="flex-1 min-w-0">
                      <div class="flex justify-between items-start mb-1">
                        <h4 class="font-bold text-white truncate pr-6">{{ item.name }}</h4>
                        <button 
                          class="absolute top-4 right-4 text-gray-600 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-all"
                          @click="cart.removeItem(item.pid)"
                        >
                          <Trash2 :size="16" />
                        </button>
                      </div>
                      <div class="text-xs text-gray-500 mb-2">{{ item.cycleLabel }}</div>
                      <div class="flex justify-between items-end">
                        <div class="text-lg font-black text-white">{{ item.price }}</div>
                      </div>
                   </div>
                </div>
              </div>
            </template>
          </div>

          <!-- Footer -->
          <div v-if="!cart.isEmpty" class="p-6 border-t border-white/10 bg-white/[0.02] space-y-4">
             <div class="space-y-2">
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">{{ $t('cart.summary') }}</span>
                  <span class="text-white font-medium">{{ cart.count }} {{ $t('cart.items') || 'items' }}</span>
                </div>
                <div class="flex justify-between items-baseline pt-2 border-t border-white/5">
                  <span class="text-lg font-bold text-white">{{ $t('cart.total') }}</span>
                  <span class="text-3xl font-black text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 to-primary-400">{{ totalLabel }}</span>
                </div>
             </div>

             <div class="flex flex-col gap-3 pt-2">
               <UiButton 
                  variant="primary" 
                  size="lg" 
                  full-width 
                  class="!h-14 font-black uppercase tracking-widest shadow-xl shadow-cyan-500/20"
                  @click="handleCheckout"
               >
                 <CreditCard :size="20" class="mr-2" />
                 {{ $t('cart.checkout') }}
               </UiButton>
               <button 
                  class="text-xs font-bold text-gray-500 hover:text-red-400 uppercase tracking-widest transition-colors flex items-center justify-center gap-2"
                  @click="cart.clear()"
                >
                  <Trash2 :size="14" />
                  {{ $t('cart.clearCart') }}
               </button>
             </div>
             
             <p class="text-[10px] text-center text-gray-600 uppercase tracking-widest flex items-center justify-center gap-1.5 pt-2">
                <Lock :size="10" />
                {{ $t('cart.secureCheckout') }}
             </p>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ShoppingCart, Server, Trash2, CreditCard, Lock, X } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

const cart = useCartStore()
const localePath = useLocalePath()
const { t: $t } = useI18n()
const router = useRouter()

const totalLabel = computed(() => {
  const items = cart.items
  if (!items.length) return ''
  const prefixes = [...new Set(items.map(i => i.prefix))]
  if (prefixes.length > 1) return $t('cart.multipleCurrencies') || 'Multiple'
  const prefix = prefixes[0] ?? ''
  const sum = items.reduce((acc, i) => acc + parseFloat(i.rawPrice || '0'), 0)
  return `${prefix}${sum.toFixed(2)}`
})

function handleBrowse() {
  cart.close()
  router.push(localePath('/hosting'))
}

function handleCheckout() {
  cart.close()
  router.push(localePath('/checkout'))
}

// Close on Escape
onMounted(() => {
  if (import.meta.client) {
    window.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && cart.isOpen) cart.close()
    })
  }
})

// Watch route change to close drawer
const route = useRoute()
watch(() => route.path, () => {
  cart.close()
})
</script>

<style scoped>
.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
</style>
