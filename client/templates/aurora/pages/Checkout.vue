<template>
  <div class="relative mx-auto min-h-dvh max-w-[1440px] overflow-hidden font-aurora text-tx">
    <div
      class="pointer-events-none absolute -top-[280px] left-1/2 h-[600px] w-[1000px] -translate-x-1/2 bg-glow1"
    />

    <section class="relative px-[clamp(20px,5vw,48px)] pb-5 pt-[clamp(36px,5vw,56px)]">
      <div class="flex flex-wrap items-center gap-2.5 text-sm text-mut2">
        <NuxtLink :to="localePath('/hosting')" class="hover:text-ac1">{{ $t('checkout.title') }}</NuxtLink>
      </div>
      <h1 class="mt-3.5 font-display text-[clamp(28px,4.4vw,44px)] font-bold -tracking-[0.02em] text-tx">
        {{ $t('checkout.title') }}
      </h1>
      <p class="mt-3 max-w-[560px] text-[17px] leading-relaxed text-mut">{{ $t('checkout.subtitle') }}</p>
      <div class="mt-3.5 flex items-center gap-2.5 text-sm text-mut">
        <ShieldCheck :size="16" class="text-ok" />
        {{ $t('cart.secureCheckout') }}
      </div>
    </section>

    <section class="relative px-[clamp(20px,5vw,48px)] pb-[clamp(56px,8vw,84px)]">
      <!-- Empty cart -->
      <div
        v-if="cart.isEmpty"
        class="mx-auto max-w-2xl rounded-[22px] border border-line bg-surf py-20 text-center"
      >
        <div class="mx-auto mb-6 grid h-20 w-20 place-items-center rounded-full border border-line2 bg-acbg text-ac1">
          <ShoppingCart :size="32" :stroke-width="1.5" />
        </div>
        <h2 class="mb-3 font-display text-2xl font-bold text-tx">{{ $t('cart.empty') }}</h2>
        <p class="mb-10 text-mut">{{ $t('cart.emptyDesc') }}</p>
        <NuxtLink
          :to="localePath('/hosting')"
          class="rounded-xl bg-brand px-7 py-3.5 text-base font-bold text-[#08090F] hover:brightness-110"
        >{{ $t('cart.browseHosting') }}</NuxtLink>
      </div>

      <div v-else class="grid items-start gap-6 [grid-template-columns:repeat(auto-fit,minmax(min(100%,340px),1fr))]">
        <!-- Left: steps -->
        <div class="flex min-w-0 flex-col gap-5">
          <!-- Account -->
          <section class="rounded-[18px] border border-line bg-surf p-6">
            <div class="flex items-center gap-3">
              <span class="grid h-10 w-10 place-items-center rounded-xl border border-line2 bg-acbg text-ac1">
                <User :size="20" :stroke-width="2" />
              </span>
              <h3 class="text-[17px] font-bold text-tx">{{ $t('checkout.account') }}</h3>
            </div>

            <div
              v-if="isLoggedIn"
              class="mt-5 flex flex-wrap items-center justify-between gap-4 rounded-xl border border-line bg-surf p-4"
            >
              <div class="flex items-center gap-3">
                <span class="grid h-10 w-10 place-items-center rounded-full bg-acbg text-ok">
                  <CheckCircle :size="18" />
                </span>
                <div>
                  <div class="text-sm font-semibold text-tx">{{ $t('checkout.loggedInAs') }}</div>
                  <div class="text-sm text-ok">{{ userEmail }}</div>
                </div>
              </div>
              <button type="button" class="text-xs font-bold text-mut2 hover:text-tx" @click="handleLogout">
                {{ $t('checkout.notYou') }}
              </button>
            </div>

            <div v-else class="mt-5 flex flex-col gap-5">
              <div class="flex items-center gap-3 rounded-xl border border-line2 bg-acbg p-4">
                <Info :size="16" class="shrink-0 text-ac1" />
                <p class="text-sm text-mut">
                  {{ $t('checkout.guestNotice') }}
                  <NuxtLink :to="localePath('/client/login')" class="ml-1 font-bold text-ac1 hover:underline">
                    {{ $t('checkout.signIn') }}
                  </NuxtLink>
                </p>
              </div>

              <div class="grid gap-4 [grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr))]">
                <UiInput v-model="form.firstname" :label="$t('checkout.fields.firstname')" placeholder="John" required size="md" />
                <UiInput v-model="form.lastname" :label="$t('checkout.fields.lastname')" placeholder="Doe" required />
              </div>

              <UiInput v-model="form.email" type="email" :label="$t('checkout.fields.email')" placeholder="you@example.com" required />

              <div class="grid gap-4 [grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr))]">
                <UiInput v-model="form.password" type="password" :label="$t('checkout.fields.password')" placeholder="••••••••" required />
                <UiPhoneInput v-model="form.phonenumber" :label="$t('checkout.fields.phonenumber')" required />
              </div>
            </div>
          </section>

          <!-- Payment method -->
          <section class="rounded-[18px] border border-line bg-surf p-6">
            <div class="flex items-center gap-3">
              <span class="grid h-10 w-10 place-items-center rounded-xl border border-line2 bg-acbg text-ac1">
                <CreditCard :size="20" :stroke-width="2" />
              </span>
              <h3 class="text-[17px] font-bold text-tx">{{ $t('checkout.paymentMethod') }}</h3>
            </div>

            <div v-if="methodsPending" class="mt-5 grid grid-cols-2 gap-3">
              <div v-for="i in 3" :key="i" class="h-14 animate-pulse rounded-xl bg-line" />
            </div>

            <div
              v-else-if="!paymentMethods.length"
              class="mt-5 rounded-xl border border-dashed border-line2 p-6 text-center text-mut2"
            >{{ $t('checkout.noPaymentMethods') }}</div>

            <div v-else class="mt-5 grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr))]">
              <label
                v-for="method in paymentMethods"
                :key="method.module"
                class="flex cursor-pointer flex-col rounded-xl border p-4"
                :class="selectedMethod === method.module ? 'border-ac1 bg-acbg' : 'border-line bg-surf hover:border-line2'"
              >
                <div class="mb-2 flex items-center justify-between">
                  <span class="grid h-8 w-8 place-items-center rounded-lg border border-line2">
                    <CreditCard :size="16" :class="selectedMethod === method.module ? 'text-ac1' : 'text-mut2'" />
                  </span>
                  <input v-model="selectedMethod" type="radio" :value="method.module" class="h-4 w-4 accent-[#5D3FFF]">
                </div>
                <span class="mt-auto text-sm font-bold" :class="selectedMethod === method.module ? 'text-tx' : 'text-mut'">
                  {{ method.displayname }}
                </span>
              </label>
            </div>

            <div v-if="selectedMethod === PAYMENT_MODULE_STRIPE" class="mt-6 overflow-hidden">
              <CheckoutStripeCardForm ref="stripeCardFormRef" />
            </div>
          </section>
        </div>

        <!-- Right: summary -->
        <aside class="flex min-w-0 flex-col gap-5">
          <div class="rounded-[20px] border border-line2 bg-card p-6 shadow-panel">
            <h3 class="flex items-center gap-3 text-[17px] font-bold text-tx">
              <Receipt :size="20" class="text-ac1" />
              {{ $t('checkout.yourOrder') }}
            </h3>

            <div class="mt-5 flex flex-col gap-3">
              <div
                v-for="item in cart.items"
                :key="item.pid"
                class="flex items-start justify-between gap-4 rounded-xl border border-line bg-surf p-4"
              >
                <div class="flex min-w-0 items-center gap-3">
                  <span class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-acbg text-ac1">
                    <Server :size="18" />
                  </span>
                  <div class="min-w-0">
                    <div class="truncate text-sm font-bold text-tx">{{ item.name }}</div>
                    <div class="mt-0.5 text-[10px] font-bold uppercase tracking-widest text-ac1">{{ item.cycleLabel }}</div>
                  </div>
                </div>
                <div class="whitespace-nowrap text-sm font-bold text-tx">{{ formatCartItemPrice(item, checkoutCurrency) }}</div>
              </div>
            </div>

            <div class="my-6 h-px bg-line" />

            <div class="mb-6 flex items-center justify-between">
              <span class="font-medium text-mut">{{ $t('cart.total') }}</span>
              <span class="text-3xl font-bold -tracking-[0.02em] text-tx">{{ totalLabel }}</span>
            </div>

            <div
              v-if="orderError"
              class="mb-5 flex gap-3 rounded-xl border border-danger bg-surf p-4"
            >
              <AlertCircle :size="18" class="mt-0.5 shrink-0 text-danger" />
              <p class="text-sm font-medium leading-snug text-danger">{{ orderError }}</p>
            </div>

            <UiButton
              variant="primary"
              size="lg"
              full-width
              :loading="submitting"
              :disabled="submitting || (cart.isEmpty)"
              class="h-14 rounded-xl text-base font-bold"
              @click="submitOrder"
            >
              <Lock v-if="!submitting" :size="20" class="mr-2" />
              {{ submitting ? submittingLabel : $t('checkout.placeOrder') }}
            </UiButton>

            <div class="mt-5 flex items-center justify-center gap-2 text-xs text-mut2">
              <ShieldCheck :size="14" class="text-ok" />
              {{ $t('cart.secureCheckout') }}
            </div>
          </div>

          <div class="flex items-center gap-4 rounded-[18px] border border-line bg-surf p-5">
            <span class="grid h-10 w-10 place-items-center rounded-full border border-line2 text-mut">
              <HelpCircle :size="20" />
            </span>
            <div>
              <div class="text-sm font-bold text-tx">{{ $t('aurora.checkout.helpTitle') }}</div>
              <div class="text-xs text-mut2">{{ $t('aurora.checkout.helpBody') }}</div>
            </div>
            <NuxtLink :to="localePath('/contact')" class="ml-auto text-xs font-bold text-ac1 hover:text-ac2">
              {{ $t('aurora.checkout.helpCta') }}
            </NuxtLink>
          </div>
        </aside>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { 
  CreditCard, ShoppingCart, Server, CheckCircle, User, 
  AlertCircle, Lock, ShieldCheck, Info, Receipt, HelpCircle, X 
} from 'lucide-vue-next'
import { useCartStore, formatCartItemPrice, convertFromAmd } from '~/stores/cart'

