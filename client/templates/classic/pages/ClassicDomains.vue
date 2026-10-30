<template>
  <UiStack gap="none" class="overflow-x-hidden bg-classic-page" data-testid="classic-domains">
    <!-- Hero / search: flat band, left aligned -->
    <UiSection :contained="false" padding="lg" class="border-b border-classic-border bg-classic-surface-2 py-10 md:py-14">
      <UiContainer>
        <UiStack gap="none">
          <!-- Breadcrumbs -->
          <UiBreadcrumbs
            :items="[{ label: $t('domains.badge') }]"
            container-class="mb-6 flex"
          />

          <UiStack direction="row" align="center" gap="xs" class="inline-flex self-start px-3 py-1.5 mb-4 rounded-md bg-classic-brand-soft">
            <Globe :size="14" :stroke-width="2" class="text-classic-brand" />
            <UiText as="span" size="xs" tone="inherit" weight="medium" class="uppercase tracking-wider text-classic-brand">{{ $t('domains.badge') }}</UiText>
          </UiStack>

          <UiHeading :level="UiHeadingLevel.H1" size="title" class="mb-3 text-3xl font-semibold leading-tight text-classic-ink md:text-4xl" data-testid="classic-domains-title">
            {{ $t('domains.title') }}
            <UiText as="span" size="inherit" tone="inherit" weight="semibold" class="text-classic-brand">{{ $t('domains.titleHighlight') }}</UiText>
          </UiHeading>

          <!-- Register: search bar -->
          <UiStack gap="none" class="mt-6 w-full">
            <!-- Tab switcher -->
            <UiStack direction="row" gap="sm" class="mb-5">
              <UiButton variant="primary" size="sm" data-testid="classic-domains-tab-register">
                <Globe :size="14" :stroke-width="2" class="mr-2" />
                {{ $t('domains.tabRegister') }}
              </UiButton>
              <UiButton variant="secondary" size="sm" :to="localePath('/domains/transfer')" data-testid="classic-domains-tab-transfer">
                <ArrowLeftRight :size="14" :stroke-width="2" class="mr-2" />
                {{ $t('domains.tabTransfer') }}
              </UiButton>
            </UiStack>

            <UiSearchBar
              v-model="searchQuery"
              :placeholder="$t('domains.placeholder')"
              :loading="searching"
              :button-label="$t('domains.search')"
              @search="searchDomain"
            />

            <!-- Availability result -->
            <UiStack v-if="result" gap="none" class="mt-6" data-testid="classic-domains-result">
              <!-- TLD not supported -->
              <UiCard
                v-if="!isTldSupported"
                padding="none"
                class="p-5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-amber-500/50"
                data-testid="classic-domains-unsupported"
              >
                <UiStack direction="row" align="center" gap="md">
                  <UiStack direction="row" align="center" justify="center" gap="none" class="w-10 h-10 rounded-md bg-amber-500/10">
                    <AlertCircle :size="22" class="text-amber-600 dark:text-amber-400" />
                  </UiStack>
                  <UiStack gap="none" class="text-left">
                    <UiText tone="inherit" weight="semibold" class="text-lg text-classic-ink">{{ result.domain }}</UiText>
                    <UiText size="xs" tone="inherit" weight="medium" class="mt-0.5 text-amber-700 dark:text-amber-400">
                      {{ $t('domains.tldNotSupported') }}
                    </UiText>
                  </UiStack>
                </UiStack>
              </UiCard>

              <!-- TLD supported: normal result -->
              <UiCard
                v-else
                padding="none"
                class="p-5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4"
                :class="result.available ? 'border-green-600/50' : 'border-red-600/50'"
                data-testid="classic-domains-availability"
              >
                <UiStack direction="row" align="center" gap="md">
                  <UiStack
                    direction="row"
                    align="center"
                    justify="center"
                    gap="none"
                    class="w-10 h-10 rounded-md"
                    :class="result.available ? 'bg-green-500/10' : 'bg-red-500/10'"
                  >
                    <CheckCircle v-if="result.available" :size="22" class="text-green-600 dark:text-green-400" />
                    <XCircle v-else :size="22" class="text-red-600 dark:text-red-400" />
                  </UiStack>
                  <UiStack gap="none" class="text-left">
                    <UiText tone="inherit" weight="semibold" class="text-lg text-classic-ink">{{ result.domain }}</UiText>
                    <UiText
                      size="xs"
                      :tone="result.available ? 'success' : 'danger'"
                      weight="medium"
                      class="mt-0.5"
                    >
                      {{ result.available ? $t('domains.available') : $t('domains.taken') }}
                    </UiText>
                  </UiStack>
                </UiStack>
                <UiStack direction="row" align="center" gap="sm" class="w-full sm:w-auto">
                  <UiButton
                    v-if="result.available"
                    variant="primary"
                    size="lg"
                    class="flex-1 sm:flex-none px-8"
                    :disabled="cart.hasDomainItem(result.domain, DomainOrderAction.Register) || !canAddDomain(result.domain)"
                    data-testid="classic-domains-register"
                    @click="addDomainToCart(result.domain)"
                  >
                    {{ cart.hasDomainItem(result.domain, DomainOrderAction.Register) ? $t('domains.alreadyInCart') : $t('domains.registerNow') }}
                  </UiButton>
                  <UiButton
                    v-else
                    variant="outline"
                    full-width
                    :to="localePath('/domains/transfer') + (result?.domain ? `?domain=${result.domain}` : '')"
                    data-testid="classic-domains-transfer"
                  >
                    <ArrowLeftRight :size="15" :stroke-width="2" class="mr-2" />
                    {{ $t('domains.takenTransfer') }}
                  </UiButton>
                </UiStack>
              </UiCard>
            </UiStack>

            <!-- Search error -->
            <UiAlert v-if="searchError" variant="error" class="mt-6 text-left" data-testid="classic-domains-error">
              {{ searchError }}
            </UiAlert>
          </UiStack>
        </UiStack>
      </UiContainer>
    </UiSection>

    <!-- TLD pricing table -->
    <UiSection :contained="false" padding="md" class="md:py-14">
      <UiContainer>
        <UiStack gap="none">
          <UiStack gap="none" class="mb-8">
            <UiText as="span" size="xs" tone="inherit" weight="semibold" class="block mb-2 uppercase tracking-wider text-classic-brand">{{ $t('domains.pricing.browseByCategory') }}</UiText>
            <UiHeading :level="UiHeadingLevel.H2" size="title" class="text-2xl font-semibold text-classic-ink md:text-3xl">{{ $t('domains.pricing.title') }}</UiHeading>
          </UiStack>

          <UiGrid v-if="pricingPending" cols="4" gap="lg" class="py-12" data-testid="classic-domains-loading">
            <UiSkeleton v-for="i in 8" :key="i" shape="block" class="h-28 rounded-lg" />
          </UiGrid>

          <template v-else-if="tldPricing">
            <!-- Category filter -->
            <UiChipTabs
              v-if="allCategories.length"
              class="mb-8"
              :tabs="categoryTabs"
              :model-value="activeCategory ?? ALL_CATEGORY"
              :label="$t('domains.pricing.browseByCategory')"
              testid="classic-domains-categories"
              @update:model-value="chooseCategory"
            />

            <!-- Desktop: table -->
            <UiCard padding="none" class="hidden md:block overflow-hidden">
              <UiTable class="border-0 rounded-none">
                <UiTableHead>
                  <UiTableRow :hoverable="false" class="bg-classic-surface-2">
                    <UiTableTh class="!py-4 !px-6 text-xs font-semibold uppercase tracking-wider text-classic-muted">{{ $t('domains.pricing.extension') }}</UiTableTh>
                    <UiTableTh align="center" class="text-xs font-semibold uppercase tracking-wider text-classic-muted">{{ $t('domains.pricing.register') }}</UiTableTh>
                    <UiTableTh align="center" class="text-xs font-semibold uppercase tracking-wider text-classic-muted">{{ $t('domains.pricing.transfer') }}</UiTableTh>
                    <UiTableTh align="center" class="text-xs font-semibold uppercase tracking-wider text-classic-muted">{{ $t('domains.pricing.renew') }}</UiTableTh>
                    <UiTableTh class="!pr-6" />
                  </UiTableRow>
                </UiTableHead>
                <UiTableBody>
                  <UiTableRow v-for="[ext, tld] in displayedTlds" :key="ext" group class="hover:bg-classic-surface-2" data-testid="classic-domains-row">
                    <UiTableTd class="!py-4 !px-6">
                      <UiText as="span" tone="inherit" weight="semibold" class="text-xl text-classic-ink group-hover:text-classic-brand transition-colors">.{{ ext }}</UiText>
                    </UiTableTd>
                    <UiTableTd align="center">
                      <UiText as="span" tone="inherit" weight="semibold" class="block text-lg tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.register, DEFAULT_YEARS)) }}</UiText>
                      <UiText as="span" size="xs" tone="inherit" class="text-classic-muted">{{ $t('domains.pricing.perYear') }}</UiText>
                    </UiTableTd>
                    <UiTableTd align="center">
                      <UiText as="span" tone="inherit" weight="semibold" class="block text-lg tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.transfer, DEFAULT_YEARS)) }}</UiText>
                      <UiText as="span" size="xs" tone="inherit" class="text-classic-muted">{{ $t('domains.pricing.perYear') }}</UiText>
                    </UiTableTd>
                    <UiTableTd align="center">
                      <UiText as="span" tone="inherit" weight="semibold" class="block text-lg tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.renew, DEFAULT_YEARS)) }}</UiText>
                      <UiText as="span" size="xs" tone="inherit" class="text-classic-muted">{{ $t('domains.pricing.perYear') }}</UiText>
                    </UiTableTd>
                    <UiTableTd align="right" class="!pr-6">
                      <UiButton
                        size="sm"
                        variant="outline"
                        data-testid="classic-domains-quick-search"
                        @click="quickSearch(ext)"
                      >
                        {{ $t('domains.search') }}
                      </UiButton>
                    </UiTableTd>
                  </UiTableRow>
                  <UiTableRow v-if="!displayedTlds.length" :hoverable="false">
                    <UiTableTd colspan="5" align="center" class="py-14 text-classic-muted">
                      {{ $t('domains.pricing.noResults') }}
                    </UiTableTd>
                  </UiTableRow>
                </UiTableBody>
              </UiTable>
            </UiCard>

            <!-- Mobile: card layout -->
            <UiGrid cols="1" gap="md" class="md:hidden">
              <UiCard
                v-for="[ext, tld] in displayedTlds"
                :key="ext"
                padding="none"
                hover
                class="p-5 group"
                data-testid="classic-domains-card"
              >
                <UiStack direction="row" align="center" justify="between" gap="none" class="mb-4">
                  <UiText as="span" tone="inherit" weight="semibold" class="text-xl text-classic-ink">.{{ ext }}</UiText>
                  <Globe :size="16" class="text-classic-muted" />
                </UiStack>

                <UiStack direction="row" gap="xs" class="mb-4 p-3 rounded-md bg-classic-surface-2 border border-classic-border">
                  <UiStack gap="none" class="flex-1 text-center">
                    <UiText as="span" size="xs" tone="inherit" class="block mb-1 text-classic-muted">{{ $t('domains.pricing.register') }}</UiText>
                    <UiText as="span" size="sm" tone="inherit" weight="semibold" class="block tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.register, DEFAULT_YEARS)) }}</UiText>
                  </UiStack>
                  <UiStack gap="none" class="flex-1 text-center border-x border-classic-border">
                    <UiText as="span" size="xs" tone="inherit" class="block mb-1 text-classic-muted">{{ $t('domains.pricing.transfer') }}</UiText>
                    <UiText as="span" size="sm" tone="inherit" weight="semibold" class="block tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.transfer, DEFAULT_YEARS)) }}</UiText>
                  </UiStack>
                  <UiStack gap="none" class="flex-1 text-center">
                    <UiText as="span" size="xs" tone="inherit" class="block mb-1 text-classic-muted">{{ $t('domains.pricing.renew') }}</UiText>
                    <UiText as="span" size="sm" tone="inherit" weight="semibold" class="block tabular-nums text-classic-ink">{{ formatPrice(priceForYears(tld.renew, DEFAULT_YEARS)) }}</UiText>
                  </UiStack>
                </UiStack>

                <UiButton
                  variant="primary"
                  full-width
                  data-testid="classic-domains-card-search"
                  @click="quickSearch(ext)"
                >
                  {{ $t('domains.pricing.registerNow') }}
                </UiButton>
              </UiCard>

              <UiText v-if="!displayedTlds.length" tone="inherit" class="py-14 text-center text-classic-muted">
                {{ $t('domains.pricing.noResults') }}
              </UiText>
            </UiGrid>
          </template>

          <!-- Promo boxes -->
          <UiGrid cols="2" gap="lg" class="mt-14">
            <!-- Add web hosting -->
            <UiCard padding="none" class="p-8" data-testid="classic-domains-promo-hosting">
              <UiStack gap="none" class="items-start">
                <UiStack direction="row" align="center" justify="center" gap="none" class="w-12 h-12 rounded-md bg-classic-brand-soft mb-4">
                  <Server :size="24" class="text-classic-brand" />
                </UiStack>
                <UiHeading :level="UiHeadingLevel.H3" size="section" class="mb-1 text-xl font-semibold text-classic-ink">{{ $t('domains.promoHostingTitle') }}</UiHeading>
                <UiText size="sm" tone="inherit" weight="medium" class="mb-3 text-classic-brand">{{ $t('domains.promoHostingSubtitle') }}</UiText>
                <UiText tone="inherit" class="mb-6 leading-relaxed text-classic-muted">{{ $t('domains.promoHostingDesc') }}</UiText>
                <UiButton
                  :to="localePath('/hosting')"
                  variant="primary"
                  size="lg"
                  class="gap-3 px-8"
                  data-testid="classic-domains-promo-hosting-link"
                >
                  {{ $t('domains.promoHostingBtn') }}
                  <ArrowRight :size="16" />
                </UiButton>
              </UiStack>
            </UiCard>

            <!-- Transfer -->
            <UiCard padding="none" class="p-8" data-testid="classic-domains-promo-transfer">
              <UiStack gap="none" class="items-start h-full">
                <UiStack direction="row" align="center" justify="center" gap="none" class="w-12 h-12 rounded-md bg-classic-brand-soft mb-4">
                  <ArrowLeftRight :size="24" class="text-classic-brand" />
                </UiStack>
                <UiHeading :level="UiHeadingLevel.H3" size="section" class="mb-1 text-xl font-semibold text-classic-ink">{{ $t('domains.promoTransferTitle') }}</UiHeading>
                <UiText tone="inherit" class="mb-6 leading-relaxed flex-1 text-classic-muted">{{ $t('domains.promoTransferDesc') }}</UiText>
                <UiStack gap="none" class="mt-auto items-start">
                  <UiButton
                    :to="localePath('/domains/transfer')"
                    variant="outline"
                    size="lg"
                    class="gap-3 px-8"
                    data-testid="classic-domains-promo-transfer-link"
                  >
                    <ArrowLeftRight :size="16" class="mr-2" />
                    {{ $t('domains.promoTransferBtn') }}
                  </UiButton>
                  <UiText size="xs" tone="inherit" class="mt-3 text-classic-muted">{{ $t('domains.promoTransferNote') }}</UiText>
                </UiStack>
              </UiStack>
            </UiCard>
          </UiGrid>
        </UiStack>
      </UiContainer>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicDomains - the classic template's domains page: the availability search with its result,
 * the TLD price table (desktop table, mobile cards) with a category filter, and the two promo
 * boxes.
 *
 * The availability search and the cart actions go through the stores; the price table is read
 * through the catalogue store in the payer's own currency and never converted.
 */
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { Globe, CheckCircle, XCircle, ArrowLeftRight, Server, ArrowRight, AlertCircle } from 'lucide-vue-next'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { useCatalogStore } from '~/stores/catalog'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'
import type { TldPriceEntry } from '~/types/tldpriceentry'
import { formatMoney } from '~/utils/formatMoney'
import { tldPriceEntry } from '~/utils/tldPriceEntry'
import { ONE_YEAR, priceForYears } from '~/utils/tldPeriodPrice'
import { UiHeadingLevel } from '~/utils/ui/enums'
import { DOMAIN_BILLING_CYCLE } from '~/constants/pricingCycle'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import type { ChipTab } from '~/types/chiptab'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { tldCategoryLabel, tldCategorySlug } from '~/utils/tldCategory'

