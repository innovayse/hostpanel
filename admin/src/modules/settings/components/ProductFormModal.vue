<script setup lang="ts">
/**
 * Modal dialog for creating or editing a product.
 *
 * Displays product type cards, group selector, pricing fields, and a hidden toggle.
 * Emits `save` with the payload on submit, and `close` on cancel.
 */
import { ref, watch, computed, onMounted } from 'vue'
import type { Product, ProductGroup, CreateProductPayload } from '@/types/models'
import type { ServerGroupDto } from '@/modules/servers/types/server.types'
import { useApi } from '@/composables/useApi'
import ToggleSwitch from '@/components/ToggleSwitch.vue'
import AppSelect from '@/components/AppSelect.vue'
import AppNumberInput from '@/components/AppNumberInput.vue'

const { request } = useApi()

/** Props for ProductFormModal. */
const props = defineProps<{
  /** Product to edit, or null when creating a new product. */
  product: Product | null
  /** Available product groups for the group dropdown. */
  groups: ProductGroup[]
  /** True while the save request is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form. */
  save: [payload: CreateProductPayload]
  /** Emitted when the user closes or cancels the modal. */
  close: []
}>()

/** Whether the modal is in edit mode (product was provided). */
const isEditMode = computed(() => props.product !== null)

/** Available product types for the type-selector cards. */
const productTypes = [
  { value: 'SharedHosting', label: 'Shared Hosting', icon: 'rack' },
  { value: 'Vps', label: 'VPS', icon: 'cloud' },
  { value: 'Dedicated', label: 'Dedicated', icon: 'server' },
  { value: 'Other', label: 'Other', icon: 'box' },
] as const

/** Selected product type. */
const type = ref('SharedHosting')

/** Selected product group ID. */
const groupId = ref(0)

/** Product name input value. */
const name = ref('')

/** URL slug input value. */
const slug = ref('')

/** Hosting package name input value. */
const packageName = ref('')

/** Whether the user has manually edited the slug field. */
const slugManuallyEdited = ref(false)

/** Product description textarea value. */
const description = ref('')

/** Monthly price input value. */
const monthlyPrice = ref(0)

/** Annual price input value. */
const annualPrice = ref(0)

/** Selected server group ID, or null for no group. */
const serverGroupId = ref<number | null>(null)

/** Available server groups fetched from the API. */
const serverGroups = ref<ServerGroupDto[]>([])

/** Whether the product is hidden (maps to Inactive status). */
const isHidden = ref(false)

/**
 * Fetches available server groups from the API on mount.
 */
onMounted(async () => {
  try {
    serverGroups.value = await request<ServerGroupDto[]>('/admin/server-groups')
  } catch {
    /* silent — dropdown will simply be empty */
  }
})

/**
 * Initializes or resets form fields based on the product prop.
 *
 * When editing, populates all fields from the existing product.
 * When creating, resets to default empty values.
 */
watch(() => props.product, (p) => {
  if (p) {
    type.value = p.type
    groupId.value = p.groupId
    name.value = p.name
    slug.value = p.slug ?? ''
    slugManuallyEdited.value = true
    packageName.value = p.packageName ?? ''
    description.value = p.description ?? ''
    monthlyPrice.value = p.pricing.monthly
    annualPrice.value = p.pricing.annual
    serverGroupId.value = p.serverGroupId ?? null
    isHidden.value = p.status === 'Inactive'
  } else {
    type.value = 'SharedHosting'
    groupId.value = props.groups[0]?.id ?? 0
    name.value = ''
    slug.value = ''
    slugManuallyEdited.value = false
    packageName.value = ''
    description.value = ''
    monthlyPrice.value = 0
    annualPrice.value = 0
    serverGroupId.value = null
    isHidden.value = false
  }
}, { immediate: true })

/** Options formatted for the AppSelect component. */
const groupOptions = computed(() =>
  props.groups.map(g => ({ value: g.id, label: g.name }))
)

/** Options formatted for the server group AppSelect component, with a "None" option. */
const serverGroupOptions = computed(() => [
  { value: 0, label: 'None (auto-select by module)' },
  ...serverGroups.value.map(g => ({ value: g.id, label: g.name })),
])

/**
 * Writable computed that maps between nullable serverGroupId and the numeric v-model for AppSelect.
 * Uses 0 to represent "no server group" (null).
 */
const serverGroupModel = computed({
  get: () => serverGroupId.value ?? 0,
  set: (v: number) => { serverGroupId.value = v === 0 ? null : v },
})

/**
 * Auto-generates the slug from the product name.
 *
 * Only runs when the user has not manually edited the slug field.
 * Converts to lowercase, replaces spaces with hyphens, and strips non-alphanumeric characters.
 */
watch(name, (newName) => {
  if (!slugManuallyEdited.value) {
    slug.value = newName
      .toLowerCase()
      .replace(/\s+/g, '-')
      .replace(/[^a-z0-9-]/g, '')
  }
})

/**
 * Marks the slug as manually edited when the user types in the slug field.
 */
function handleSlugInput(): void {
  slugManuallyEdited.value = true
}

/**
 * Submits the form by emitting the save event with the current field values.
 */
