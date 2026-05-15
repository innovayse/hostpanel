<template>
  <div class="min-h-screen bg-[#0a0a10] text-white selection:bg-cyan-500/30">
    <!-- Hero Header -->
    <div class="relative py-12 md:py-20 overflow-hidden border-b border-white/5">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/10 rounded-full blur-[140px] animate-pulse" />
        <div class="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-cyan-500/10 rounded-full blur-[140px] animate-pulse animation-delay-2000" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto text-center"
             v-motion
             :initial="{ opacity: 0, y: 20 }"
             :enter="{ opacity: 1, y: 0 }">
          <div class="inline-flex items-center gap-2 px-3 py-1 mb-6 rounded-full glass border border-white/10 text-xs font-semibold tracking-wider uppercase text-cyan-400">
            <ShieldCheck :size="14" />
            Secure Checkout
          </div>
          <h1 class="text-4xl md:text-6xl font-black mb-6 tracking-tight bg-gradient-to-r from-white via-white to-white/60 bg-clip-text text-transparent">
            {{ $t('checkout.title') }}
          </h1>
          <p class="text-gray-400 text-lg max-w-xl mx-auto font-medium">
            {{ $t('checkout.subtitle') }}
          </p>
        </div>
      </div>
    </div>

    <!-- Main Content -->
    <section class="py-12 md:py-20 relative">
      <div class="container-custom relative z-10">
        
        <!-- Empty Cart State -->
        <div v-if="cart.isEmpty" 
             class="max-w-2xl mx-auto text-center py-20 bg-white/5 border border-white/10 rounded-3xl backdrop-blur-sm"
             v-motion
             :initial="{ opacity: 0, scale: 0.95 }"
             :enter="{ opacity: 1, scale: 1 }">
          <div class="w-20 h-20 rounded-full bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center mx-auto mb-6 text-cyan-400">
            <ShoppingCart :size="32" :stroke-width="1.5" />
          </div>
          <h2 class="text-2xl font-bold mb-3">{{ $t('cart.empty') }}</h2>
          <p class="text-gray-400 mb-10">{{ $t('cart.emptyDesc') }}</p>
          <UiButton :to="localePath('/hosting')" variant="primary" size="lg">
            {{ $t('cart.browseHosting') }}
          </UiButton>
        </div>

        <!-- Checkout Grid -->
        <div v-else class="grid grid-cols-1 lg:grid-cols-12 gap-10 items-start">
          
          <!-- Left Column: Steps -->
          <div class="lg:col-span-7 space-y-8">
            
            <!-- Account Section -->
            <section class="p-8 md:p-10 bg-white/5 border border-white/10 rounded-[32px] backdrop-blur-xl relative group transition-all duration-500 hover:border-white/20">
              <div class="flex items-center gap-4 mb-8">
                <div class="w-12 h-12 rounded-2xl bg-primary-500/20 border border-primary-500/30 flex items-center justify-center text-primary-400 group-hover:scale-110 transition-transform duration-500">
                  <User :size="24" :stroke-width="2" />
                </div>
                <div>
                  <h3 class="text-xl font-bold text-white">{{ $t('checkout.account') }}</h3>
                  <p class="text-sm text-gray-400">How should we identify you?</p>
                </div>
              </div>

              <!-- Logged in State -->
              <div v-if="isLoggedIn" 
                   class="flex items-center justify-between p-5 rounded-2xl bg-green-500/5 border border-green-500/20">
                <div class="flex items-center gap-4">
                  <div class="w-10 h-10 rounded-full bg-green-400/20 flex items-center justify-center text-green-400">
                    <CheckCircle :size="18" />
                  </div>
                  <div>
                    <div class="text-sm font-semibold text-white">{{ $t('checkout.loggedInAs') }}</div>
                    <div class="text-sm text-green-400/80">{{ userEmail }}</div>
                  </div>
                </div>
                <button @click="handleLogout" class="text-xs font-bold text-gray-500 hover:text-white transition-colors">
                  {{ $t('checkout.notYou') }}
                </button>
              </div>

              <!-- Guest Form -->
              <div v-else class="space-y-6">
                <div class="flex items-center gap-3 p-4 rounded-2xl bg-cyan-500/5 border border-cyan-500/10 mb-2">
                  <Info :size="16" class="text-cyan-400 shrink-0" />
                  <p class="text-sm text-gray-400">
                    {{ $t('checkout.guestNotice') }}
                    <NuxtLink :to="localePath('/client/login')" class="text-cyan-400 font-bold hover:underline ml-1">
                      {{ $t('checkout.signIn') }}
                    </NuxtLink>
                  </p>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
                  <UiInput v-model="form.firstname" 
                           :label="$t('checkout.fields.firstname')" 
                           placeholder="John" required 
                           size="md" />
                  <UiInput v-model="form.lastname" 
                           :label="$t('checkout.fields.lastname')" 
                           placeholder="Doe" required />
                </div>
                
                <UiInput v-model="form.email" 
                         type="email" 
                         :label="$t('checkout.fields.email')" 
                         placeholder="you@example.com" required />
                
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
                  <UiInput v-model="form.password" 
                           type="password" 
                           :label="$t('checkout.fields.password')" 
                           placeholder="••••••••" required />
                  <UiPhoneInput v-model="form.phonenumber" 
                                :label="$t('checkout.fields.phonenumber')" 
                                required />
                </div>
              </div>
            </section>

            <!-- Payment Method Section -->
            <section class="p-8 md:p-10 bg-white/5 border border-white/10 rounded-[32px] backdrop-blur-xl group transition-all duration-500 hover:border-white/20">
              <div class="flex items-center gap-4 mb-8">
                <div class="w-12 h-12 rounded-2xl bg-cyan-500/20 border border-cyan-500/30 flex items-center justify-center text-cyan-400 group-hover:scale-110 transition-transform duration-500">
                  <CreditCard :size="24" :stroke-width="2" />
                </div>
                <div>
                  <h3 class="text-xl font-bold text-white">{{ $t('checkout.paymentMethod') }}</h3>
                  <p class="text-sm text-gray-400">Select your preferred payment way</p>
                </div>
              </div>

              <div v-if="methodsPending" class="grid grid-cols-2 gap-3">
                <div v-for="i in 3" :key="i" class="h-14 rounded-2xl bg-white/5 animate-pulse" />
              </div>

              <div v-else-if="!paymentMethods.length" class="text-gray-500 italic p-6 text-center border border-dashed border-white/10 rounded-2xl">
                {{ $t('checkout.noPaymentMethods') }}
              </div>

              <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <label v-for="method in paymentMethods" 
                       :key="method.module"
                       class="relative flex flex-col p-4 rounded-2xl border cursor-pointer transition-all duration-300 group"
                       :class="selectedMethod === method.module 
                         ? 'border-cyan-500/60 bg-cyan-500/10 shadow-[0_0_20px_rgba(6,182,212,0.15)]' 
                         : 'border-white/10 hover:border-white/20 bg-white/5'">
                  <div class="flex items-center justify-between mb-2">
                    <div class="w-8 h-8 rounded-lg bg-white/5 flex items-center justify-center border border-white/10">
                      <CreditCard :size="16" :class="selectedMethod === method.module ? 'text-cyan-400' : 'text-gray-500'" />
                    </div>
                    <input v-model="selectedMethod" 
                           type="radio" 
                           :value="method.module" 
                           class="w-4 h-4 accent-cyan-500" />
                  </div>
                  <span class="text-sm font-bold mt-auto" :class="selectedMethod === method.module ? 'text-white' : 'text-gray-400'">
                    {{ method.displayname }}
                  </span>
                </label>
              </div>
            </section>

            <!-- Removed: Product Configuration Step (Now moved to post-payment Setup Wizard) -->

          </div>

          <!-- Right Column: Summary Card -->
          <aside class="lg:col-span-5 sticky top-24 space-y-6">
            <div class="p-8 bg-gradient-to-br from-[#1a1a24] to-[#121217] border border-white/10 rounded-[32px] shadow-2xl relative overflow-hidden ring-1 ring-white/10">
              <!-- Decorative background -->
              <div class="absolute -top-24 -right-24 w-48 h-48 bg-cyan-500/10 rounded-full blur-[80px]" />
              
              <h3 class="text-xl font-bold mb-8 flex items-center gap-3">
                <Receipt :size="20" class="text-cyan-400" />
                {{ $t('checkout.yourOrder') }}
              </h3>

              <div class="space-y-4 mb-8">
                <div v-for="item in cart.items" :key="item.pid" 
                     class="flex items-start justify-between gap-4 p-4 rounded-2xl bg-white/5 border border-white/5">
                  <div class="flex items-center gap-3">
                    <div class="w-10 h-10 rounded-xl bg-cyan-500/10 flex items-center justify-center text-cyan-400 shrink-0">
                      <Server :size="18" />
                    </div>
                    <div>
                      <div class="text-sm font-bold text-white truncate w-32 md:w-full">{{ item.name }}</div>
                      <div class="text-[10px] uppercase font-bold tracking-widest text-cyan-400/60 mt-0.5">{{ item.cycleLabel }}</div>
                    </div>
                  </div>
                  <div class="text-sm font-black text-white whitespace-nowrap">{{ item.price }}</div>
                </div>
              </div>

              <!-- Total Divider -->
              <div class="h-px bg-white/10 my-6 border-dashed border-t lg:border-t-0" />

              <div class="flex items-center justify-between mb-8">
                <span class="text-gray-400 font-medium">{{ $t('cart.total') }}</span>
                <span class="text-3xl font-black text-white bg-gradient-to-br from-white to-white/70 bg-clip-text">{{ totalLabel }}</span>
              </div>

              <!-- Error Box -->
              <div v-if="orderError"
                   class="mb-6 p-4 rounded-2xl bg-red-500/10 border border-red-500/20 flex gap-3 animate-shake">
                <AlertCircle :size="18" class="text-red-400 shrink-0 mt-0.5" />
                <p class="text-sm font-medium text-red-400 leading-snug">{{ orderError }}</p>
              </div>

              <!-- Submit Button -->
              <UiButton variant="primary" 
                        size="lg" 
                        full-width 
                        :loading="submitting"
                        :disabled="submitting || (cart.isEmpty)"
                        @click="submitOrder"
                        class="h-16 rounded-2xl text-lg font-bold shadow-2xl shadow-cyan-500/20 bg-gradient-to-tr from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500 transition-all duration-300">
                <Lock v-if="!submitting" :size="20" class="mr-2" />
                {{ submitting ? $t('checkout.processing') : $t('checkout.placeOrder') }}
              </UiButton>

              <div class="mt-6 flex items-center justify-center gap-2 text-xs text-gray-500 font-medium">
                <ShieldCheck :size="14" class="text-green-500" />
                {{ $t('cart.secureCheckout') }}
              </div>
            </div>

            <!-- Additional Help Card -->
            <div class="p-6 bg-white/5 border border-white/10 rounded-3xl flex items-center gap-4">
              <div class="w-10 h-10 rounded-full bg-white/5 flex items-center justify-center text-gray-400">
                <HelpCircle :size="20" />
              </div>
              <div>
                <div class="text-sm font-bold">Need Help?</div>
                <div class="text-xs text-gray-500">Our support is here 24/7</div>
              </div>
              <NuxtLink to="/contact" class="ml-auto text-xs font-bold text-cyan-400 hover:text-cyan-300 transition-colors">
                Contact Us
              </NuxtLink>
            </div>
          </aside>

        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { 
  CreditCard, ShoppingCart, Server, CheckCircle, User, 
  AlertCircle, Lock, ShieldCheck, Info, Receipt, HelpCircle, X 
} from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