/** One extension's entry of the price table. */
type TldEntry = TldPriceEntry

/** Registration period, in years, of a quick add and of the one-year price columns. */
const DEFAULT_YEARS = ONE_YEAR

/** Base name used when a quick search starts from an empty search box. */
const FALLBACK_BASE_NAME = 'example'

/** Translate function and the active locale. */
const { t, locale } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Current route; `?domain=` pre-fills the search. */
const route = useRoute()

/** Cart store. */
const cart = useCartStore()

/** Domains store: the availability search and the domain product id. */
const domainsStore = useDomainsStore()
domainsStore.reset()

/** Domain product ID fetched from the backend products endpoint. */
const domainProductId = ref<number | null>(null)

// SEO and structured data live in pages/domains/index.vue so both templates emit
// the same head.

/** The text in the search box. */
const searchQuery = ref(typeof route.query[DOMAIN_SEARCH_QUERY_PARAM] === 'string' ? route.query[DOMAIN_SEARCH_QUERY_PARAM] : '')

/** Whether an availability search is in flight (owned by the store). */
const searching = computed(() => domainsStore.searching)

/** The availability answer (owned by the store). */
const result = computed(() => domainsStore.searchResult)

/** The page's own validation sentence, shown before any request is made. */
const validationError = ref('')

