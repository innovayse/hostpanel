<script setup lang="ts">
/**
 * Order detail view — displays a single order with its items, invoice link,
 * and admin action buttons (accept, cancel, delete).
 */
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useOrdersStore } from '../stores/ordersStore'
import { ORDER_STATUS_STYLES, INVOICE_STATUS_STYLES } from '../../../utils/constants'

const route = useRoute()
const router = useRouter()
const store = useOrdersStore()

/** Whether the delete confirmation dialog is visible. */
const showDeleteConfirm = ref(false)

/** Admin notes text area value. */
const notesText = ref('')

/** Item status badge styles. */
const itemStatusStyles: Record<string, string> = {
  Pending: 'text-status-yellow bg-status-yellow/10 border border-status-yellow/20',
  Active: 'text-status-green bg-status-green/10 border border-status-green/20',
  Cancelled: 'text-status-red bg-status-red/10 border border-status-red/20',
}

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
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
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
 * Capitalizes the first letter of a billing cycle string.
 *
 * @param cycle - Raw billing cycle value.
 * @returns Capitalized string.
 */
function capitalizeCycle(cycle: string): string {
  if (!cycle) return '\u2014'
  return cycle.charAt(0).toUpperCase() + cycle.slice(1)
}

/**
 * Accepts the current order.
 *
 * @returns Promise that resolves when the action is done.
 */
async function handleAccept(): Promise<void> {
  const id = Number(route.params.id)
  await store.acceptOrder(id)
}

/**
 * Cancels the current order.
 *
 * @returns Promise that resolves when the action is done.
 */
async function handleCancel(): Promise<void> {
  const id = Number(route.params.id)
  await store.cancelOrder(id)
}

/**
 * Deletes the current order and navigates back to the list.
 *
 * @returns Promise that resolves when the order is deleted.
 */
async function handleDelete(): Promise<void> {
  const id = Number(route.params.id)
  await store.deleteOrder(id)
  await router.push('/orders')
}

