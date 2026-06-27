<script setup lang="ts">
/**
 * Full-page form for creating or editing a TLD pricing configuration.
 *
 * Detects edit mode from route params, loads TLD config data when editing,
 * and includes pricing tables for registration, transfer, and renewal
 * across multiple year periods. Uses AppSelect, AppSpinner, and ToggleSwitch
 * reusable components instead of raw HTML inputs.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import { useTldConfigsStore } from '../stores/tldConfigsStore'
import AppSelect from '@/components/AppSelect.vue'
import AppSpinner from '@/components/AppSpinner.vue'
import ToggleSwitch from '@/components/ToggleSwitch.vue'
import type { CreateTldConfigPayload, UpdateTldConfigPayload } from '@/types/models'

const route = useRoute()
const router = useRouter()
const store = useTldConfigsStore()

/** Available registration periods in years. */
const PERIODS = ['1', '2', '3', '5', '10'] as const

/** Provider options for the registrar module dropdown. */
const PROVIDER_OPTIONS = [
  { label: 'Name.am', value: 'NameAm' },
  { label: 'Namecheap', value: 'Namecheap' },
]

/** Currency options for cost and sell currency dropdowns. */
const CURRENCY_OPTIONS = [
  { label: 'AMD (\u058F)', value: 'AMD' },
  { label: 'USD ($)', value: 'USD' },
  { label: 'EUR (\u20AC)', value: 'EUR' },
  { label: 'GBP (\u00A3)', value: 'GBP' },
  { label: 'RUB (\u20BD)', value: 'RUB' },
]

/** Whether we are in edit mode (TLD config ID present in route). */
const isEditMode = computed(() => !!route.params.id)

/** Page title based on create/edit mode. */
const pageTitle = computed(() => isEditMode.value ? 'Edit TLD' : 'Add TLD')

/** True while loading existing config data in edit mode. */
const loadingConfig = ref(false)

/** True while saving the form. */
const saving = ref(false)

/** Error message from save or load operations. */
const saveError = ref<string | null>(null)

/** Tracks whether the form has been modified since loading. */
const isDirty = ref(false)

// -- Form fields --

/** TLD input value (e.g. "com"). */
const tld = ref('')

/** Selected registrar module. */
const registrarModule = ref('NameAm')

/** Cost currency code. */
const currency = ref('AMD')

/** Sell currency code. */
const sellCurrency = ref('USD')

/** Whether this TLD is enabled for sale. */
const isEnabled = ref(true)

/** Display sort order. */
const sortOrder = ref(0)

/** Categories as comma-separated string input. */
const categoriesInput = ref('')

/** Registration cost prices keyed by period string. */
const costRegister = ref<Record<string, number>>({})

/** Transfer cost prices keyed by period. */
const costTransfer = ref<Record<string, number>>({})

/** Renewal cost prices keyed by period. */
const costRenew = ref<Record<string, number>>({})

/** Registration sell prices keyed by period. */
const sellRegister = ref<Record<string, number>>({})

/** Transfer sell prices keyed by period. */
const sellTransfer = ref<Record<string, number>>({})

/** Renewal sell prices keyed by period. */
const sellRenew = ref<Record<string, number>>({})

/**
 * Initializes empty price fields for all periods with zero values.
 *
 * @returns Record of zero values keyed by period.
 */
function emptyPriceMap(): Record<string, number> {
  return Object.fromEntries(PERIODS.map(p => [p, 0]))
}

// Initialize all price maps
costRegister.value = emptyPriceMap()
costTransfer.value = emptyPriceMap()
costRenew.value = emptyPriceMap()
sellRegister.value = emptyPriceMap()
sellTransfer.value = emptyPriceMap()
sellRenew.value = emptyPriceMap()

/**
 * Calculates margin percentage between cost and sell.
 *
 * @param cost - Cost price, or undefined if the period has no value.
 * @param sell - Sell price, or undefined if the period has no value.
 * @returns Margin percentage string, or em dash if not calculable.
 */
function calcMargin(cost: number | undefined, sell: number | undefined): string {
  if (!cost || !sell || cost === 0) return '\u2014'
  const margin = ((sell - cost) / cost * 100).toFixed(1)
  return `${parseFloat(margin) >= 0 ? '+' : ''}${margin}%`
}