/** The sentence to show: local validation first, else the store's API refusal. */
const searchError = computed(() => validationError.value || domainsStore.searchError)

/** Checks domain availability through the store. */
const searchDomain = async (): Promise<void> => {
  const domain = searchQuery.value.trim()
  if (!domain) {
    return
  }
  validationError.value = ''
  if (!domain.includes('.')) {
    validationError.value = t('domains.invalidDomain')
    return
  }
  const outcome = await domainsStore.checkAvailability(domain)
  if (outcome === DomainActionOutcome.Failed && !domainsStore.searchError) {
    validationError.value = t('domains.checkFailed')
  }
}

/**
 * Currency store. TLD pricing is priced in the payer's own currency, never the page's language.
 */
const currencyStore = useCurrencyStore()

// Auto-search if a domain query param is present (coming from /products)
onMounted(async () => {
  currencyStore.init()
  currencyStore.load()

  if (searchQuery.value) {
    searchDomain()
  }

  // Fetch the domain product PID for the cart; null disables cart add
  domainProductId.value = await domainsStore.fetchDomainProductId()
})

/** Catalogue store: the TLD price table. */
const catalogStore = useCatalogStore()

/** The Nuxt instance, kept so a read that starts later still runs inside its context. */
const nuxtApp = useNuxtApp()

