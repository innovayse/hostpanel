<script setup lang="ts">
/**
 * Modal for editing one product's specification — the lines the storefront's
 * plan comparison table is built from.
 *
 * Each line is saved on its own, matching the API: there is no bulk endpoint,
 * and a failure part-way through a list of edits should leave the lines that
 * did save rather than roll the whole dialog back to an unknown state.
 *
 * Rows line up across plans by label, so the label of an existing line is what
 * an operator has to keep consistent between products. The hint below the list
 * says so, because nothing in the UI would otherwise reveal it.
 */
import { onMounted, ref } from 'vue'
import { useApi } from '@/composables/useApi'
import type { Product } from '@/types/models'

/** One specification line as the API returns it. */
interface ProductFeature {
  id: number
  productId: number
  label: string
  value: string
  sortOrder: number
}

const props = defineProps<{
  /** Product whose specification is being edited. */
  product: Product
}>()

const emit = defineEmits<{ close: [] }>()

const { request } = useApi()

const features = ref<ProductFeature[]>([])
const loading = ref(true)
const error = ref('')
const busyId = ref<number | null>(null)

const newLabel = ref('')
const newValue = ref('')
const adding = ref(false)

/** Loads the product's lines, newest state from the server. */
async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    features.value = await request<ProductFeature[]>(
      `/product-features?productId=${props.product.id}`)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Could not load the specification.'
  } finally {
    loading.value = false
  }
}

onMounted(load)

/** Adds a line at the end of the list. */
async function add(): Promise<void> {
  if (!newLabel.value.trim() || !newValue.value.trim()) return

  adding.value = true
  error.value = ''
  try {
    await request('/product-features', {
      method: 'POST',
      body: JSON.stringify({
        productId: props.product.id,
        label: newLabel.value.trim(),
        value: newValue.value.trim(),
        sortOrder: features.value.length,
      }),
    })
    newLabel.value = ''
    newValue.value = ''
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Could not add the line.'
  } finally {
    adding.value = false
  }
}

/**
 * Saves an edited line.
 *
 * @param feature - The line to save, with the operator's current text.
 */
async function save(feature: ProductFeature): Promise<void> {
  if (!feature.label.trim() || !feature.value.trim()) return

  busyId.value = feature.id
  error.value = ''
  try {
    await request(`/product-features/${feature.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        id: feature.id,
        label: feature.label.trim(),
        value: feature.value.trim(),
        sortOrder: feature.sortOrder,
      }),
    })
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Could not save the line.'
    await load()
  } finally {
    busyId.value = null
  }
}

/**
 * Removes a line.
 *
 * @param feature - The line to remove.
 */
async function remove(feature: ProductFeature): Promise<void> {
  busyId.value = feature.id
  error.value = ''
  try {
    await request(`/product-features/${feature.id}`, { method: 'DELETE' })
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Could not remove the line.'
  } finally {
    busyId.value = null
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" @click.self="emit('close')">
    <div class="w-full max-w-2xl rounded-2xl border border-border bg-surface-card p-6">
      <div class="mb-5 flex items-start justify-between gap-4">
        <div>
          <h2 class="font-display text-lg font-bold text-text-primary">Specification</h2>
          <p class="mt-1 text-sm text-text-secondary">
            Shown in the storefront's plan comparison table for
            <span class="text-text-primary">{{ product.name }}</span>.
          </p>
        </div>
        <button class="p-1 text-text-muted hover:text-text-primary" title="Close" @click="emit('close')">
          <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        </button>
      </div>

      <div v-if="error" class="mb-4 rounded-xl border border-status-red/20 bg-status-red/10 p-3 text-sm text-status-red">
        {{ error }}
      </div>

      <div v-if="loading" class="text-sm text-text-secondary">Loading...</div>

      <template v-else>
        <div v-if="features.length === 0" class="rounded-xl border border-border bg-surface-elevated p-4 text-sm text-text-muted">
          Nothing specified yet. The comparison table stays hidden until at least one plan has a line.
        </div>

        <div v-else class="flex flex-col gap-2">
          <div v-for="feature in features" :key="feature.id" class="flex items-center gap-2">
            <input
              v-model="feature.label"
              type="text"
              placeholder="Disk"
              class="w-1/3 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
              @keyup.enter="save(feature)"
            >
            <input
              v-model="feature.value"
              type="text"
              placeholder="10 GB"
              class="flex-1 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
              @keyup.enter="save(feature)"
            >
            <button
              type="button"
              :disabled="busyId === feature.id"
              class="shrink-0 rounded-lg bg-text-primary px-3 py-1.5 text-sm font-medium text-surface-card disabled:opacity-50"
              @click="save(feature)"
            >
              Save
            </button>
            <button
              type="button"
              :disabled="busyId === feature.id"
              class="shrink-0 p-1 text-text-muted hover:text-status-red disabled:opacity-50"
              title="Remove line"
              @click="remove(feature)"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M3 6h18M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
              </svg>
            </button>
          </div>
        </div>

        <div class="mt-4 flex items-center gap-2 border-t border-border pt-4">
          <input
            v-model="newLabel"
            type="text"
            placeholder="Feature"
            class="w-1/3 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
            @keyup.enter="add"
          >
          <input
            v-model="newValue"
            type="text"
            placeholder="Value"
            class="flex-1 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm text-text-primary focus:border-text-secondary focus:outline-none"
            @keyup.enter="add"
          >
          <button
            type="button"
            :disabled="adding || !newLabel.trim() || !newValue.trim()"
            class="shrink-0 rounded-lg bg-text-primary px-3 py-1.5 text-sm font-medium text-surface-card disabled:opacity-50"
            @click="add"
          >
            {{ adding ? 'Adding…' : 'Add' }}
          </button>
        </div>

        <p class="mt-3 text-xs text-text-muted">
          Plans line up by feature name, so spell it the same way across products — "Disk" and
          "Disk space" become two separate rows.
        </p>
      </template>
    </div>
  </div>
</template>
