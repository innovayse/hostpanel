<template>
  <div class="min-h-screen bg-[#0a0a0f] py-10 md:py-16">
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
                  <span class="text-white font-medium">{{ $t(`hosting.cycles.${cycle.key}`) }}</span>
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
                  <span class="text-gray-400">{{ $t(`hosting.cycles.${selectedCycle}`) }}</span>
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

const route = useRoute()
const { t, locale } = useI18n()
const localePath = useLocalePath()
const cart = useCartStore()
onMounted(() => cart.init())

const pid = Number(route.params.pid)

// Fetch product by pid
const { data: products, pending } = await useApi('/api/portal/public/products', {
  query: computed(() => ({ lang: locale.value, pid })),
  default: () => []
})

const product = computed(() => (products.value as any[])?.[0] ?? null)

// Find config by gid
const cfg = computed(() => {
  if (!product.value) return null
  const key = productGidToKey[product.value.gid]
  return key ? productConfig[key] : null
})

const groupName = computed(() =>
  product.value?.group_translations?.translated_name ||
  product.value?.group_translations?.translated_headline ||
  cfg.value?.name || ''
)

const productName = computed(() =>
  product.value?.translated_name || product.value?.name || ''
)

const productDesc = computed(() =>
  product.value?.translated_shortdescription ||
  product.value?.shortdescription ||
  product.value?.translated_description ||
  product.value?.description || ''
)

// Features: group_features first, then parse description
const features = computed(() => {
  if (!product.value) return []
  const gf: any[] = product.value.group_features ?? []
  const groupF = gf.map((f: any) => f.translated_feature || f.feature).filter(Boolean)
  if (groupF.length > 0) return groupF
  const { features: f } = parseDescription(productDesc.value)
  return f
})

// Pricing
const allCycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const
type CycleKey = typeof allCycleKeys[number]

function getCurrency() {
  const pricing = product.value?.pricing
  if (!pricing) return null
  const preferred = currencyByLocale[locale.value] ?? 'USD'
  return pricing[preferred] ?? Object.values(pricing)[0] as any
}

const availableCycles = computed(() => {
  const c = getCurrency()
  if (!c) return []
  return allCycleKeys
    .filter(k => c[k] && c[k] !== '-1.00' && c[k] !== '0.00')
    .map(k => ({ key: k, price: `${c.prefix}${c[k]}` }))
})

const selectedCycle = ref<CycleKey>('monthly')

// Set default cycle to first available
watch(availableCycles, (cycles) => {
  if (cycles.length && !cycles.find(c => c.key === selectedCycle.value)) {
    selectedCycle.value = cycles[0].key
  }
}, { immediate: true })

const selectedPrice = computed(() => {
  const c = getCurrency()
  if (!c) return ''
  const amount = c[selectedCycle.value]
  if (!amount || amount === '-1.00' || amount === '0.00') return ''
  return `${c.prefix}${amount}`
})

const setupFee = computed(() => {
  const c = getCurrency()
  const cycleSetupMap: Record<CycleKey, string> = {
    monthly: 'msetupfee', quarterly: 'qsetupfee', semiannually: 'ssetupfee',
    annually: 'asetupfee', biennially: 'bsetupfee', triennially: 'tsetupfee'
  }
  const key = cycleSetupMap[selectedCycle.value]
  const fee = c?.[key] ?? '0.00'
  return `${c?.prefix ?? ''}${fee}`
})

function addToCart() {
  const c = getCurrency()
  cart.addItem({
    pid,
    name: productName.value,
    billingcycle: selectedCycle.value,
    cycleLabel: t(`hosting.cycles.${selectedCycle.value}`),
    price: selectedPrice.value,
    prefix: c?.prefix ?? '',
    rawPrice: c?.[selectedCycle.value] ?? '0'
  })
  navigateTo(localePath('/checkout'))
}

useSeo({
  title: `${t('configure.title')} — ${productName.value}`,
  description: productDesc.value,
  path: `/configure/${pid}`
})
</script>
