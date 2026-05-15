<script setup lang="ts">
/**
 * Domain Registrations — paginated table of all domain registrations across clients.
 * Shows ID, Domain, Client Name, Reg Period, Registrar, Price, Next Due Date, Expiry Date, Status.
 */
import { onMounted } from 'vue'
import { useDomainsStore } from '../../domains/stores/domainsStore'

const store = useDomainsStore()

/** Status badge style map. */
const statusStyles: Record<string, string> = {
  Active: 'text-status-green bg-status-green/10 border-status-green/20',
  PendingRegistration: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  PendingTransfer: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Expired: 'text-status-red bg-status-red/10 border-status-red/20',
  Redemption: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Transferred: 'text-text-muted bg-white/[0.04] border-border',
  Cancelled: 'text-text-muted bg-white/[0.04] border-border',
}

/**
 * Formats a date string as a localized date.
 *
 * @param iso - ISO 8601 date string.
 * @returns Formatted date string or "—" if invalid.
 */
function formatDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) return '—'
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/**
 * Formats a price with its currency.
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns Formatted price string (e.g. "$23.70 USD").
 */
function formatPrice(price: number, currency: string): string {
  const symbols: Record<string, string> = { USD: '$', EUR: '€', GBP: '£', AMD: '֏', RUB: '₽' }
  const sym = symbols[currency] ?? ''
  return `${sym}${price.toFixed(2)} ${currency}`
}

/** Total number of pages. */
function totalPages(): number {
  return Math.max(1, Math.ceil(store.totalCount / store.pageSize))
}

/** Navigate to a page. */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages()) return
  store.page = p
  store.fetchAll()
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Domain Registrations</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">View and manage client domain registrations</p>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.domains.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading domains…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.domains.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.4fr_1.5fr_1.5fr_0.7fr_1.5fr_1fr_1fr_1fr_0.7fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Reg Period</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Registrar</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Price</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Next Due Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Expiry Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
      </div>

      <!-- Rows -->
      <div
        v-for="domain in store.domains"
        :key="domain.id"
        class="grid grid-cols-1 sm:grid-cols-[0.4fr_1.5fr_1.5fr_0.7fr_1.5fr_1fr_1fr_1fr_0.7fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ domain.id }}</span>

        <span class="text-[0.875rem] font-medium text-text-primary truncate">{{ domain.name }}</span>

        <RouterLink
          :to="`/clients/${domain.clientId}`"
          class="text-[0.82rem] text-primary-400 hover:text-primary-300 truncate transition-colors"
        >
          {{ domain.clientName }}
        </RouterLink>

        <span class="text-[0.82rem] text-text-secondary">{{ domain.regPeriod }}</span>

        <span class="text-[0.82rem] text-text-secondary truncate">{{ domain.registrar ?? '—' }}</span>

        <span class="text-[0.82rem] text-text-secondary">{{ formatPrice(domain.price, domain.priceCurrency) }}</span>

        <span class="text-[0.82rem] text-text-muted">{{ formatDate(domain.nextDueDate) }}</span>

        <span class="text-[0.82rem] text-text-muted">{{ formatDate(domain.expiresAt) }}</span>

        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="statusStyles[domain.status] ?? statusStyles.Cancelled"
        >
          {{ domain.status }}
        </span>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages() > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
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
          <span class="text-[0.75rem] text-text-muted px-2">{{ store.page }} / {{ totalPages() }}</span>
          <button
            :disabled="store.page >= totalPages()"
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
          <circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/>
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No domain registrations found</p>
    </div>
  </div>
</template>