/**
 * Returns CSS class for margin display color.
 *
 * @param cost - Cost price, or undefined if the period has no value.
 * @param sell - Sell price, or undefined if the period has no value.
 * @returns Tailwind color class.
 */
function marginColorClass(cost: number | undefined, sell: number | undefined): string {
  if (!cost || !sell || cost === 0) return 'text-text-muted'
  return sell >= cost ? 'text-status-green' : 'text-status-red'
}

/**
 * Converts a numeric price map to a record with only non-zero entries.
 *
 * @param map - Numeric price map.
 * @returns Record with only non-zero entries.
 */
function toNonZeroMap(map: Record<string, number>): Record<string, number> {
  const result: Record<string, number> = {}
  for (const [k, v] of Object.entries(map)) {
    if (v && v > 0) result[k] = v
  }
  return result
}

/**
 * Converts an API numeric price map to local form map, filling in zeros for missing periods.
 *
 * @param map - Numeric price map from the API.
 * @returns Full price map with all periods filled.
 */
function fromApiMap(map: Record<string, number>): Record<string, number> {
  const base = emptyPriceMap()
  for (const [k, v] of Object.entries(map)) {
    base[k] = v
  }
  return base
}

// -- Mark form dirty on any change --

watch(
  [tld, registrarModule, currency, sellCurrency, isEnabled, sortOrder, categoriesInput,
    costRegister, costTransfer, costRenew, sellRegister, sellTransfer, sellRenew],
  () => { isDirty.value = true },
  { deep: true },
)

// -- Unsaved changes guard --

onBeforeRouteLeave((_to, _from, next) => {
  if (isDirty.value && !saving.value) {
    const leave = confirm('You have unsaved changes. Are you sure you want to leave?')
    next(leave)
  } else {
    next()
  }
})

// -- Load data on mount --

onMounted(async () => {
  if (isEditMode.value) {
    loadingConfig.value = true
    try {
      const id = Number(route.params.id)
      const config = await store.fetchById(id)
      if (!config) {
        saveError.value = `TLD config #${id} not found.`
        return
      }

      tld.value = config.tld
      registrarModule.value = config.registrarModule
      currency.value = config.currency
      sellCurrency.value = config.sellCurrency
      isEnabled.value = config.isEnabled
      sortOrder.value = config.sortOrder
      categoriesInput.value = config.categories.join(', ')

      costRegister.value = fromApiMap(config.costRegister)
      costTransfer.value = fromApiMap(config.costTransfer)
      costRenew.value = fromApiMap(config.costRenew)
      sellRegister.value = fromApiMap(config.sellRegister)
      sellTransfer.value = fromApiMap(config.sellTransfer)
      sellRenew.value = fromApiMap(config.sellRenew)

      // Reset dirty flag after populating form
      isDirty.value = false
    } finally {
      loadingConfig.value = false
    }
  }
})

// -- Save handler --

/**
 * Collects form data and dispatches create or update via the store.
 * Navigates back to the TLD list on success.
 */
async function handleSave(): Promise<void> {
  if (!tld.value.trim()) {
    saveError.value = 'TLD is required.'
    return
  }

  saving.value = true
  saveError.value = null

  try {
    const categories = categoriesInput.value
      .split(',')
      .map(c => c.trim())
      .filter(c => c.length > 0)

    const payload: CreateTldConfigPayload = {
      tld: tld.value.toLowerCase().trim(),
      registrarModule: registrarModule.value,
      currency: currency.value,
      sellCurrency: sellCurrency.value,
      isEnabled: isEnabled.value,
      sortOrder: sortOrder.value,
      categories,
      costRegister: toNonZeroMap(costRegister.value),
      costTransfer: toNonZeroMap(costTransfer.value),
      costRenew: toNonZeroMap(costRenew.value),
      sellRegister: toNonZeroMap(sellRegister.value),
      sellTransfer: toNonZeroMap(sellTransfer.value),
      sellRenew: toNonZeroMap(sellRenew.value),
    }

    if (isEditMode.value) {
      const id = Number(route.params.id)
      await store.update(id, { ...payload, id } as UpdateTldConfigPayload)
    } else {
      await store.create(payload)
    }

    isDirty.value = false
    router.push('/settings/tld-pricing')
  } catch (e) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save TLD configuration.'
  } finally {
    saving.value = false
  }
}

