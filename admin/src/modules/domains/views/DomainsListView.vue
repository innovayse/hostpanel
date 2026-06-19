<script setup lang="ts">
/**
 * Domains list view -- paginated, searchable table of all domain registrations.
 *
 * Displays domain name, client, status, expiry date, auto-renew flag, and actions.
 * Clicking a domain name navigates to its detail page under the client context.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useDomainsStore } from '../stores/domainsStore'
import { DOMAIN_STATUS_STYLES } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'

const router = useRouter()
const store = useDomainsStore()

/** Status badge style map. */
const statusStyles = DOMAIN_STATUS_STYLES

/** Debounce timer handle for search input. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

/** Local search model bound to the input. */
const searchInput = ref('')

/** Total number of pages based on totalCount and pageSize. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / store.pageSize)))

/**
 * Navigates to the domain detail page within the client context.
 *
 * @param clientId - The client who owns the domain.
 * @param domainId - The domain primary key.
 */
function viewDomain(clientId: number, domainId: number): void {
  router.push(`/clients/${clientId}/domains/${domainId}`)
}

/**
 * Navigates to a specific page and re-fetches domains.
 *
 * @param p - Target page number (1-based).
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  store.page = p
  store.fetchAll()
}

/**
 * Handles search input with debouncing.
 * Resets to page 1 on each new search term.
 */
function handleSearch(): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    store.search = searchInput.value
    store.page = 1
    store.fetchAll()
  }, 350)
}

/**
 * Formats a price with a currency symbol.
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns Formatted price string (e.g. "$12.99 USD").
 */
function formatPrice(price: number, currency: string): string {
  const symbols: Record<string, string> = { USD: '$', EUR: '\u20AC', GBP: '\u00A3', AMD: '\u058F', RUB: '\u20BD' }
  const sym = symbols[currency] ?? ''
  return `${sym}${price.toFixed(2)} ${currency}`
}

onMounted(() => store.fetchAll())

// Clean up debounce timer
watch(searchInput, handleSearch)
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-7">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          Domains
        </h1>
        <p class="text-sm text-text-secondary">Manage all registered domain names across clients</p>
      </div>
    </div>

    <!-- Search bar -->
    <div class="mb-5">
      <div class="relative max-w-sm">
        <svg
          class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted pointer-events-none"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <circle cx="11" cy="11" r="8" />
          <line x1="21" y1="21" x2="16.65" y2="16.65" />
        </svg>
        <input
          v-model="searchInput"
          type="text"
          placeholder="Search domains..."
          class="w-full bg-white/[0.04] border border-border rounded-[10px] pl-9 pr-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
        />
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.domains.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading domains...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.domains.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1.5fr_1.2fr_0.7fr_1fr_0.7fr_0.8fr_0.5fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Expires At</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Renewal Price</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Auto-Renew</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right">Actions</span>
      </div>

      <!-- Rows -->
      <div
        v-for="domain in store.domains"
        :key="domain.id"
        class="grid grid-cols-1 sm:grid-cols-[1.5fr_1.2fr_0.7fr_1fr_0.7fr_0.8fr_0.5fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <!-- Domain Name -->
        <button
          class="text-left text-[0.875rem] font-medium text-primary-400 hover:text-primary-300 truncate transition-colors"
          @click="viewDomain(domain.clientId, domain.id)"
        >
          {{ domain.name }}
        </button>

        <!-- Client -->
        <RouterLink
          :to="`/clients/${domain.clientId}`"
          class="text-[0.82rem] text-text-secondary hover:text-primary-400 truncate transition-colors"
        >
          {{ domain.clientName }}
        </RouterLink>

        <!-- Status -->
        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="statusStyles[domain.status] ?? statusStyles.Cancelled"
        >
          {{ domain.status }}
        </span>

        <!-- Expires At -->
        <span class="text-[0.82rem] text-text-muted">{{ formatDate(domain.expiresAt) }}</span>

        <!-- Renewal Price -->
        <span class="text-[0.82rem] text-text-secondary">{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</span>

        <!-- Auto-Renew -->
        <span
          class="text-[0.82rem] font-medium"
          :class="domain.autoRenew ? 'text-status-green' : 'text-text-muted'"
        >
          {{ domain.autoRenew ? 'Yes' : 'No' }}
        </span>

        <!-- Actions -->
        <div class="flex items-center justify-end">
          <button
            class="text-text-muted hover:text-primary-400 transition-colors p-1"
            title="View domain details"
            @click="viewDomain(domain.clientId, domain.id)"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
              <circle cx="12" cy="12" r="3" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ store.totalCount }} domain{{ store.totalCount !== 1 ? 's' : '' }}
        </span>
        <div class="flex items-center gap-1">
          <button
            :disabled="store.page <= 1"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page - 1)"
          >
            Prev
          </button>
          <span class="text-[0.75rem] text-text-muted px-2">{{ store.page }} / {{ totalPages }}</span>
          <button
            :disabled="store.page >= totalPages"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page + 1)"
          >
            Next
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10" /><line x1="2" y1="12" x2="22" y2="12" /><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">
        {{ searchInput ? 'No domains match your search' : 'No domains found' }}
      </p>
      <p v-if="searchInput" class="text-[0.75rem] text-text-muted">
        Try adjusting your search term
      </p>
    </div>
  </div>
</template>
