<template>
  <div class="min-h-dvh bg-[#0a0a0f] py-10 md:py-16">
    <div class="container-custom max-w-6xl mx-auto">

      <!-- Back link -->
      <NuxtLink
        :to="localePath('/products')"
        class="inline-flex items-center gap-2 text-gray-400 hover:text-white text-sm mb-8 transition-colors"
      >
        <ArrowLeft :size="16" :stroke-width="2" />
        {{ $t('configure.back') }}
      </NuxtLink>

      <!-- Loading -->
      <div v-if="pending" class="grid md:grid-cols-[1fr_360px] gap-8">
        <div class="space-y-4">
          <div class="h-8 w-64 bg-white/5 rounded animate-pulse" />
          <div class="h-4 w-full bg-white/5 rounded animate-pulse" />
          <div class="h-4 w-3/4 bg-white/5 rounded animate-pulse" />
          <div class="h-48 bg-white/5 rounded-2xl animate-pulse mt-6" />
        </div>
        <div class="h-80 bg-white/5 rounded-2xl animate-pulse" />
      </div>

      <!-- Error -->
      <div v-else-if="!product" class="text-center py-24">
        <AlertCircle :size="48" :stroke-width="1.5" class="text-red-400 mx-auto mb-4" />
        <p class="text-gray-400">{{ $t('configure.notFound') }}</p>
        <NuxtLink :to="localePath('/products')" class="mt-6 inline-block text-cyan-400 hover:underline">
          {{ $t('configure.back') }}
        </NuxtLink>
      </div>

      <!-- Configure layout -->
      <div v-else class="grid md:grid-cols-[1fr_360px] gap-8 items-start">

        <!-- LEFT: Product details -->
        <div class="space-y-8">
          <!-- Product header -->
          <div>
            <div class="flex items-center gap-4 mb-4">
              <div
                v-if="cfg"
                class="w-14 h-14 rounded-xl flex items-center justify-center border-2 flex-shrink-0"
                :style="{ backgroundColor: cfg.color + '15', borderColor: cfg.color + '40' }"
              >
                <Icon :name="`mdi:${cfg.icon}`" class="text-3xl" :style="{ color: cfg.color }" />
              </div>
              <div>
                <div class="text-xs text-gray-500 uppercase tracking-widest mb-1">{{ groupName }}</div>
                <h1 class="text-2xl md:text-3xl font-bold text-white">{{ productName }}</h1>
              </div>
            </div>
            <p v-if="productDesc" class="text-gray-400 leading-relaxed">
              <strong class="text-white">{{ productName }}</strong> {{ productDesc }}
            </p>
          </div>

          <!-- Features -->
          <div v-if="features.length > 0" class="p-6 rounded-2xl bg-white/5 border border-white/10">
            <h2 class="text-sm font-bold text-white mb-4 uppercase tracking-widest">
              {{ $t('configure.includedFeatures') }}
            </h2>
            <ul class="space-y-2">
              <li
                v-for="feat in features"
                :key="feat"
                class="flex items-center gap-2 text-sm text-gray-300"
              >
                <CheckCircle :size="15" :stroke-width="2" class="text-cyan-400 flex-shrink-0" />
                {{ feat }}
              </li>
            </ul>
          </div>

          <!-- Billing Cycle selector -->
          <div>
            <h2 class="text-sm font-bold text-white mb-3 uppercase tracking-widest">
              {{ $t('configure.chooseBillingCycle') }}
            </h2>
            <div class="space-y-2">
              <label
                v-for="cycle in availableCycles"
                :key="cycle.key"
                class="flex items-center justify-between p-4 rounded-xl border cursor-pointer transition-all duration-200"
                :class="selectedCycle === cycle.key
                  ? 'border-cyan-500/60 bg-cyan-500/5'
                  : 'border-white/10 bg-white/[0.02] hover:border-white/20'"
              >
                <div class="flex items-center gap-3">
                  <div
                    class="w-4 h-4 rounded-full border-2 flex items-center justify-center flex-shrink-0 transition-colors"
                    :class="selectedCycle === cycle.key ? 'border-cyan-400' : 'border-gray-600'"
                  >
                    <div v-if="selectedCycle === cycle.key" class="w-2 h-2 rounded-full bg-cyan-400" />
                  </div>
                  <span class="text-white font-medium">{{ $t(`hosting.cycles.${cycleI18nKey[cycle.key]}`) }}</span>
                </div>
                <span class="font-bold text-white">{{ cycle.price }}</span>
                <input v-model="selectedCycle" type="radio" :value="cycle.key" class="sr-only" />
              </label>
            </div>
          </div>
        </div>

        <!-- RIGHT: Order Summary -->
        <div class="sticky top-24">
          <div class="rounded-2xl border border-white/10 overflow-hidden">
            <!-- Header -->
            <div class="px-6 py-4 bg-white/5 border-b border-white/10">
              <h2 class="font-bold text-white text-base">{{ $t('configure.orderSummary') }}</h2>
            </div>

            <!-- Body -->
            <div class="p-6 space-y-4">
              <!-- Product info -->
              <div>
                <div class="font-semibold text-white">{{ productName }}</div>
                <div class="text-sm text-gray-500 italic">{{ groupName }}</div>
              </div>

              <!-- Line items -->
              <div class="space-y-2 pt-2 border-t border-white/10">
                <div class="flex justify-between text-sm">
                  <span class="text-gray-400">{{ productName }}</span>
                  <span class="text-white">{{ selectedPrice }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-400">{{ $t('configure.setupFees') }}</span>
                  <span class="text-gray-400">{{ setupFee }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-400">{{ $t(`hosting.cycles.${cycleI18nKey[selectedCycle]}`) }}</span>
                  <span class="text-white">{{ selectedPrice }}</span>
                </div>
              </div>

              <!-- Total -->
              <div class="pt-3 border-t border-white/10">
                <div class="flex justify-between">
                  <span class="text-2xl font-bold text-white">{{ selectedPrice }}</span>
                </div>
                <div class="text-xs text-gray-500 text-right mt-0.5">{{ $t('configure.totalDueToday') }}</div>
              </div>

              <!-- Continue button -->
              <button
                class="w-full py-4 rounded-xl font-bold text-white transition-all duration-300 hover:scale-[1.02] shadow-lg flex items-center justify-center gap-2 bg-gradient-to-r from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500"
                @click="addToCart"
              >
                {{ $t('configure.continue') }}
                <ArrowRight :size="18" :stroke-width="2.5" />
              </button>
            </div>
          </div>

          <!-- Support -->
          <p class="mt-4 text-center text-xs text-gray-500 flex items-center justify-center gap-1.5">
            <HelpCircle :size="13" :stroke-width="2" />
            {{ $t('configure.haveQuestions') }}
            <NuxtLink :to="localePath('/contact')" class="text-cyan-400 hover:underline">
              {{ $t('configure.clickHere') }}
            </NuxtLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, ArrowRight, CheckCircle, AlertCircle, HelpCircle } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { formatMoney } from '~/utils/formatMoney'
import type { PortalProduct } from '~/types/portalproduct'

const route = useRoute()
const { t } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()
const currencyStore = useCurrencyStore()
onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

const pid = Number(route.params.pid)

// Fetch product by pid
// Straight from the API composable rather than through a store: this page fetches once and
// owns the result alone, which is the named exception to component -> store -> api. A store
// would also cost the SSR dedup and the locale re-fetch that `useApi()` gives for free, and
// this page is server-rendered and indexed.
// `currency` is passed the same way `usePortalPlans.ts` passes it — the payer's currency, so
// the getter re-reads and the request re-fetches once `payerCode` resolves or changes.
const { data: products, pending } = await useCatalogApi().loadProducts(
  () => ({ pid, currency: currencyStore.payerCode ?? undefined })
)

const product = computed<PortalProduct | null>(() => products.value?.[0] ?? null)

// Find config by product group.
//
// `groupId` is the field `ProductDto` sends; this read `product.value.gid`, which no product
// has ever carried, so the lookup was `productGidToKey[undefined]` and `cfg` was null on every
// render — taking the group name and every visual with it.
const cfg = computed(() => {
  const groupId = product.value?.groupId
  if (groupId === undefined) return null

  const key = productGidToKey[groupId]
  return key ? productConfig[key] : null
})

// The `translated_*` and `group_translations` reads that used to head each of these are gone:
// `ProductDto` carries no translated copy and never has, so they resolved to `undefined` on
// every render and the `||` fallback below is what has actually been rendering all along. They
// are removed rather than left in place so the next reader does not take them for a working
// translation path -- see the note on `types/portalproduct.ts`.
const groupName = computed(() => cfg.value?.name ?? '')

const productName = computed(() => product.value?.name ?? '')

/**
 * Summary paragraph and feature lines parsed out of the product description.
 *
 * The catalogue copy is HTML (`✔ 600 MB Disk Space <br />`), so rendering the raw
 * `description` printed the markup and every bullet inline above the feature list.
 * `parseDescription` (utils/whmcs.ts, auto-imported) unwraps the tags and splits the
 * leading prose off as `summary`; bullet-only copy yields an empty summary, and the
 * paragraph is then hidden rather than duplicating the list.
 */
const parsedDesc = computed(() => parseDescription(product.value?.description ?? ''))

/** Leading prose of the description, tags stripped; empty for bullet-only copy. */
const productDesc = computed(() => parsedDesc.value.summary)

/** Feature bullets, parsed out of the product description — the only source the API offers. */
const features = computed(() => parsedDesc.value.features)

// Pricing — the backend prices only monthly and annually; see the note on
// `types/portalproduct.ts` for why there is no longer a quarterly/semiannual/biennial/triennial
// key to look up.
const allCycleKeys = ['monthly', 'annual'] as const
type CycleKey = typeof allCycleKeys[number]

/** Maps the API's cycle key to the `hosting.cycles.*` i18n key — they are spelled differently. */
const cycleI18nKey: Record<CycleKey, string> = { monthly: 'monthly', annual: 'annually' }

/** The payer's currency to format and price with — never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

/**
 * Raw numeric price for one cycle, in the caller's own currency, as the API sent it.
 *
 * @param key - The billing cycle to read.
 * @returns The amount, or null when the product carries no price for that cycle.
 */
const cycleAmount = (key: CycleKey): number | null => product.value?.pricing?.[key] ?? null

const availableCycles = computed(() => allCycleKeys
  .filter(k => cycleAmount(k) !== null)
  .map(k => ({ key: k, price: formatMoney(cycleAmount(k), payerCurrency.value) })))

const selectedCycle = ref<CycleKey>('monthly')

// Set default cycle to first available
watch(availableCycles, (cycles) => {
  const first = cycles[0]
  if (first && !cycles.find(c => c.key === selectedCycle.value)) {
    selectedCycle.value = first.key
  }
}, { immediate: true })

const selectedAmount = computed(() => cycleAmount(selectedCycle.value))

const selectedPrice = computed(() => {
  if (selectedAmount.value === null) return ''
  return formatMoney(selectedAmount.value, payerCurrency.value)
})

/**
 * The setup fee for the selected cycle.
 *
 * Always zero — `ProductPricingDto` carries no setup fee, and there is no per-cycle
 * setup-fee field anywhere in the C# API for one to come from.
 */
const setupFee = computed(() => formatMoney(0, payerCurrency.value))

/** Adds the configured plan to the cart and moves the visitor to checkout. */
const addToCart = () => {
  const amount = selectedAmount.value
  if (amount === null || !currencyStore.payerCode) return
  cart.addItem({
    pid,
    name: productName.value,
    billingcycle: selectedCycle.value,
    cycleLabel: t(`hosting.cycles.${cycleI18nKey[selectedCycle.value]}`),
    amount,
    currency: currencyStore.payerCode
  })
  navigateTo(localePath('/checkout'))
}

useSeo({
  title: `${t('configure.title')} — ${productName.value}`,
  description: productDesc.value,
  path: `/configure/${pid}`
})
</script>