// The price table is read in the payer's currency; the read runs again once `payerCode`
// resolves or changes. The read runs inside the Nuxt context, for the reason given on the hosting page.
/** Whether the price table is still loading. */
const { pending: pricingPending } = await usePayerAsyncData('domains:classic-tld-pricing', () => nuxtApp.runWithContext(() => catalogStore.loadTldPricing()), {
  watch: [locale]
})

/** The price table (owned by the catalogue store). */
const tldPricing = computed(() => catalogStore.tldPricing)

/** The category the table is filtered to; null shows every extension. */
const activeCategory = ref<string | null>(null)

/**
 * The extension part of a domain name.
 *
 * @param domain - Full domain name (e.g. "example.com").
 * @returns Everything after the first dot (e.g. "com").
 */
const tldOf = (domain: string): string => domain.substring(domain.indexOf('.') + 1)

/**
 * Performs a quick domain search using the current base name with the given TLD extension.
 *
 * @param ext - TLD extension (e.g. "com", "net").
 */
const quickSearch = (ext: string): void => {
  const base = searchQuery.value.trim().split('.')[0] || FALLBACK_BASE_NAME
  searchQuery.value = `${base}.${ext}`
  nextTick(() => searchDomain())
}

/**
 * Whether a domain can be added to the cart: its TLD has a price table entry. The entry is
 * already converted to the payer's currency by the API, whatever currency the TLD sells in.
 *
 * @param domain - Full domain name.
 * @returns True when the TLD is priced.
 */