const localePath = useLocalePath()
const { t: $t, locale } = useI18n()

const cart = useCartStore()

/** Maps the current locale to the display currency. */
const checkoutCurrency = computed(() => {
  switch (locale.value) {
    case 'hy': return 'AMD'
    case 'ru': return 'RUB'
    default: return 'USD'
  }
})

/** Currency symbols keyed by code. */
const currencySymbols: Record<string, string> = {
  AMD: '֏', USD: '$', EUR: '€', RUB: '₽', GBP: '£',
}
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
  const currency = checkoutCurrency.value
  const symbol = currencySymbols[currency] ?? '$'
  let sum = 0
  for (const item of items) {
    if (item.priceAmd !== undefined) {
      sum += convertFromAmd(item.priceAmd, currency)
    } else {
      sum += parseFloat(item.rawPrice || '0')
    }
  }
  return `${symbol}${sum.toFixed(2)}`
})

// ── Submit ─────────────────────────────────────────────────────────────────

const submitting = ref(false)
const orderError = ref('')
const stripeCardFormRef = ref<{ confirmPayment: (clientSecret: string) => Promise<{ id: string }> } | null>(null)

/** Submit-button label while the order is being placed/processed. */
const submittingLabel = computed(() =>
  selectedMethod.value !== PAYMENT_MODULE_STRIPE && selectedMethod.value !== PAYMENT_MODULE_BANK_TRANSFER && selectedMethod.value
    ? $t('checkout.redirectingToBank')
    : $t('checkout.processing'))

