<script setup lang="ts">
/**
 * Products settings view — list and manage all products/plans.
 *
 * Displays products in a dark-themed table with create/edit capabilities.
 * Fetches products and groups on mount.
 */
import { computed, onMounted, ref } from 'vue'
import { useSettingsStore } from '../stores/settingsStore'
import ProductFormModal from '../components/ProductFormModal.vue'
import type { Product, CreateProductPayload } from '@/types/models'

const store = useSettingsStore()

/** Whether the product form modal is visible. */
const showModal = ref(false)

/** Product being edited, or null for create mode. */
const editingProduct = ref<Product | null>(null)

/** True while saving a product. */
const saving = ref(false)

onMounted(async () => {
  await Promise.all([
    store.fetchProducts(),
    store.fetchProductGroups(),
  ])
})

/** Product type display labels. */
const typeLabels: Record<string, string> = {
  SharedHosting: 'Shared Hosting',
  Vps: 'VPS',
  Dedicated: 'Dedicated',
  Domain: 'Domain',
  Ssl: 'SSL',
  Other: 'Other',
}

/**
 * Resolves the group name for a product.
 *
 * @param groupId - Product group ID.
 * @returns Group name or "—" if not found.
 */
function groupName(groupId: number): string {
  return store.productGroups.find(g => g.id === groupId)?.name ?? '—'
}

/**
 * Opens the modal in create mode.
 */
function openCreate(): void {
  editingProduct.value = null
  showModal.value = true
}

/**
 * Opens the modal in edit mode for the given product.
 *
 * @param product - The product to edit.
 */
function openEdit(product: Product): void {
  editingProduct.value = product
  showModal.value = true
}

/**
 * Handles the save event from the modal.
 *
 * @param payload - The product data to save.
 */
async function handleSave(payload: CreateProductPayload): Promise<void> {
  saving.value = true
  try {
    if (editingProduct.value) {
      await store.updateProduct(editingProduct.value.id, {
        name: payload.name,
        description: payload.description,
        website: payload.website,
        slug: payload.slug,
        monthlyPrice: payload.monthlyPrice,
        annualPrice: payload.annualPrice,
      })
    } else {
      await store.createProduct(payload)
    }
    showModal.value = false
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-7">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          Products
        </h1>
        <p class="text-sm text-text-secondary">Manage hosting plans and services available for purchase</p>
      </div>
      <button
        class="gradient-brand text-white rounded-[10px] px-5 py-2.5 text-[0.85rem] font-semibold transition-all duration-150 hover:-translate-y-px"
        style="box-shadow: 0 3px 14px rgba(14,165,233,0.2);"
        @click="openCreate"
      >
        Create Product
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading products...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Products table -->
    <div v-else class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border">
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Name</th>
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Group</th>
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Type</th>
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Monthly</th>
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Annual</th>
            <th class="px-5 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Status</th>
            <th class="px-5 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-border">
          <tr
            v-for="product in store.products"
            :key="product.id"
            class="hover:bg-white/[0.02] transition-colors"
          >
            <td class="px-5 py-3.5">
              <div class="font-medium text-text-primary">{{ product.name }}</div>
              <div v-if="product.slug" class="text-[0.72rem] text-text-muted mt-0.5">/{{ product.slug }}</div>
            </td>
            <td class="px-5 py-3.5 text-text-secondary">{{ groupName(product.groupId) }}</td>
            <td class="px-5 py-3.5 text-text-secondary">{{ typeLabels[product.type] ?? product.type }}</td>
            <td class="px-5 py-3.5 text-text-primary font-mono">${{ product.pricing.monthly.toFixed(2) }}</td>
            <td class="px-5 py-3.5 text-text-primary font-mono">${{ product.pricing.annual.toFixed(2) }}</td>
            <td class="px-5 py-3.5">
              <span
                class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                :class="product.status === 'Active'
                  ? 'text-status-green bg-status-green/10 border border-status-green/20'
                  : 'text-text-muted bg-white/[0.04] border border-border'"
              >
                {{ product.status }}
              </span>
            </td>
            <td class="px-5 py-3.5 text-right">
              <button
                class="text-text-muted hover:text-primary-400 transition-colors p-1"
                title="Edit product"
                @click="openEdit(product)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                </svg>
              </button>
            </td>
          </tr>
          <tr v-if="store.products.length === 0">
            <td colspan="7" class="px-5 py-8 text-center text-text-muted">
              No products found. Create your first product to get started.
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Product form modal -->
    <ProductFormModal
      v-if="showModal"
      :product="editingProduct"
      :groups="store.productGroups"
      :saving="saving"
      @save="handleSave"
      @close="showModal = false"
    />

  </div>
</template>