const canAddDomain = (domain: string): boolean => {
  return !!tldPriceEntry(tldPricing.value, tldOf(domain))
}

/**
 * Adds a domain registration to the cart.
 *
 * Refuses when the TLD has no registration price for the period. The price is the API's, already
 * converted to the payer's currency. Callers gate the button on {@link canAddDomain} first;
 * this is the second line of defence.
 *
 * @param domain - Full domain name (e.g. "example.com").
 * @param years - Registration period in years.
 */
const addDomainToCart = (domain: string, years: number = DEFAULT_YEARS): void => {
  if (!domainProductId.value || !currencyStore.payerCode) {
    return
  }

  const tld = tldOf(domain)
  const amount = priceForYears(tldPriceEntry(tldPricing.value, tld)?.register, years)
  if (amount === null) {
    return
  }

  cart.addItem({
    pid: domainProductId.value,
    name: domain,
    billingcycle: DOMAIN_BILLING_CYCLE,
    cycleLabel: t('domains.years', years),
    amount,
    currency: currencyStore.payerCode,
    domain,
    itemType: CartItemType.Domain,
    domainAction: DomainOrderAction.Register,
    tld,
    years,
  })
}

/**
 * Whether the searched domain's TLD is present and enabled in the pricing data.
 * False when the TLD is not offered by the platform.
 */
