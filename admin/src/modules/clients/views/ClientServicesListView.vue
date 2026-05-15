<script setup lang="ts">
/**
 * Client services list -- shows all services belonging to the current client.
 * Navigates to the service detail page when a row is clicked.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import { SERVICE_STATUS_STYLES } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import type { ServiceListItem, PagedResult } from '../../../types/models'

const route = useRoute()
const router = useRouter()
const { request } = useApi()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded services list for the current page. */
const services = ref<ServiceListItem[]>([])

/** True while a request is in flight. */
const loading = ref(false)

/** Error message, null when no error. */
const error = ref<string | null>(null)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(20)

/** Total number of matching items across all pages. */
const totalCount = ref(0)

/** Status badge style map. */
const statusStyles = SERVICE_STATUS_STYLES

/**
 * Formats a price with its currency symbol.
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns Formatted price string (e.g. "$4.99 USD").
 */
function formatPrice(price: number, currency: string): string {
  const symbols: Record<string, string> = { USD: '$', EUR: '\u20AC', GBP: '\u00A3', AMD: '\u058F', RUB: '\u20BD' }
  const sym = symbols[currency] ?? ''
  return `${sym}${price.toFixed(2)} ${currency}`
}

/**
 * Capitalizes the first letter of a billing cycle string.
 *
 * @param cycle - Raw billing cycle value (e.g. "monthly").
 * @returns Capitalized string (e.g. "Monthly").
 */
function capitalizeCycle(cycle: string): string {
  if (!cycle) return '\u2014'
  return cycle.charAt(0).toUpperCase() + cycle.slice(1)
}

/**
 * Fetches paginated services for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchServices(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const result = await request<PagedResult<ServiceListItem>>(
      `/services?clientId=${clientId.value}&page=${page.value}&pageSize=${pageSize.value}`,
    )
    services.value = result.items
    totalCount.value = result.totalCount
  } catch {
    error.value = 'Failed to load services.'
  } finally {
    loading.value = false
  }
}

/** Total number of pages based on totalCount and pageSize. */
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))

/**
 * Navigate to a specific page and re-fetch.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchServices()
}

/**
 * Navigates to the service detail page for the given service.
 *
 * @param serviceId - The ID of the service to view.
 */
function viewService(serviceId: number): void {
  router.push(`/clients/${clientId.value}/services/${serviceId}`)
}

onMounted(() => fetchServices())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="loading && services.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading services...
    </div>

    <!-- Error -->
    <div v-else-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ error }}
    </div>

    <!-- Table -->
    <div v-else-if="services.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.4fr_1.5fr_1.5fr_1fr_0.8fr_1fr_0.7fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product/Service</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Price</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Billing Cycle</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Next Due</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
      </div>

      <!-- Rows -->
      <div
        v-for="service in services"
        :key="service.id"
        class="grid grid-cols-1 sm:grid-cols-[0.4fr_1.5fr_1.5fr_1fr_0.8fr_1fr_0.7fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors cursor-pointer"
        @click="viewService(service.id)"
      >
        <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ service.id }}</span>

        <span class="text-[0.875rem] font-medium text-text-primary truncate">{{ service.productName }}</span>

        <span class="text-[0.82rem] text-text-secondary truncate">{{ service.domain ?? '\u2014' }}</span>

        <span class="text-[0.82rem] text-text-secondary">{{ formatPrice(service.price, service.priceCurrency) }}</span>

        <span class="text-[0.82rem] text-text-secondary">{{ capitalizeCycle(service.billingCycle) }}</span>

        <span class="text-[0.82rem] text-text-muted">{{ formatDate(service.nextDueDate) }}</span>

        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="statusStyles[service.status] ?? statusStyles.Terminated"
        >
          {{ service.status }}
        </span>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ totalCount }} service{{ totalCount !== 1 ? 's' : '' }}
        </span>
        <div class="flex items-center gap-1">
          <button
            :disabled="page <= 1"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(page - 1)"
          >
            Prev
          </button>
          <span class="text-[0.75rem] text-text-muted px-2">{{ page }} / {{ totalPages }}</span>
          <button
            :disabled="page >= totalPages"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(page + 1)"
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
          <rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" />
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No services found for this client</p>
    </div>
  </div>
</template>
