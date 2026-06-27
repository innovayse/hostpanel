<template>
  <div class="overflow-x-hidden">
    <!-- Hero / Search -->
    <section class="relative py-12 md:py-24 lg:py-32 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/3 w-[400px] h-[400px] bg-secondary-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </div>

      <div class="container-custom relative z-10 text-center max-w-4xl mx-auto px-4">
        <!-- Breadcrumbs -->
        <UiBreadcrumbs 
          :items="[{ label: $t('domains.badge') }]" 
          container-class="mb-10 flex justify-center opacity-60 hover:opacity-100 transition-opacity" 
        />

        <div class="inline-flex items-center gap-2 px-4 py-2 mb-8 rounded-full glass border border-primary-500/20 backdrop-blur-sm">
          <Globe :size="14" :stroke-width="2" class="text-primary-400" />
          <span class="text-xs md:text-sm font-black text-gray-300 uppercase tracking-[0.2em]">{{ $t('domains.badge') }}</span>
        </div>

        <h1 class="text-3xl md:text-6xl font-black mb-6 text-white leading-tight uppercase tracking-tighter">
          {{ $t('domains.title') }}
          <span class="block pb-2 text-transparent bg-clip-text bg-gradient-to-r from-primary-400 to-cyan-400">{{ $t('domains.titleHighlight') }}</span>
        </h1>

        <!-- Register: search bar -->
        <div class="mt-12 max-w-2xl mx-auto">
          <!-- Tab switcher -->
          <div class="flex justify-center gap-3 mb-8">
            <UiButton variant="primary" size="sm" class="!rounded-xl shadow-lg shadow-primary-500/20">
              <Globe :size="14" :stroke-width="2" class="mr-2" />
              {{ $t('domains.tabRegister') }}
            </UiButton>
            <UiButton variant="subtle" size="sm" :to="localePath('/domains/transfer')" class="!rounded-xl hover:bg-white/10">
              <ArrowLeftRight :size="14" :stroke-width="2" class="mr-2" />
              {{ $t('domains.tabTransfer') }}
            </UiButton>
          </div>

          <UiSearchBar
            v-model="searchQuery"
            :placeholder="$t('domains.placeholder')"
            :loading="searching"
            :button-label="$t('domains.search')"
            @search="searchDomain"
          />

          <!-- Availability result -->
          <div v-if="result" class="mt-8 transition-all duration-300">
            <!-- TLD not supported -->
            <div
              v-if="!isTldSupported"
              class="p-6 rounded-[2rem] border-2 border-amber-500/40 bg-amber-500/5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-6 backdrop-blur-md"
            >
              <div class="flex items-center gap-4">
                <div class="w-12 h-12 rounded-2xl flex items-center justify-center border bg-amber-500/10 border-amber-500/30">
                  <AlertCircle :size="24" class="text-amber-400" />
                </div>
                <div class="text-left">
                  <div class="text-xl font-black text-white uppercase tracking-tight">{{ result.domain }}</div>
                  <div class="text-xs font-bold uppercase tracking-widest mt-1 text-amber-400">
                    {{ $t('domains.tldNotSupported') }}
                  </div>
                </div>
              </div>
            </div>

            <!-- TLD supported — normal result -->
            <div
              v-else
              class="p-6 rounded-[2rem] border-2 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-6 backdrop-blur-md"
              :class="result.available ? 'border-green-500/40 bg-green-500/5' : 'border-red-500/40 bg-red-500/5'"
            >
              <div class="flex items-center gap-4">
                <div
                  class="w-12 h-12 rounded-2xl flex items-center justify-center border transition-all"
                  :class="result.available ? 'bg-green-500/10 border-green-500/30' : 'bg-red-500/10 border-red-500/30'"
                >
                  <CheckCircle v-if="result.available" :size="24" class="text-green-400" />
                  <XCircle v-else :size="24" class="text-red-400" />
                </div>
                <div class="text-left">
                  <div class="text-xl font-black text-white uppercase tracking-tight">{{ result.domain }}</div>
                  <div class="text-xs font-bold uppercase tracking-widest mt-1" :class="result.available ? 'text-green-400' : 'text-red-400'">
                    {{ result.available ? $t('domains.available') : $t('domains.taken') }}
                  </div>
                </div>
              </div>
              <div class="flex items-center gap-3 w-full sm:w-auto">
                <button
                  v-if="result.available"
                  class="flex-1 sm:flex-none px-8 py-3.5 rounded-2xl bg-gradient-to-r from-green-500 to-emerald-600 text-white font-black text-xs uppercase tracking-widest hover:shadow-xl hover:shadow-green-500/20 transition-all scale-100 active:scale-95 text-center disabled:opacity-50 disabled:cursor-not-allowed"
                  :disabled="cart.hasDomainItem(result.domain, 'register')"
                  @click="addDomainToCart(result.domain, getRegisterPrice(result.domain), 1)"
                >
                  {{ cart.hasDomainItem(result.domain, 'register') ? $t('domains.alreadyInCart') : $t('domains.registerNow') }}
                </button>
                <UiButton
                  v-else
                  variant="outline"
                  full-width
                  class="!rounded-2xl !h-12 !border-white/10 hover:!border-primary-500/50"
                  :to="localePath('/domains/transfer') + (result?.domain ? `?domain=${result.domain}` : '')"
                >
                  <ArrowLeftRight :size="15" :stroke-width="2" class="mr-2" />
                  {{ $t('domains.takenTransfer') }}
                </UiButton>
              </div>
            </div>
          </div>

          <!-- Search error -->
          <div v-if="searchError" class="mt-6 p-5 rounded-2xl border-2 border-red-500/20 bg-red-500/5 text-red-400 text-sm font-medium flex items-center gap-3">
             <AlertCircle :size="18" />
             {{ searchError }}
          </div>
        </div>
      </div>
    </section>

    <!-- TLD Pricing Table -->
    <section class="py-8 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <div class="container-custom max-w-6xl mx-auto px-4 relative z-10">
        <div class="text-center mb-16">
          <span class="text-xs font-black text-primary-500 uppercase tracking-[0.3em] block mb-4 italic">{{ $t('domains.pricing.browseByCategory') }}</span>
          <h2 class="text-4xl md:text-5xl font-black text-white mb-4 uppercase tracking-tighter">{{ $t('domains.pricing.title') }}</h2>
          <div class="w-24 h-px bg-gradient-to-r from-transparent via-primary-500 to-transparent mx-auto mt-6" />
        </div>

        <div v-if="pricingPending" class="grid grid-cols-1 md:grid-cols-4 gap-6 py-20">
          <div v-for="i in 8" :key="i" class="h-32 rounded-3xl bg-white/5 border border-white/10 animate-pulse" />
        </div>

        <template v-else-if="tldPricing">
          <!-- Category filter -->
          <div v-if="allCategories.length" class="mb-12">
            <div class="flex flex-nowrap overflow-x-auto no-scrollbar gap-3 pb-6 -mx-4 px-4 scroll-smooth">
              <button
                class="px-6 py-2.5 rounded-2xl text-[10px] md:text-xs font-black uppercase tracking-widest transition-all border whitespace-nowrap"
                :class="activeCategory === null
                  ? 'bg-gradient-to-r from-primary-500 to-cyan-500 border-transparent text-white shadow-xl shadow-primary-500/20 scale-105 z-10'
                  : 'border-white/5 bg-white/[0.02] text-gray-500 hover:border-white/20 hover:text-white'"
                @click="activeCategory = null"
              >
                {{ $t('domains.pricing.catAll') }}
                <span class="ml-2 opacity-50 font-mono">[{{ totalTldCount }}]</span>
              </button>
              <button
                v-for="cat in allCategories"
                :key="cat.name"
                class="px-6 py-2.5 rounded-2xl text-[10px] md:text-xs font-black uppercase tracking-widest transition-all border whitespace-nowrap"
                :class="activeCategory === cat.name
                  ? 'bg-gradient-to-r from-primary-500 to-cyan-500 border-transparent text-white shadow-xl shadow-primary-500/20 scale-105 z-10'
                  : 'border-white/5 bg-white/[0.02] text-gray-500 hover:border-white/20 hover:text-white'"
                @click="activeCategory = cat.name"
              >
                {{ translateCategory(cat.name) }}
                <span class="ml-2 opacity-50 font-mono">[{{ cat.count }}]</span>
              </button>
            </div>
          </div>

          <!-- Desktop: UiTable -->
          <div class="hidden md:block rounded-[2.5rem] border border-white/5 bg-white/[0.01] overflow-hidden backdrop-blur-3xl shadow-2xl">
            <UiTable>
              <UiTableHead>
                <UiTableRow :hoverable="false" class="bg-white/5">
                  <UiTableTh class="!py-8 !px-10 font-black text-xs uppercase tracking-widest opacity-50">{{ $t('domains.pricing.extension') }}</UiTableTh>
                  <UiTableTh align="center" class="font-black text-xs uppercase tracking-widest opacity-50">{{ $t('domains.pricing.register') }}</UiTableTh>
                  <UiTableTh align="center" class="font-black text-xs uppercase tracking-widest opacity-50">{{ $t('domains.pricing.transfer') }}</UiTableTh>
                  <UiTableTh align="center" class="font-black text-xs uppercase tracking-widest opacity-50">{{ $t('domains.pricing.renew') }}</UiTableTh>
                  <UiTableTh class="!pr-10" />
                </UiTableRow>
              </UiTableHead>
              <UiTableBody class="divide-y divide-white/5">
                <UiTableRow v-for="(tld, ext) in displayedTlds" :key="ext" class="hover:bg-white/[0.03] transition-colors group">
                  <UiTableTd class="!py-8 !px-10">
                    <span class="text-2xl font-black text-white group-hover:text-primary-400 transition-colors tracking-tighter uppercase">.{{ ext }}</span>
                  </UiTableTd>
                  <UiTableTd align="center">
                    <span class="block text-xl font-black text-white tabular-nums tracking-tight">{{ formatPrice(tld.register['1'] ?? '') }}</span>
                    <span class="text-[10px] text-gray-500 font-bold uppercase tracking-widest">{{ $t('domains.pricing.perYear') }}</span>
                  </UiTableTd>
                  <UiTableTd align="center">
                    <span class="block text-xl font-black text-white tabular-nums tracking-tight">{{ formatPrice(tld.transfer['1'] ?? '') }}</span>
                    <span class="text-[10px] text-gray-500 font-bold uppercase tracking-widest">{{ $t('domains.pricing.perYear') }}</span>
                  </UiTableTd>
                  <UiTableTd align="center">
                    <span class="block text-xl font-black text-white tabular-nums tracking-tight">{{ formatPrice(tld.renew['1'] ?? '') }}</span>
                    <span class="text-[10px] text-gray-500 font-bold uppercase tracking-widest">{{ $t('domains.pricing.perYear') }}</span>
                  </UiTableTd>
                  <UiTableTd align="right" class="!pr-10">
                     <UiButton 
                       size="sm" 
                       variant="outline" 
                       class="!rounded-xl !border-white/10 group-hover:!border-primary-500/50 group-hover:!bg-primary-500 group-hover:!text-white transition-all uppercase text-[10px] font-black tracking-widest"
                       @click="quickSearch(ext)"
                     >
                       {{ $t('domains.search') }}
                     </UiButton>
                  </UiTableTd>
                </UiTableRow>
                <UiTableRow v-if="!Object.keys(displayedTlds).length" :hoverable="false">
                  <UiTableTd colspan="5" align="center" class="text-gray-500 py-20 font-medium italic">
                    {{ $t('domains.pricing.noResults') }}
                  </UiTableTd>
                </UiTableRow>
              </UiTableBody>
            </UiTable>
          </div>

          <!-- Mobile: card layout -->
          <div class="md:hidden grid grid-cols-1 gap-6">
            <div
              v-for="(tld, ext) in displayedTlds"
              :key="ext"
              class="rounded-3xl border border-white/10 bg-white/[0.01] p-6 hover:border-primary-500/30 transition-all group active:scale-[0.98]"
            >
              <div class="flex items-center justify-between mb-6">
                 <span class="text-2xl font-black text-white group-hover:text-primary-400 transition-colors tracking-tighter uppercase">.{{ ext }}</span>
                 <div class="p-2 rounded-xl bg-white/5 border border-white/5 opacity-40">
                    <Globe :size="16" />
                 </div>
              </div>
              
              <div class="grid grid-cols-3 gap-2 mb-8 p-4 rounded-2xl bg-white/[0.03] border border-white/5">
                <div class="text-center">
                  <span class="block text-xs font-black text-gray-500 uppercase tracking-widest mb-1 opacity-50">{{ $t('domains.pricing.register') }}</span>
                  <span class="block text-sm font-black text-white tabular-nums">{{ formatPrice(tld.register['1'] ?? '') }}</span>
                </div>
                <div class="text-center border-x border-white/5">
                  <span class="block text-xs font-black text-gray-500 uppercase tracking-widest mb-1 opacity-50">{{ $t('domains.pricing.transfer') }}</span>
                  <span class="block text-sm font-black text-white tabular-nums">{{ formatPrice(tld.transfer['1'] ?? '') }}</span>
                </div>
                <div class="text-center">
                  <span class="block text-xs font-black text-gray-500 uppercase tracking-widest mb-1 opacity-50">{{ $t('domains.pricing.renew') }}</span>
                  <span class="block text-sm font-black text-white tabular-nums">{{ formatPrice(tld.renew['1'] ?? '') }}</span>
                </div>
              </div>

              <button
                class="w-full h-12 flex items-center justify-center rounded-2xl bg-white/[0.05] border border-white/5 text-[10px] font-black uppercase tracking-widest text-gray-400 hover:bg-primary-500 hover:text-white hover:border-transparent transition-all"
                @click="quickSearch(ext as string)"
              >
                {{ $t('domains.pricing.registerNow') }}
              </button>
            </div>
            
            <div v-if="!Object.keys(displayedTlds).length" class="py-20 text-center text-gray-500 italic">
              {{ $t('domains.pricing.noResults') }}
            </div>
          </div>
        </template>

        <!-- Promo boxes -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-8 mt-24">
          <!-- Add Web Hosting -->
          <div class="rounded-[2.5rem] border border-white/5 bg-gradient-to-br from-white/[0.03] to-transparent p-10 flex flex-col md:flex-row items-center md:items-start justify-between gap-10 hover:border-primary-400/20 transition-all group">
            <div class="text-center md:text-left">
              <div class="w-16 h-16 rounded-3xl bg-primary-500/10 flex items-center justify-center border border-primary-500/20 mb-6 group-hover:scale-110 transition-transform">
                <Server :size="32" class="text-primary-400" />
              </div>
              <h3 class="text-2xl font-black text-white mb-2 uppercase tracking-tighter">{{ $t('domains.promoHostingTitle') }}</h3>
              <p class="text-sm font-bold text-primary-400 mb-4 uppercase tracking-widest">{{ $t('domains.promoHostingSubtitle') }}</p>
              <p class="text-base text-gray-500 mb-8 leading-relaxed italic">{{ $t('domains.promoHostingDesc') }}</p>
              <NuxtLink
                :to="localePath('/hosting')"
                class="inline-flex items-center gap-3 px-8 py-4 rounded-2xl bg-primary-500 hover:bg-primary-600 text-white text-xs font-black uppercase tracking-widest transition-all shadow-xl shadow-primary-500/20 hover:scale-105 active:scale-95"
              >
                {{ $t('domains.promoHostingBtn') }}
                <ArrowRight :size="16" />
              </NuxtLink>
            </div>
          </div>

          <!-- Transfer -->
          <div class="rounded-[2.5rem] border border-white/5 bg-gradient-to-bl from-white/[0.03] to-transparent p-10 flex flex-col md:flex-row items-center md:items-start justify-between gap-10 hover:border-cyan-400/20 transition-all group">
            <div class="text-center md:text-left flex flex-col h-full">
              <div class="w-16 h-16 rounded-3xl bg-cyan-500/10 flex items-center justify-center border border-cyan-500/20 mb-6 group-hover:scale-110 transition-transform">
                <ArrowLeftRight :size="32" class="text-cyan-400" />
              </div>
              <h3 class="text-2xl font-black text-white mb-2 uppercase tracking-tighter">{{ $t('domains.promoTransferTitle') }}</h3>
              <p class="text-base text-gray-500 mb-8 leading-relaxed flex-1 italic">{{ $t('domains.promoTransferDesc') }}</p>
              <div class="mt-auto">
                <NuxtLink 
                  :to="localePath('/domains/transfer')"
                  class="inline-flex items-center gap-3 px-8 py-4 rounded-2xl border-2 border-white/10 hover:border-cyan-500 hover:bg-cyan-500 text-white text-xs font-black uppercase tracking-widest transition-all shadow-xl hover:scale-105 active:scale-95"
                >
                  <ArrowLeftRight :size="16" class="mr-2" />
                  {{ $t('domains.promoTransferBtn') }}
                </NuxtLink>
                <p class="text-[10px] text-gray-600 mt-4 font-bold uppercase tracking-widest">{{ $t('domains.promoTransferNote') }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { Globe, CheckCircle, XCircle, ArrowLeftRight, Server, ArrowRight, AlertCircle } from 'lucide-vue-next'
import { apiFetch } from '~/composables/useApi'
import { useCartStore } from '~/stores/cart'

const { t: $t, locale } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const cart = useCartStore()

/** Domain product ID fetched from the backend products endpoint. */
const domainProductId = ref<number | null>(null)

// SEO
const { baseUrl: domainsBaseUrl } = useSeo({
  title: $t('seo.domains.title'),
  description: $t('seo.domains.description'),
  keywords: $t('seo.domains.keywords'),
  type: 'website',
  path: '/domains'
})

// Schema.org
const { organizationSchema: domainsOrgSchema, injectSchema: domainsInjectSchema } = useSchemaOrg()
domainsInjectSchema([
  domainsOrgSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${domainsBaseUrl}/domains#domainspage`,
    url: `${domainsBaseUrl}/domains`,
    name: $t('seo.domains.title'),
    description: $t('seo.domains.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${domainsBaseUrl}/#organization` }
  }
])

// Mode: register | transfer
const mode = ref('register')


// Register: domain availability search
const searchQuery = ref(typeof route.query.domain === 'string' ? route.query.domain : '')
const searching = ref(false)
/** Shape of the DomainCheckResultDto from the backend. */
interface DomainCheckResult {
  /** The fully-qualified domain name that was checked (e.g. "example.com"). */
  domain: string
  /** Whether the domain is available for registration. */
  available: boolean
  /** Human-readable status string: "available" or "taken". */
  status: string
}

const result = ref<DomainCheckResult | null>(null)
const searchError = ref('')

/** Checks domain availability via the backend API. */
async function searchDomain() {
  const domain = searchQuery.value.trim()
  if (!domain) return
  if (!domain.includes('.')) {
    searchError.value = $t('domains.invalidDomain')
    return
  }
  searching.value = true
  result.value = null
  searchError.value = ''
  try {
    result.value = await apiFetch<DomainCheckResult>('/api/portal/public/domain-check', {
      method: 'POST',
      body: { domain }
    })
  } catch (err: unknown) {
    searchError.value = (err as { data?: { statusMessage?: string } })?.data?.statusMessage || $t('domains.checkFailed')
  } finally {
    searching.value = false
  }
}

// Auto-search if domain query param is present (coming from /products)
onMounted(async () => {
  if (searchQuery.value) searchDomain()

  // Fetch domain product PID for cart
  try {
    const products = await apiFetch<Array<{ id: number; type: string }>>('/api/portal/public/products')
    const domainProduct = products.find(p => p.type === 'Domain')
    if (domainProduct) domainProductId.value = domainProduct.id
  } catch {
    // silent — cart add will be disabled if PID not found
  }
})

/**
 * Performs a quick domain search using the current base name with the given TLD extension.
 *
 * @param ext - TLD extension (e.g. "com", "net").
 */
function quickSearch(ext: string) {
  const base = searchQuery.value.trim().split('.')[0] || 'example'
  searchQuery.value = `${base}.${ext}`
  nextTick(() => searchDomain())
}

/**
 * Adds a domain registration to the cart.
 *
 * @param domain - Full domain name (e.g. "example.com")
 * @param price - Registration price for the selected period
 * @param years - Registration period in years
 */
function addDomainToCart(domain: string, price: number, years: number = 1) {
  if (!domainProductId.value) return

  const tld = domain.substring(domain.indexOf('.') + 1)
  const amdPrice = getRegisterPriceAmd(domain)

  cart.addItem({
    pid: domainProductId.value,
    name: domain,
    billingcycle: 'annually',
    cycleLabel: `${years} Year${years > 1 ? 's' : ''}`,
    price: '',
    prefix: '$',
    rawPrice: '0',
    priceAmd: amdPrice,
    domain,
    itemType: 'domain',
    domainAction: 'register',
    tld,
    years,
  })
}

/**
 * Whether the searched domain's TLD is present and enabled in the pricing data.
 * Returns false when the TLD is not offered by the platform.
 *
 * @returns True if the TLD is supported, false otherwise.
 */
const isTldSupported = computed<boolean>(() => {
  if (!result.value) return true
  const tld = result.value.domain.substring(result.value.domain.indexOf('.') + 1)
  return !!tldPricing.value?.pricing?.[tld]
})

/**
 * Looks up the 1-year registration price for a domain from the TLD pricing data.
 * Returns the price already converted to the current locale's currency.
 *
 * @param domain - Full domain name
 * @returns Registration price in the current display currency, or 0 if not found.
 */
function getRegisterPrice(domain: string): number {
  const tld = domain.substring(domain.indexOf('.') + 1)
  const entry = tldPricing.value?.pricing?.[tld]
  if (!entry?.register?.['1']) return 0
  return parseFloat(entry.register['1'])
}

/**
 * Looks up the 1-year registration price in AMD for a domain.
 * The AMD price is the base price before currency conversion.
 *
 * @param domain - Full domain name
 * @returns Registration price in AMD, or 0 if not found.
 */
function getRegisterPriceAmd(domain: string): number {
  const tld = domain.substring(domain.indexOf('.') + 1)
  const entry = tldPricing.value?.pricing?.[tld]
  if (!entry?.register?.['1']) return 0
  const displayPrice = parseFloat(entry.register['1'])
  // Reverse-convert from display currency back to AMD
  const rate = AMD_RATES[localeCurrency.value] ?? AMD_RATES['USD']!
  return rate > 0 ? displayPrice / rate : 0
}

/** Exchange rates: 1 AMD = X target currency (must match backend) */
const AMD_RATES: Record<string, number> = {
  AMD: 1,
  USD: 1 / 390,
  EUR: 1 / 420,
  RUB: 1 / 4.5,
  GBP: 1 / 490,
}

/** Price entries keyed by period in years. */
interface TldPriceEntry {
  /** Registration prices keyed by period in years. */
  register: Record<string, string>
  /** Transfer prices keyed by period in years. */
  transfer: Record<string, string>
  /** Renewal prices keyed by period in years. */
  renew: Record<string, string>
  /** Category tags for filtering. */
  categories: string[]
}

/** Top-level TLD pricing response shape from the backend. */
interface TldPricingResponse {
  /** Currency metadata. */
  currency: { code: string; prefix: string }
  /** TLD pricing dictionary keyed by extension. */
  pricing: Record<string, TldPriceEntry>
}

// TLD Pricing — convert to locale-appropriate currency
/** Maps the current locale to the display currency. */
const localeCurrency = computed(() => {
  switch (locale.value) {
    case 'hy': return 'AMD'
    case 'ru': return 'RUB'
    default: return 'USD'
  }
})

const tldPricingUrl = computed(() =>
  `/api/portal/public/tld-pricing?currency=${localeCurrency.value}`,
)

const { data: tldPricing, pending: pricingPending } = await useApi<TldPricingResponse>(
  tldPricingUrl,
)

const activeCategory = ref<string | null>(null)

/** Collects unique TLD categories with counts from the pricing data. */
const allCategories = computed(() => {
  if (!tldPricing.value?.pricing) return []
  const cats = new Map<string, number>()
  for (const tld of Object.values(tldPricing.value.pricing)) {
    for (const cat of (tld.categories ?? [])) {
      cats.set(cat, (cats.get(cat) ?? 0) + 1)
    }
  }
  return Array.from(cats.entries()).map(([name, count]) => ({ name, count }))
})

const totalTldCount = computed(() => Object.keys(tldPricing.value?.pricing ?? {}).length)

const filteredEntries = computed<[string, TldPriceEntry][]>(() => {
  if (!tldPricing.value?.pricing) return []
  const entries = Object.entries(tldPricing.value.pricing) as [string, TldPriceEntry][]
  if (!activeCategory.value) return entries
  return entries.filter(([, tld]) => (tld.categories ?? []).includes(activeCategory.value))
})

const displayedTlds = computed(() => Object.fromEntries(filteredEntries.value) as Record<string, TldPriceEntry>)

/**
 * Formats a price string with currency prefix and code.
 *
 * @param price - Price value as a string.
 * @returns Formatted price string or "N/A" for unavailable prices.
 */
function formatPrice(price: string) {
  if (!price || price === '-1' || price === '0.00') return 'N/A'
  const currency = tldPricing.value?.currency
  const code = currency?.code ? ` ${currency.code}` : ' USD'
  return currency ? `${currency.prefix}${price}${code}` : `$${price} USD`
}

/**
 * Translates a TLD category name using i18n, falling back to the raw name.
 *
 * @param name - Category name to translate.
 * @returns Translated category name or the original name if no translation exists.
 */
function translateCategory(name: string) {
  if (!name) return ''
  const key = name.toLowerCase().replace(/\s+/g, '-')
  const translated = $t(`domains.pricing.categories.${key}`)
  return translated !== `domains.pricing.categories.${key}` ? translated : name
}
</script>

<style scoped>
.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
</style>