async function submitOrder() {
  if (!selectedMethod.value || submitting.value) return
  orderError.value = ''

  const body: Record<string, unknown> = {
    items: cart.items.map(i => ({
      pid: i.pid,
      billingcycle: i.billingcycle,
      domain: i.domain || undefined,
      hostname: i.hostname || undefined,
      domainAction: i.domainAction || undefined,
      eppCode: i.eppCode || undefined,
      years: i.years || undefined,
    })),
    paymentmethod: selectedMethod.value,
  }

  if (!isLoggedIn.value) {
    if (!form.firstname || !form.lastname || !form.email || !form.password || !form.phonenumber) {
      orderError.value = $t('checkout.errors.fillRequired')
      return
    }
    Object.assign(body, { ...form })
  }

  submitting.value = true
  try {
    // Step 1: Place the order (Order + Invoice created on backend)
    const result = await apiFetch<{ orderId: number; invoiceId: number }>(
      '/api/portal/order/create',
      { method: 'POST', body }
    )

    // Auto-login guest after account creation
    if (!isLoggedIn.value && form.email && form.password) {
      try { await login(form.email, form.password) } catch { /* middleware handles */ }
    }

    if (selectedMethod.value === PAYMENT_MODULE_STRIPE) {
      // Step 2: Create PaymentIntent
      const { clientSecret } = await apiFetch<{ clientSecret: string }>(
        `/api/portal/order/${result.orderId}/create-payment-intent`,
        { method: 'POST' }
      )

      // Step 3: Confirm card payment via Stripe.js
      if (!stripeCardFormRef.value) throw new Error('Card form not ready')
      const paymentIntent = await stripeCardFormRef.value.confirmPayment(clientSecret)

      // Step 4: Tell backend payment succeeded — auto-accepts order, creates services
      await apiFetch(`/api/portal/order/${result.orderId}/confirm-payment`, {
        method: 'POST',
        body: { paymentIntentId: paymentIntent.id },
      })

      const domainItem = cart.items.find(i => i.itemType === 'domain')
      const domainQuery = domainItem
        ? `&domain=${domainItem.domain}&action=${domainItem.domainAction}`
        : ''
      const finalAmount = totalLabel.value
      cart.clear()
      await navigateTo(localePath(`/client/order-success?order=ORD-${String(result.orderId).padStart(4, '0')}&amount=${finalAmount}${domainQuery}`))
    } else if (selectedMethod.value !== PAYMENT_MODULE_BANK_TRANSFER) {
      // Any plugin-backed payment method (e.g. Inecobank) is a hosted gateway: register the
      // payment and hand the browser to the bank. Branching on "not stripe, not bank_transfer"
      // rather than a specific plugin id, so a second gateway plugin doesn't silently fall
      // through to the bank-transfer branch below. The cart is cleared first — the order and
      // invoice already exist on the backend, and the payer returns via /payment/result which
      // verifies with the bank.
      const returnUrl = new URL(
        localePath(`/payment/result?order=${result.orderId}`),
        window.location.origin,
      ).toString()
      const { redirectUrl } = await apiFetch<{ redirectUrl: string }>(
        `/api/portal/order/${result.orderId}/gateway-payment/start`,
        { method: 'POST', body: { module: selectedMethod.value, returnUrl } },
      )
      cart.clear()
      window.location.href = redirectUrl
    } else {
      // Bank transfer / other methods: order stays Pending, redirect to invoice
      cart.clear()
      if (result.invoiceId) {
        await navigateTo(localePath(`/client/invoices/${result.invoiceId}/pay`))
      } else {
        await navigateTo(localePath('/client/invoices'))
      }
    }
  } catch (err: unknown) {
    const stripeErr = err as { message?: string }
    const fetchErr = err as { data?: { statusMessage?: string }; message?: string }
    const msg = fetchErr?.data?.statusMessage
      ?? stripeErr?.message
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
