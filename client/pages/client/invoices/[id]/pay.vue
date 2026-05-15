<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-500 mb-8">
      <NuxtLink :to="localePath('/client/invoices')" class="hover:text-white transition-colors">
        {{ $t('client.invoices.title') }}
      </NuxtLink>
      <span>/</span>
      <NuxtLink :to="localePath(`/client/invoices/${invoiceId}`)" class="hover:text-white transition-colors">
        {{ $t('invoicePay.invoiceNum', { id: invoiceId }) }}
      </NuxtLink>
      <span>/</span>
      <span class="text-white">{{ $t('invoicePay.makePayment') }}</span>
    </nav>

    <!-- Loading -->
    <div v-if="pending" class="grid md:grid-cols-[1fr_380px] gap-8">
      <div class="space-y-4">
        <div v-for="i in 4" :key="i" class="h-14 rounded-xl bg-white/5 animate-pulse" />
      </div>
      <div class="h-80 rounded-2xl bg-white/5 animate-pulse" />
    </div>

    <!-- Error -->
    <div v-else-if="!invoice" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="1.5" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-400">{{ $t('client.invoices.notFound') }}</p>
    </div>

    <!-- Already paid -->
    <div v-else-if="invoice.status === 'Paid'" class="text-center py-20">
      <CheckCircle :size="48" :stroke-width="1.5" class="text-green-400 mx-auto mb-4" />
      <h2 class="text-xl font-bold text-white mb-2">{{ $t('invoicePay.alreadyPaid') }}</h2>
      <NuxtLink :to="localePath(`/client/invoices/${invoiceId}`)" class="text-cyan-400 hover:underline text-sm">
        {{ $t('invoicePay.viewInvoice') }}
      </NuxtLink>
    </div>

    <!-- Payment layout -->
    <div v-else class="grid md:grid-cols-[1fr_380px] gap-8 items-start">

      <!-- LEFT: Make Payment -->
      <div class="rounded-2xl border border-white/10 bg-white/[0.02] overflow-hidden">
        <!-- Header -->
        <div class="px-6 py-5 border-b border-white/10">
          <h1 class="text-xl font-bold text-white">{{ $t('invoicePay.makePayment') }}</h1>
        </div>

        <div class="p-6 space-y-6">
          <!-- Success message -->
          <div v-if="paySuccess" class="flex items-start gap-3 p-4 rounded-xl bg-green-500/10 border border-green-500/20 text-green-400">
            <CheckCircle :size="20" :stroke-width="2" class="flex-shrink-0 mt-0.5" />
            <div>
              <div class="font-semibold">{{ $t('invoicePay.paymentSuccess') }}</div>
              <NuxtLink :to="localePath(`/client/invoices/${invoiceId}`)" class="text-sm underline mt-1 block">
                {{ $t('invoicePay.viewInvoice') }}
              </NuxtLink>
            </div>
          </div>

          <!-- Error message -->
          <div v-if="payError" class="flex items-start gap-3 p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400">
            <AlertCircle :size="20" :stroke-width="2" class="flex-shrink-0 mt-0.5" />
            <span class="text-sm">{{ payError }}</span>
          </div>

          <template v-if="!paySuccess">
            <!-- Payment Method label -->
            <div class="grid grid-cols-[180px_1fr] gap-4 items-start">
              <label class="text-sm text-gray-400 pt-1">{{ $t('invoicePay.paymentMethod') }}</label>
              <div>
                <!-- Saved cards -->
                <div v-if="savedMethods.length > 0" class="space-y-2 mb-4">
                  <label
                    v-for="method in savedMethods"
                    :key="method.id"
                    class="flex items-center gap-3 p-3 rounded-xl border cursor-pointer transition-colors"
                    :class="selectedMethodId === method.id
                      ? 'border-cyan-500/50 bg-cyan-500/5'
                      : 'border-white/10 hover:border-white/20'"
                  >
                    <div
                      class="w-5 h-5 rounded-full border-2 flex items-center justify-center flex-shrink-0"
                      :class="selectedMethodId === method.id ? 'border-cyan-400' : 'border-gray-600'"
                    >
                      <div v-if="selectedMethodId === method.id" class="w-2.5 h-2.5 rounded-full bg-cyan-400" />
                    </div>
                    <CreditCard :size="16" :stroke-width="2" class="text-gray-400" />
                    <span class="text-sm text-white">
                      {{ method.card_type }} •••• {{ method.card_last_four }}
                      <span class="text-gray-500 ml-2">{{ method.card_expiry }}</span>
                    </span>
                    <input v-model="selectedMethodId" type="radio" :value="method.id" class="sr-only" />
                  </label>
                  <!-- New card option -->
                  <label
                    class="flex items-center gap-3 p-3 rounded-xl border cursor-pointer transition-colors"
                    :class="selectedMethodId === 'new'
                      ? 'border-cyan-500/50 bg-cyan-500/5'
                      : 'border-white/10 hover:border-white/20'"
                  >
                    <div
                      class="w-5 h-5 rounded-full border-2 flex items-center justify-center flex-shrink-0"
                      :class="selectedMethodId === 'new' ? 'border-cyan-400' : 'border-gray-600'"
                    >
                      <div v-if="selectedMethodId === 'new'" class="w-2.5 h-2.5 rounded-full bg-cyan-400" />
                    </div>
                    <Plus :size="16" :stroke-width="2" class="text-gray-400" />
                    <span class="text-sm text-white">{{ $t('invoicePay.newCard') }}</span>
                    <input v-model="selectedMethodId" type="radio" value="new" class="sr-only" />
                  </label>
                </div>

                <!-- No saved methods -->
                <div v-else class="flex items-center gap-2 p-3 rounded-xl border border-cyan-500/30 bg-cyan-500/5">
                  <div class="w-5 h-5 rounded-full border-2 border-cyan-400 flex items-center justify-center">
                    <div class="w-2.5 h-2.5 rounded-full bg-cyan-400" />
                  </div>
                  <span class="text-sm text-white">{{ $t('invoicePay.enterCardBelow') }}</span>
                </div>
              </div>
            </div>

            <!-- New card form -->
            <template v-if="selectedMethodId === 'new' || savedMethods.length === 0">
              <!-- Card Number -->
              <div class="grid grid-cols-[180px_1fr] gap-4 items-center">
                <label class="text-sm text-gray-400">{{ $t('invoicePay.cardNumber') }}</label>
                <input
                  v-model="cardNumber"
                  type="text"
                  inputmode="numeric"
                  maxlength="19"
                  placeholder="1234 1234 1234 1234"
                  class="w-full px-4 py-2.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-600 focus:outline-none focus:border-cyan-500/50 text-sm"
                  @input="formatCardNumber"
                />
              </div>

              <!-- Expiry -->
              <div class="grid grid-cols-[180px_1fr] gap-4 items-center">
                <label class="text-sm text-gray-400">{{ $t('invoicePay.expiryDate') }}</label>
                <input
                  v-model="expiryDate"
                  type="text"
                  inputmode="numeric"
                  maxlength="5"
                  placeholder="MM / YY"
                  class="w-40 px-4 py-2.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-600 focus:outline-none focus:border-cyan-500/50 text-sm"
                  @input="formatExpiry"
                />
              </div>

              <!-- CVV -->
              <div class="grid grid-cols-[180px_1fr] gap-4 items-center">
                <label class="text-sm text-gray-400">{{ $t('invoicePay.cvv') }}</label>
                <div class="flex items-center gap-4">
                  <input
                    v-model="cvv"
                    type="text"
                    inputmode="numeric"
                    maxlength="4"
                    placeholder="CVC"
                    class="w-24 px-4 py-2.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-600 focus:outline-none focus:border-cyan-500/50 text-sm"
                  />
                  <button class="text-cyan-400 text-sm hover:underline" @click.prevent>
                    {{ $t('invoicePay.whereIsFindThis') }}
                  </button>
                </div>
              </div>

              <!-- Card Description -->
              <div class="grid grid-cols-[180px_1fr] gap-4 items-center">
                <label class="text-sm text-gray-400">{{ $t('invoicePay.cardDescription') }}</label>
                <input
                  v-model="cardDescription"
                  type="text"
                  :placeholder="$t('invoicePay.cardDescriptionPlaceholder')"
                  class="w-full px-4 py-2.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder-gray-600 focus:outline-none focus:border-cyan-500/50 text-sm"
                />
              </div>
            </template>

            <!-- Submit -->
            <div class="grid grid-cols-[180px_1fr] gap-4 items-center">
              <div />
              <button
                class="px-8 py-3 rounded-xl font-bold text-white transition-all duration-200 hover:scale-[1.02] flex items-center gap-2 bg-gradient-to-r from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
                :disabled="submitting"
                @click="submitPayment"
              >
                <Loader2 v-if="submitting" :size="18" :stroke-width="2" class="animate-spin" />
                <CreditCard v-else :size="18" :stroke-width="2" />
                {{ submitting ? $t('invoicePay.processing') : $t('invoicePay.submitPayment') }}
              </button>
            </div>
          </template>
        </div>
      </div>

      <!-- RIGHT: Invoice Summary -->
      <div class="rounded-2xl border border-white/10 bg-white/[0.02] overflow-hidden">
        <div class="px-6 py-5 border-b border-white/10 text-center">
          <h2 class="text-xl font-bold text-white">{{ $t('invoicePay.invoiceNum', { id: invoiceId }) }}</h2>
        </div>

        <div class="p-6">
          <!-- Line items table -->
          <div class="mb-4">
            <div class="grid grid-cols-[1fr_auto] gap-4 pb-2 border-b border-white/10 mb-3">
              <span class="text-xs font-semibold text-gray-400 uppercase tracking-widest">{{ $t('client.invoices.colDescription') }}</span>
              <span class="text-xs font-semibold text-gray-400 uppercase tracking-widest text-right">{{ $t('client.invoices.colAmount') }}</span>
            </div>
            <div
              v-for="item in invoice.items?.item ?? []"
              :key="item.id"
              class="grid grid-cols-[1fr_auto] gap-4 py-2 border-b border-white/5"
            >
              <span class="text-sm text-gray-300">{{ item.description }}</span>
              <span class="text-sm text-white font-medium text-right">${{ item.amount }} USD</span>
            </div>
          </div>

          <!-- Totals -->
          <div class="space-y-2 pt-2">
            <div class="grid grid-cols-[1fr_auto] gap-4">
              <span class="text-sm font-semibold text-gray-400">{{ $t('client.invoices.subtotal') }}</span>
              <span class="text-sm font-semibold text-white">${{ invoice.subtotal }} USD</span>
            </div>
            <div class="grid grid-cols-[1fr_auto] gap-4">
              <span class="text-sm text-gray-500">{{ $t('client.invoices.creditApplied') }}</span>
              <span class="text-sm text-gray-400">${{ invoice.credit }} USD</span>
            </div>
            <div class="grid grid-cols-[1fr_auto] gap-4 pt-2 border-t border-white/10">
              <span class="text-sm font-bold text-white">{{ $t('client.invoices.total') }}</span>
              <span class="text-sm font-bold text-white">${{ invoice.total }} USD</span>
            </div>
          </div>

          <!-- Payments to date / Balance -->
          <div class="mt-6 space-y-2">
            <div class="flex justify-between text-sm">
              <span class="text-gray-400">{{ $t('invoicePay.paymentsToDate') }}</span>
              <span class="text-white">${{ paymentsToDate }} USD</span>
            </div>
          </div>

          <!-- Balance Due -->
          <div class="mt-4 p-4 rounded-xl bg-green-500/10 border border-green-500/20 flex justify-between items-center">
            <span class="font-bold text-white">{{ $t('invoicePay.balanceDue') }}</span>
            <span class="font-bold text-green-400 text-lg">${{ invoice.balance ?? invoice.total }} USD</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Security notice -->
    <div class="mt-6 flex items-center gap-2 px-4 py-3 rounded-xl bg-yellow-500/5 border border-yellow-500/15 text-yellow-400/80 text-xs">
      <Lock :size="14" :stroke-width="2" class="flex-shrink-0" />
      {{ $t('invoicePay.securityNotice') }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { AlertCircle, CheckCircle, CreditCard, Plus, Lock, Loader2 } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()
const config = useRuntimeConfig()
const whmcsUrl = config.public.whmcsUrl

const invoiceId = route.params.id as string

// Fetch invoice + saved payment methods in parallel
const [{ data: invoiceData, pending }, { data: methodsData }] = await Promise.all([
  useApi(`/api/portal/client/invoices/${invoiceId}`),
  useApi('/api/portal/client/payment-methods', { default: () => [] })
])

const invoice = computed(() => invoiceData.value as any)
const savedMethods = computed(() => (methodsData.value as any[]) ?? [])

// Payments to date = total - balance
const paymentsToDate = computed(() => {
  if (!invoice.value) return '0.00'
  const total = parseFloat(invoice.value.total ?? '0')
  const balance = parseFloat(invoice.value.balance ?? invoice.value.total ?? '0')
  return (total - balance).toFixed(2)
})

// Payment form state
const selectedMethodId = ref<number | 'new'>(savedMethods.value[0]?.id ?? 'new')
const cardNumber = ref('')
const expiryDate = ref('')
const cvv = ref('')
const cardDescription = ref('')
const submitting = ref(false)
const paySuccess = ref(false)
const payError = ref('')

function formatCardNumber(e: Event) {
  const input = e.target as HTMLInputElement
  let v = input.value.replace(/\D/g, '').slice(0, 16)
  cardNumber.value = v.replace(/(.{4})/g, '$1 ').trim()
}

function formatExpiry(e: Event) {
  const input = e.target as HTMLInputElement
  let v = input.value.replace(/\D/g, '').slice(0, 4)
  if (v.length >= 3) expiryDate.value = v.slice(0, 2) + ' / ' + v.slice(2)
  else expiryDate.value = v
}

async function submitPayment() {
  payError.value = ''

  // If new card entry → redirect to WHMCS for PCI-compliant card processing
  if (selectedMethodId.value === 'new') {
    window.location.href = `${whmcsUrl}/index.php?rp=/invoice/${invoiceId}/pay`
    return
  }

  submitting.value = true
  try {
    await apiFetch(`/api/portal/client/invoices/${invoiceId}/pay`, {
      method: 'POST',
      body: { paymethodid: selectedMethodId.value }
    })
    paySuccess.value = true
  } catch (err: any) {
    payError.value = err?.data?.statusMessage ?? t('invoicePay.paymentFailed')
  } finally {
    submitting.value = false
  }
}
</script>