const localePath = useLocalePath()
const { t: $t } = useI18n()

const cart = useCartStore()
const { isLoggedIn, fetchUser, user, logout, login } = useClientAuth()

// Registration from previous version
onMounted(async () => {
  cart.init()
  if (isLoggedIn.value) await fetchUser()
})

// ── Payment methods ────────────────────────────────────────────────────────

const { data: paymentMethods, pending: methodsPending } = await useApi<{ module: string; displayname: string }[]>(
  '/api/portal/order/payment-methods',
  { default: () => [] }
)
const selectedMethod = ref('')
watch(paymentMethods, (methods) => {
  if (methods?.length && !selectedMethod.value) {
    selectedMethod.value = methods[0].module
  }
}, { immediate: true })

// ── Auth state ─────────────────────────────────────────────────────────────

const userEmail = computed(() => user.value?.email ?? '')

const handleLogout = async () => {
  await logout()
}

// ── Guest form ─────────────────────────────────────────────────────────────

const form = reactive({
  firstname: '',
  lastname: '',
  email: '',
  password: '',
  phonenumber: ''
})

// ── Product Configuration ────────────────────────────────────────────────

const isHostingProduct = (name: string) => 
  name.toLowerCase().includes('hosting') || 
  name.toLowerCase().includes('plan') || 
  name.toLowerCase().includes('webmaster')