const isTldSupported = computed<boolean>(() => {
  if (!result.value) {
    return true
  }
  return !!tldPriceEntry(tldPricing.value, tldOf(result.value.domain))
})

/** Unique TLD categories with their counts, from the pricing data. */
const allCategories = computed(() => {
  if (!tldPricing.value) {
    return []
  }
  const cats = new Map<string, number>()
  for (const tld of tldPricing.value.pricing) {
    for (const cat of (tld.categories ?? [])) {
      cats.set(cat, (cats.get(cat) ?? 0) + 1)
    }
  }
  return Array.from(cats.entries()).map(([name, count]) => ({ name, count }))
})

/** The category chips: "all" first (with the full count), then one per category the data carries. */
const categoryTabs = computed<ChipTab[]>(() => [
  { value: ALL_CATEGORY, label: t('domains.pricing.catAll'), count: totalTldCount.value, testid: 'classic-domains-category-all' },
  ...allCategories.value.map(cat => ({
    value: cat.name,
    label: tldCategoryLabel(cat.name, t),
    count: cat.count,
    testid: `classic-domains-category-${tldCategorySlug(cat.name)}`,
  })),
])

/**
 * Applies a chip choice to the filter.
 *
 * @param value - The chosen category, or the "all" sentinel to show every extension.
 */
const chooseCategory = (value: string): void => {
  activeCategory.value = value === ALL_CATEGORY ? null : value
}

/** How many extensions the table holds before filtering. */
const totalTldCount = computed(() => tldPricing.value?.pricing.length ?? 0)

/** The extensions that pass the category filter, as `[extension, entry]` pairs. */
const displayedTlds = computed<Array<[string, TldEntry]>>(() => {
  if (!tldPricing.value) {
    return []
  }
  const entries = tldPricing.value.pricing.map((entry): [string, TldEntry] => [entry.tld, entry])
  const category = activeCategory.value
  if (!category) {
    return entries
  }
  return entries.filter(([, tld]) => (tld.categories ?? []).includes(category))
})

/**
 * Formats a price with the payer's currency.
 *
 * @param price - The period's price, or null when that period is not offered.
 * @returns Formatted price, or the "not available" label when there is none.
 */
const formatPrice = (price: number | null): string => {
  if (price === null) {
    return t('domains.pricing.notAvailable')
  }
  const code = tldPricing.value?.currency?.code
  return formatMoney(price, currencyStore.moneyCurrencyFor(code))
}
</script>