function handleSubmit(): void {
  emit('save', {
    groupId: groupId.value,
    name: name.value,
    description: description.value || null,
    website: null,
    slug: slug.value || null,
    packageName: packageName.value || null,
    type: type.value,
    monthlyPrice: monthlyPrice.value,
    annualPrice: annualPrice.value,
    serverGroupId: serverGroupId.value || null,
  })
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <div class="relative bg-surface-card border border-border rounded-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto shadow-2xl">

      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border">
        <h2 class="font-display font-bold text-[1rem] text-text-primary">
          {{ isEditMode ? 'Edit Product' : 'Create New Product' }}
        </h2>
        <button
          class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="emit('close')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <form class="px-6 py-5 flex flex-col gap-5" @submit.prevent="handleSubmit">

        <!-- Product Type (create mode only) -->
        <div v-if="!isEditMode">
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Product Type</label>
          <div class="grid grid-cols-2 gap-2.5">
            <button
              v-for="pt in productTypes"
              :key="pt.value"
              type="button"
              class="flex items-center gap-3 px-4 py-3 rounded-xl border transition-colors text-left"
              :class="type === pt.value
                ? 'border-primary-500 bg-primary-500/[0.06]'
                : 'border-border bg-white/[0.02] hover:bg-white/[0.04]'"
              @click="type = pt.value"
            >
              <!-- Server rack icon -->
              <svg v-if="pt.icon === 'rack'" class="w-5 h-5 shrink-0" :class="type === pt.value ? 'text-primary-400' : 'text-text-muted'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <rect x="2" y="2" width="20" height="8" rx="2" />
                <rect x="2" y="14" width="20" height="8" rx="2" />
                <circle cx="17" cy="6" r="1" fill="currentColor" />
                <circle cx="17" cy="18" r="1" fill="currentColor" />
                <line x1="6" y1="6" x2="10" y2="6" />
                <line x1="6" y1="18" x2="10" y2="18" />
              </svg>
              <!-- Cloud icon -->
              <svg v-else-if="pt.icon === 'cloud'" class="w-5 h-5 shrink-0" :class="type === pt.value ? 'text-primary-400' : 'text-text-muted'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path d="M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z" />
              </svg>
              <!-- Server icon -->
              <svg v-else-if="pt.icon === 'server'" class="w-5 h-5 shrink-0" :class="type === pt.value ? 'text-primary-400' : 'text-text-muted'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <rect x="2" y="2" width="20" height="8" rx="2" />
                <rect x="2" y="14" width="20" height="8" rx="2" />
                <line x1="6" y1="6" x2="6.01" y2="6" />
                <line x1="6" y1="18" x2="6.01" y2="18" />
              </svg>
              <!-- Box icon -->
              <svg v-else class="w-5 h-5 shrink-0" :class="type === pt.value ? 'text-primary-400' : 'text-text-muted'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
                <line x1="12" y1="22.08" x2="12" y2="12" />
              </svg>
              <div>
                <p class="text-[0.82rem] font-medium" :class="type === pt.value ? 'text-text-primary' : 'text-text-secondary'">{{ pt.label }}</p>
              </div>
            </button>
          </div>
        </div>

        <!-- Product Group -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Product Group</label>
          <AppSelect
            v-model="groupId"
            :options="groupOptions"
            placeholder="Select a group"
          />
        </div>

        <!-- Product Name -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Product Name</label>
          <input
            v-model="name"
            required
            type="text"
            placeholder="e.g. Starter Hosting"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Slug -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Slug (URL)</label>
          <input
            v-model="slug"
            type="text"
            placeholder="e.g. starter-hosting"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            @input="handleSlugInput"
          />
          <p class="mt-1 text-[0.7rem] text-text-muted">Auto-generated from name. Edit to override.</p>
        </div>

        <!-- Package Name -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Package Name</label>
          <input
            v-model="packageName"
            type="text"
            placeholder="e.g. starter, pro, business"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
          <p class="mt-1 text-[0.7rem] text-text-muted">Hosting package name used for server provisioning. Optional.</p>
        </div>

        <!-- Server Group -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Server Group</label>
          <AppSelect
            v-model="serverGroupModel"
            :options="serverGroupOptions"
            placeholder="None (auto-select by module)"
          />
          <p class="mt-1 text-[0.7rem] text-text-muted">Assign to a server group for load-balanced provisioning. Optional.</p>
        </div>

        <!-- Description -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Description</label>
          <textarea
            v-model="description"
            rows="3"
            placeholder="Brief product description..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
          />
        </div>

        <!-- Pricing -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Pricing</label>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.68rem] text-text-muted mb-1">Monthly Price</label>
              <AppNumberInput
                v-model="monthlyPrice"
                :min="0"
                :step="0.01"
                placeholder="0.00"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] text-text-muted mb-1">Annual Price</label>
              <AppNumberInput
                v-model="annualPrice"
                :min="0"
                :step="0.01"
                placeholder="0.00"
              />
            </div>
          </div>
        </div>

        <!-- Hidden toggle -->
        <div class="flex items-center justify-between py-1">
          <div>
            <p class="text-[0.82rem] text-text-primary font-medium">Hidden</p>
            <p class="text-[0.7rem] text-text-muted">Hide this product from the order form</p>
          </div>
          <ToggleSwitch v-model="isHidden" />
        </div>

        <!-- Actions -->
        <div class="flex items-center justify-end gap-2.5 pt-1">
          <button
            type="button"
            class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            type="submit"
            :disabled="saving"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          >
            {{ saving ? 'Saving...' : isEditMode ? 'Save Changes' : 'Create Product' }}
          </button>
        </div>

      </form>
    </div>
  </div>
</template>