onMounted(async () => {
  const id = Number(route.params.id)
  await store.fetchOne(id)
  if (store.currentOrder) {
    notesText.value = store.currentOrder.notes ?? ''
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full max-w-6xl">

    <!-- Back button -->
    <RouterLink to="/orders" class="inline-flex items-center gap-1.5 text-[0.78rem] text-text-muted hover:text-text-secondary transition-colors mb-4">
      <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="15 18 9 12 15 6" />
      </svg>
      Back to orders
    </RouterLink>

    <!-- Loading -->
    <div v-if="store.loading && !store.currentOrder" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading order...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <template v-else-if="store.currentOrder">
      <!-- Header -->
      <div class="flex items-center justify-between gap-4 mb-6">
        <div class="flex items-center gap-3">
          <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">
            {{ store.currentOrder.orderNumber }}
          </h1>
          <span
            class="inline-flex px-2.5 py-0.5 rounded-full text-[0.68rem] font-semibold border"
            :class="ORDER_STATUS_STYLES[store.currentOrder.status] ?? 'text-text-muted bg-white/[0.04] border-border'"
          >
            {{ store.currentOrder.status }}
          </span>
          <span class="text-[0.78rem] text-text-muted">{{ formatDate(store.currentOrder.createdAt) }}</span>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-2">
          <button
            v-if="store.currentOrder.status === 'Pending'"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[0.78rem] font-medium border border-status-green/30 text-status-green bg-status-green/6 hover:bg-status-green/12 transition-colors"
            @click="handleAccept"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12" />
            </svg>
            Accept
          </button>
          <button
            v-if="store.currentOrder.status === 'Pending'"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[0.78rem] font-medium border border-status-red/30 text-status-red bg-status-red/6 hover:bg-status-red/12 transition-colors"
            @click="handleCancel"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
            </svg>
            Cancel
          </button>
          <button
            v-if="store.currentOrder.status === 'Pending' || store.currentOrder.status === 'Cancelled'"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[0.78rem] font-medium border border-border text-text-muted hover:text-status-red hover:border-status-red/30 hover:bg-status-red/6 transition-colors"
            @click="showDeleteConfirm = true"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
            </svg>
            Delete
          </button>
        </div>
      </div>

      <!-- Info cards grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">

        <!-- Order Info card -->
        <div class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.78rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Order Info</h2>
          <dl class="space-y-3">
            <div class="flex items-center justify-between">
              <dt class="text-[0.78rem] text-text-muted">Client</dt>
              <dd>
                <RouterLink
                  :to="`/clients/${store.currentOrder.clientId}`"
                  class="text-[0.82rem] text-primary-400 hover:text-primary-300 transition-colors"
                >
                  {{ store.currentOrder.clientName }}
                </RouterLink>
              </dd>
            </div>
            <div class="flex items-center justify-between">
              <dt class="text-[0.78rem] text-text-muted">Payment Method</dt>
              <dd class="text-[0.82rem] text-text-secondary">{{ store.currentOrder.paymentMethod }}</dd>
            </div>
            <div class="flex items-center justify-between">
              <dt class="text-[0.78rem] text-text-muted">IP Address</dt>
              <dd class="text-[0.82rem] text-text-secondary font-mono">{{ store.currentOrder.ipAddress ?? '\u2014' }}</dd>
            </div>
            <div class="flex items-center justify-between">
              <dt class="text-[0.78rem] text-text-muted">Created</dt>
              <dd class="text-[0.82rem] text-text-secondary">{{ formatDate(store.currentOrder.createdAt) }}</dd>
            </div>
          </dl>
        </div>

        <!-- Invoice card -->
        <div class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.78rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Invoice</h2>
          <template v-if="store.currentOrder.invoiceId">
            <dl class="space-y-3">
              <div class="flex items-center justify-between">
                <dt class="text-[0.78rem] text-text-muted">Invoice ID</dt>
                <dd>
                  <RouterLink
                    :to="`/billing/${store.currentOrder.invoiceId}`"
                    class="text-[0.82rem] text-primary-400 hover:text-primary-300 transition-colors"
                  >
                    #{{ store.currentOrder.invoiceId }}
                  </RouterLink>
                </dd>
              </div>
              <div class="flex items-center justify-between">
                <dt class="text-[0.78rem] text-text-muted">Status</dt>
                <dd>
                  <span
                    v-if="store.currentOrder.invoiceStatus"
                    class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border"
                    :class="INVOICE_STATUS_STYLES[store.currentOrder.invoiceStatus] ?? 'text-text-muted bg-white/[0.04] border-border'"
                  >
                    {{ store.currentOrder.invoiceStatus }}
                  </span>
                  <span v-else class="text-[0.82rem] text-text-muted">&mdash;</span>
                </dd>
              </div>
              <div class="flex items-center justify-between">
                <dt class="text-[0.78rem] text-text-muted">Total</dt>
                <dd class="text-[0.82rem] font-medium text-text-primary">{{ formatCurrency(store.currentOrder.total) }}</dd>
              </div>
            </dl>
          </template>
          <p v-else class="text-[0.82rem] text-text-muted">No invoice linked to this order.</p>
        </div>
      </div>

      <!-- Items table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-6">
        <div class="px-5 py-3 border-b border-border bg-white/[0.02]">
          <h2 class="text-[0.78rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Order Items</h2>
        </div>

        <!-- Header -->
        <div class="hidden sm:grid grid-cols-[2fr_0.8fr_1.2fr_1fr_1fr_0.6fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Billing Cycle</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">First Payment</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Recurring</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        </div>

        <!-- Rows -->
        <div
          v-for="item in store.currentOrder.items"
          :key="item.id"
          class="grid grid-cols-1 sm:grid-cols-[2fr_0.8fr_1.2fr_1fr_1fr_0.6fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] font-medium text-text-primary">{{ item.productName }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ capitalizeCycle(item.billingCycle) }}</span>
          <span class="text-[0.82rem] text-text-secondary truncate">{{ item.domain ?? item.hostname ?? '\u2014' }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ formatCurrency(item.firstPaymentAmount) }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ formatCurrency(item.recurringAmount) }}</span>
          <span
            class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
            :class="itemStatusStyles[item.status] ?? 'text-text-muted bg-white/[0.04] border-border'"
          >
            {{ item.status }}
          </span>
        </div>
      </div>

      <!-- Notes -->
      <div class="bg-surface-card border border-border rounded-2xl p-5">
        <h2 class="text-[0.78rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-3">Admin Notes</h2>
        <textarea
          v-model="notesText"
          rows="4"
          class="w-full rounded-lg border border-border bg-surface-panel px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted outline-none focus:border-primary-500/40 transition-colors resize-none"
          placeholder="Add notes about this order..."
        />
        <p class="text-[0.68rem] text-text-muted mt-1.5">Notes are only visible to admins.</p>
      </div>
    </template>

    <!-- Delete confirmation overlay -->
    <Teleport to="body">
      <div v-if="showDeleteConfirm" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
        <div class="bg-surface-card border border-border rounded-2xl p-6 max-w-sm w-full mx-4">
          <h3 class="text-[1rem] font-semibold text-text-primary mb-2">Delete Order</h3>
          <p class="text-[0.82rem] text-text-secondary mb-5">Are you sure you want to permanently delete this order? This action cannot be undone.</p>
          <div class="flex items-center justify-end gap-2">
            <button
              class="px-3 py-1.5 rounded-lg text-[0.78rem] font-medium border border-border text-text-secondary hover:text-text-primary hover:bg-white/[0.04] transition-colors"
              @click="showDeleteConfirm = false"
            >
              Cancel
            </button>
            <button
              class="px-3 py-1.5 rounded-lg text-[0.78rem] font-medium border border-status-red/30 text-white bg-status-red hover:bg-status-red/80 transition-colors"
              @click="handleDelete"
            >
              Delete
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