const hostingItems = computed(() => cart.items.filter(i => isHostingProduct(i.name)))
const hostingItemsCount = computed(() => hostingItems.value.length)

// ── Order total ────────────────────────────────────────────────────────────

const totalLabel = computed(() => {
  const items = cart.items
  if (!items.length) return '0.00'
  const prefixes = [...new Set(items.map(i => i.prefix))]
  if (prefixes.length > 1) return $t('cart.multipleCurrencies')
  const prefix = prefixes[0] ?? ''
  const sum = items.reduce((acc, i) => acc + parseFloat(i.rawPrice || '0'), 0)
  return `${prefix}${sum.toFixed(2)}`
})

// ── Submit ─────────────────────────────────────────────────────────────────

const submitting = ref(false)
const orderError = ref('')

async function submitOrder() {
  if (!selectedMethod.value || submitting.value) return
  orderError.value = ''

  const body: Record<string, unknown> = {
      items: cart.items.map(i => ({ 
        pid: i.pid, 
        billingcycle: i.billingcycle
      })),
      paymentmethod: selectedMethod.value
  }

  if (!isLoggedIn.value) {
    if (!form.firstname || !form.lastname || !form.email || !form.password || !form.phonenumber) {
      orderError.value = $t('checkout.errors.fillRequired')
      return
    }
    Object.assign(body, { ...form })
  }

  // Hosting detail validation moved to post-payment setup wizard


  submitting.value = true
  try {
    const result = await apiFetch<{ orderId: number; invoiceId: number; invoiceUrl: string }>(
      '/api/portal/order/create',
      { method: 'POST', body }
    )

    cart.clear()

    // Auto-login guest after account creation
    if (!isLoggedIn.value && form.email && form.password) {
      try { await login(form.email, form.password) } catch { /* ignore — middleware will handle */ }
    }

    // Redirect to our own invoice pay page
    if (result.invoiceId) {
      await navigateTo(localePath(`/client/invoices/${result.invoiceId}/pay`))
    } else {
      await navigateTo(localePath('/client/invoices'))
    }
  } catch (err: unknown) {
    const msg = (err as { data?: { statusMessage?: string }; message?: string })?.data?.statusMessage
      ?? (err as { message?: string })?.message
      ?? $t('checkout.errors.generic')
    orderError.value = msg
  } finally {
    submitting.value = false
  }
}

useHead({ title: computed(() => `${$t('checkout.title')} — Innovayse`) })
</script>

<style scoped>
.glass {
  background: rgba(255, 255, 255, 0.03);
  backdrop-filter: blur(8px);
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  75% { transform: translateX(4px); }
}

.animate-shake {
  animation: shake 0.2s ease-in-out 0s 2;
}

/* Custom transitions */
.v-enter-active,
.v-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.v-enter-from,
.v-leave-to {
  opacity: 0;
  transform: translateY(10px);
}
</style>