/**
 * Navigates back to the TLD pricing list.
 * The unsaved changes guard will handle confirmation if needed.
 */
function handleCancel(): void {
  router.push('/settings/tld-pricing')
}

/** Currency symbol for the cost currency. */
const costSymbol = computed(() => {
  const symbols: Record<string, string> = { USD: '$', EUR: '\u20AC', GBP: '\u00A3', AMD: '\u058F', RUB: '\u20BD' }
  return symbols[currency.value] ?? ''
})

/** Currency symbol for the sell currency. */
const sellSymbol = computed(() => {
  const symbols: Record<string, string> = { USD: '$', EUR: '\u20AC', GBP: '\u00A3', AMD: '\u058F', RUB: '\u20BD' }
  return symbols[sellCurrency.value] ?? ''
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Breadcrumb -->
    <nav class="flex items-center gap-1.5 text-sm mb-6">
      <router-link to="/settings" class="text-text-muted hover:text-primary-400 transition-colors">Settings</router-link>
      <svg class="w-3.5 h-3.5 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6" /></svg>
      <router-link to="/settings/tld-pricing" class="text-text-muted hover:text-primary-400 transition-colors">TLD Pricing</router-link>
      <svg class="w-3.5 h-3.5 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6" /></svg>
      <span class="text-text-primary font-medium">{{ pageTitle }}</span>
    </nav>

    <!-- Loading spinner for edit mode -->
    <div v-if="loadingConfig" class="flex items-center gap-3 text-text-secondary text-sm py-12">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading TLD configuration...
    </div>

    <template v-else>

      <!-- Save error -->
      <div
        v-if="saveError"
        class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5"
      >
        {{ saveError }}
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-5 mb-24">

        <!-- Left: General Settings -->
        <div class="bg-surface-card border border-border rounded-xl p-6">
          <h2 class="text-lg font-semibold text-text-primary mb-4">General Settings</h2>

          <div class="flex flex-col gap-4">

            <!-- TLD -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">TLD</label>
              <div class="flex items-center gap-2">
                <span class="text-lg font-semibold text-text-muted">.</span>
                <input
                  v-model="tld"
                  type="text"
                  placeholder="com"
                  :disabled="isEditMode"
                  class="flex-1 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors lowercase disabled:opacity-50 disabled:cursor-not-allowed"
                  @input="tld = tld.toLowerCase().replace(/[^a-z0-9-]/g, '')"
                />
              </div>
              <p class="text-[0.7rem] text-text-muted mt-1">Domain extension without the dot (e.g. "com", "net", "am")</p>
            </div>

            <!-- Provider -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Registrar Provider</label>
              <AppSelect
                :model-value="registrarModule"
                :options="PROVIDER_OPTIONS"
                @update:model-value="registrarModule = $event"
              />
            </div>

            <!-- Cost Currency -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Cost Currency</label>
              <AppSelect
                :model-value="currency"
                :options="CURRENCY_OPTIONS"
                @update:model-value="currency = $event"
              />
              <p class="text-[0.7rem] text-text-muted mt-1">Currency used by the registrar for cost prices</p>
            </div>

            <!-- Sell Currency -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Sell Currency</label>
              <AppSelect
                :model-value="sellCurrency"
                :options="CURRENCY_OPTIONS"
                @update:model-value="sellCurrency = $event"
              />
              <p class="text-[0.7rem] text-text-muted mt-1">Currency shown to clients for sell prices</p>
            </div>

            <!-- Enabled Toggle -->
            <div class="flex items-center justify-between py-1">
              <div>
                <p class="text-sm text-text-primary font-medium">Enabled</p>
                <p class="text-xs text-text-muted">Make this TLD available for purchase</p>
              </div>
              <ToggleSwitch v-model="isEnabled" />
            </div>

            <!-- Sort Order -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Sort Order</label>
              <AppSpinner
                :model-value="sortOrder"
                :min="0"
                :step="1"
                placeholder="0"
                @update:model-value="sortOrder = $event"
              />
            </div>

            <!-- Categories -->
            <div>
              <label class="block text-sm text-text-secondary mb-1.5">Categories</label>
              <input
                v-model="categoriesInput"
                type="text"
                placeholder="popular, business, tech"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
              <p class="text-[0.7rem] text-text-muted mt-1">Comma-separated category tags for filtering</p>
            </div>
          </div>
        </div>

        <!-- Right: Pricing -->
        <div class="flex flex-col gap-5">

          <!-- Registration Pricing -->
          <div class="bg-surface-card border border-border rounded-xl p-6">
            <h2 class="text-lg font-semibold text-text-primary mb-4">Registration Pricing</h2>
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-border">
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Period</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Cost ({{ costSymbol }})</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Sell ({{ sellSymbol }})</th>
                  <th class="pb-2.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Margin</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border/50">
                <tr v-for="period in PERIODS" :key="period">
                  <td class="py-2.5 text-text-secondary">{{ period }} {{ Number(period) === 1 ? 'Year' : 'Years' }}</td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="costRegister[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="costRegister[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="sellRegister[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="sellRegister[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 text-right tabular-nums">
                    <span
                      class="text-xs font-medium"
                      :class="marginColorClass(costRegister[period], sellRegister[period])"
                    >
                      {{ calcMargin(costRegister[period], sellRegister[period]) }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Transfer Pricing -->
          <div class="bg-surface-card border border-border rounded-xl p-6">
            <h2 class="text-lg font-semibold text-text-primary mb-4">Transfer Pricing</h2>
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-border">
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Period</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Cost ({{ costSymbol }})</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Sell ({{ sellSymbol }})</th>
                  <th class="pb-2.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Margin</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border/50">
                <tr v-for="period in PERIODS" :key="period">
                  <td class="py-2.5 text-text-secondary">{{ period }} {{ Number(period) === 1 ? 'Year' : 'Years' }}</td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="costTransfer[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="costTransfer[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="sellTransfer[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="sellTransfer[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 text-right tabular-nums">
                    <span
                      class="text-xs font-medium"
                      :class="marginColorClass(costTransfer[period], sellTransfer[period])"
                    >
                      {{ calcMargin(costTransfer[period], sellTransfer[period]) }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Renewal Pricing -->
          <div class="bg-surface-card border border-border rounded-xl p-6">
            <h2 class="text-lg font-semibold text-text-primary mb-4">Renewal Pricing</h2>
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-border">
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Period</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Cost ({{ costSymbol }})</th>
                  <th class="pb-2.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Sell ({{ sellSymbol }})</th>
                  <th class="pb-2.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Margin</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border/50">
                <tr v-for="period in PERIODS" :key="period">
                  <td class="py-2.5 text-text-secondary">{{ period }} {{ Number(period) === 1 ? 'Year' : 'Years' }}</td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="costRenew[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="costRenew[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 pr-2">
                    <AppSpinner
                      :model-value="sellRenew[period]"
                      :step="0.01"
                      :min="0"
                      placeholder="0.00"
                      @update:model-value="sellRenew[period] = $event"
                    />
                  </td>
                  <td class="py-2.5 text-right tabular-nums">
                    <span
                      class="text-xs font-medium"
                      :class="marginColorClass(costRenew[period], sellRenew[period])"
                    >
                      {{ calcMargin(costRenew[period], sellRenew[period]) }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

        </div>
      </div>

      <!-- Sticky Footer -->
      <div class="fixed bottom-0 left-0 right-0 bg-surface-card/95 backdrop-blur-sm border-t border-border px-6 py-4 z-30">
        <div class="flex items-center justify-end gap-3">
          <button
            type="button"
            class="px-5 py-2.5 text-sm font-medium text-text-secondary hover:text-text-primary bg-transparent border border-border rounded-[10px] transition-colors"
            @click="handleCancel"
          >
            Cancel
          </button>
          <button
            type="button"
            :disabled="saving"
            class="gradient-brand px-6 py-2.5 text-sm font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="handleSave"
          >
            {{ saving ? 'Saving...' : isEditMode ? 'Save Changes' : 'Create TLD' }}
          </button>
        </div>
      </div>

    </template>

  </div>
</template>
