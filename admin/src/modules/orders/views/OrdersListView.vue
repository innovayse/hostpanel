<script setup lang="ts">
/**
 * Orders list view — paginated table of all customer orders.
 * Shows Order #, Client, Status, Payment Method, Total, Items, Date, and Actions.
 */
import { onMounted } from 'vue'
import { useOrdersStore } from '../stores/ordersStore'
import { ORDER_STATUS_OPTIONS, ORDER_STATUS_STYLES } from '../../../utils/constants'

const store = useOrdersStore()

/**
 * Formats a date string as a localized date.
 *
 * @param iso - ISO 8601 date string or null.
 * @returns Formatted date string or dash if invalid/null.
 */
function formatDate(iso: string | null): string {
  if (!iso) return '\u2014'
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) return '\u2014'
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/**
 * Formats a currency amount.
 *
 * @param amount - Numeric value.
 * @returns Formatted price string.
 */
function formatCurrency(amount: number): string {
  return `$${amount.toFixed(2)}`
}

/**
 * Computes the total number of pages.
 *
 * @returns Total page count.
 */
function totalPages(): number {
  return Math.max(1, Math.ceil(store.totalCount / store.pageSize))
}

/**
 * Navigates to a specific page.
 *
 * @param p - Page number to navigate to.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages()) return
  store.page = p
  store.fetchAll()
}

/**
 * Handles status filter change.
 *
 * @param event - DOM change event from the select.
 */
function onStatusChange(event: Event): void {
  const val = (event.target as HTMLSelectElement).value
  store.statusFilter = val || null
  store.page = 1
  store.fetchAll()
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5 flex items-end justify-between gap-4">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Orders</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Manage customer orders</p>
      </div>

      <!-- Status filter -->
      <select
        class="h-8 rounded-lg border border-border bg-surface-card px-3 text-[0.78rem] text-text-secondary outline-none focus:border-primary-500/40 transition-colors"
        :value="store.statusFilter ?? ''"
        @change="onStatusChange"
      >
        <option v-for="opt in ORDER_STATUS_OPTIONS" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.orders.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading orders...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.orders.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1fr_1.5fr_0.7fr_1fr_0.8fr_0.5fr_0.8fr_0.9fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Order #</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Items</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Rows -->
      <div
        v-for="order in store.orders"
        :key="order.id"
        class="grid grid-cols-1 sm:grid-cols-[1fr_1.5fr_0.7fr_1fr_0.8fr_0.5fr_0.8fr_0.9fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <span class="text-[0.82rem] font-medium text-text-primary font-mono">{{ order.orderNumber }}</span>

        <RouterLink
          :to="`/clients/${order.clientId}`"
          class="text-[0.82rem] text-primary-400 hover:text-primary-300 truncate transition-colors"
        >
          {{ order.clientName }}
        </RouterLink>

        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="ORDER_STATUS_STYLES[order.status] ?? 'text-text-muted bg-white/[0.04] border-border'"
        >
          {{ order.status }}
        </span>

        <span class="text-[0.82rem] text-text-secondary truncate">{{ order.paymentMethod }}</span>

        <span class="text-[0.82rem] text-text-secondary">{{ formatCurrency(order.total) }}</span>

        <span class="text-[0.82rem] text-text-muted">{{ order.itemCount }}</span>

        <span class="text-[0.82rem] text-text-muted">{{ formatDate(order.createdAt) }}</span>

        <!-- Actions -->
        <div class="flex items-center gap-1">
          <!-- Accept -->
          <button
            v-if="order.status === 'Pending'"
            title="Accept"
            class="flex items-center justify-center w-7 h-7 rounded-lg border border-border text-text-muted hover:text-status-green hover:border-status-green/30 hover:bg-status-green/6 transition-colors"
            @click.stop="store.acceptOrder(order.id).then(() => store.fetchAll())"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </button>

          <!-- Cancel -->
          <button
            v-if="order.status === 'Pending'"
            title="Cancel"
            class="flex items-center justify-center w-7 h-7 rounded-lg border border-border text-text-muted hover:text-status-red hover:border-status-red/30 hover:bg-status-red/6 transition-colors"
            @click.stop="store.cancelOrder(order.id).then(() => store.fetchAll())"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>

          <!-- View -->
          <RouterLink
            :to="`/orders/${order.id}`"
            title="View"
            class="flex items-center justify-center w-7 h-7 rounded-lg border border-border text-text-muted hover:text-primary-400 hover:border-primary-500/30 hover:bg-primary-500/6 transition-colors"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" /><circle cx="12" cy="12" r="3" />
            </svg>
          </RouterLink>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages() > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ store.totalCount }} order{{ store.totalCount !== 1 ? 's' : '' }}
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
          <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" />
          <rect x="9" y="3" width="6" height="4" rx="1" />
          <line x1="9" y1="12" x2="15" y2="12" /><line x1="9" y1="16" x2="13" y2="16" />
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No orders found</p>
    </div>
  </div>
</template>
