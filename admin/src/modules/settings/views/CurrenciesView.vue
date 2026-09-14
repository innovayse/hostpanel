<script setup lang="ts">
/**
 * Admin Currencies settings view — table of configured currencies with rate,
 * base and enabled state, plus the bulk exchange-rate and product-price actions.
 *
 * Mirrors the shape of {@link ../views/TldConfigsView.vue}: a result banner for
 * async actions, a confirm modal for destructive/global ones, and per-row edit.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useCurrenciesStore } from '../stores/currenciesStore'
import CurrencyFormModal from '../components/CurrencyFormModal.vue'
import UiConfirmModal from '@/components/ui/UiConfirmModal.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import { apiErrorMessage } from '@/utils/apiErrorMessage'
import type { CurrencyDto, UpsertCurrencyPayload } from '@/types/currency'

const { t } = useI18n()
const store = useCurrenciesStore()

/** Whether the add/edit currency modal is visible. */
const showModal = ref(false)

/** Currency being edited, or null for create mode. */
const editingCurrency = ref<CurrencyDto | null>(null)

/** True while a currency save is in flight. */
const saving = ref(false)

/** The currency pending a "make base" confirmation, or null when the modal is closed. */
const makeBaseTarget = ref<CurrencyDto | null>(null)

/** True while a "make base" request is in flight. */
const makingBase = ref(false)

/** Whether the "update product prices" currency picker is open. */
const showPricesPicker = ref(false)

/** Whether the "update product prices" confirm step is open. */
const showPricesConfirm = ref(false)

/** Non-base currency codes selected for the product-price recompute. */
const selectedPriceCurrencies = ref<string[]>([])

/** True while exchange rates are being refreshed. */
const updatingRates = ref(false)

/** True while product prices are being recomputed. */
const updatingPrices = ref(false)

/** Result banner text from the last async action. */
const resultMessage = ref<string | null>(null)

/** Whether the result banner reflects a failure. */
const resultIsError = ref(false)

/** Non-base currencies, candidates for the product-price recompute picker. */
const nonBaseCurrencies = computed<CurrencyDto[]>(() => store.currencies.filter(c => !c.isBase))

/**
 * Shows a result banner for five seconds.
 *
 * @param message - Text to display.
 * @param isError - Whether the banner should render as an error.
 */
function showResult(message: string, isError: boolean): void {
  resultMessage.value = message
  resultIsError.value = isError
  setTimeout(() => { resultMessage.value = null }, 5000)
}

/**
 * Formats a sample amount with a currency's prefix, suffix and decimals.
 *
 * @param currency - The currency to format the sample for.
 * @returns The formatted sample string, e.g. "2.99 -> $2.99".
 */
function formatSample(currency: CurrencyDto): string {
  return `2.99 → ${currency.prefix}${(2.99).toFixed(currency.decimals)}${currency.suffix}`
}

/**
 * Opens the modal in create mode.
 */
function openCreate(): void {
  editingCurrency.value = null
  showModal.value = true
}

/**
 * Opens the modal in edit mode for the given currency.
 *
 * @param currency - The currency to edit.
 */
function openEdit(currency: CurrencyDto): void {
  editingCurrency.value = currency
  showModal.value = true
}

/**
 * Handles the save event from the currency form modal.
 *
 * @param code - The currency code being saved.
 * @param payload - The upsert payload.
 */
async function handleSave(code: string, payload: UpsertCurrencyPayload): Promise<void> {
  saving.value = true
  try {
    await store.upsert(code, payload)
    showModal.value = false
  } catch (e) {
    showResult(apiErrorMessage(e, t('currencies.toasts.saveFailed')), true)
  } finally {
    saving.value = false
  }
}

/**
 * Confirms and applies a "make base" switch for the targeted currency.
 */
async function confirmMakeBase(): Promise<void> {
  if (!makeBaseTarget.value) return
  makingBase.value = true
  try {
    await store.makeBase(makeBaseTarget.value.code)
    showResult(t('currencies.toasts.makeBaseSucceeded', { code: makeBaseTarget.value.code }), false)
  } catch (e) {
    showResult(apiErrorMessage(e, t('currencies.toasts.makeBaseFailed')), true)
  } finally {
    makingBase.value = false
    makeBaseTarget.value = null
  }
}

/**
 * Refreshes exchange rates from the source and reports which codes changed.
 */
async function handleUpdateRates(): Promise<void> {
  updatingRates.value = true
  try {
    const result = await store.updateRates()
    const updated = result.updated.length > 0 ? result.updated.join(', ') : 'none'
    const missing = result.missing.length > 0
      ? t('currencies.toasts.ratesMissing', { codes: result.missing.join(', ') })
      : ''
    showResult(t('currencies.toasts.ratesUpdated', { updated, missing }), false)
  } catch (e) {
    showResult(apiErrorMessage(e, t('currencies.toasts.ratesUpdateFailed')), true)
  } finally {
    updatingRates.value = false
  }
}

/**
 * Opens the currency picker for the product-price recompute.
 */
function openPricesPicker(): void {
  selectedPriceCurrencies.value = []
  showPricesPicker.value = true
}

/**
 * Advances from the picker to the overwrite confirmation.
 */
function confirmPricesPicker(): void {
  if (selectedPriceCurrencies.value.length === 0) return
  showPricesPicker.value = false
  showPricesConfirm.value = true
}

/**
 * Recomputes product prices in the selected currencies from the base prices.
 */
async function handleUpdatePrices(): Promise<void> {
  updatingPrices.value = true
  try {
    const written = await store.updateProductPrices(selectedPriceCurrencies.value)
    showResult(t('currencies.toasts.pricesUpdated', { count: written }), false)
  } catch (e) {
    showResult(apiErrorMessage(e, t('currencies.toasts.pricesUpdateFailed')), true)
  } finally {
    updatingPrices.value = false
    showPricesConfirm.value = false
    selectedPriceCurrencies.value = []
  }
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-7">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          {{ t('currencies.title') }}
        </h1>
        <p class="text-sm text-text-secondary">{{ t('currencies.description') }}</p>
      </div>
      <div class="flex items-center gap-2">
        <!-- Update exchange rates -->
        <button
          class="flex items-center gap-1.5 px-4 py-2.5 text-[0.85rem] font-medium text-text-secondary bg-surface-card border border-border rounded-[10px] hover:text-text-primary hover:border-white/20 transition-colors"
          :disabled="updatingRates"
          @click="handleUpdateRates"
        >
          <svg class="w-4 h-4" :class="{ 'animate-spin': updatingRates }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="23 4 23 10 17 10" />
            <polyline points="1 20 1 14 7 14" />
            <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
          </svg>
          <span v-if="updatingRates">{{ t('currencies.updatingRates') }}</span>
          <span v-else>{{ t('currencies.updateRates') }}</span>
        </button>

        <!-- Update product prices -->
        <button
          class="flex items-center gap-1.5 px-4 py-2.5 text-[0.85rem] font-medium text-text-secondary bg-surface-card border border-border rounded-[10px] hover:text-text-primary hover:border-white/20 transition-colors"
          :disabled="updatingPrices || nonBaseCurrencies.length === 0"
          @click="openPricesPicker"
        >
          {{ t('currencies.updateProductPrices') }}
        </button>

        <!-- Add currency -->
        <button
          class="gradient-brand text-white rounded-[10px] px-5 py-2.5 text-[0.85rem] font-semibold transition-all duration-150 hover:-translate-y-px flex items-center gap-1.5"
          @click="openCreate"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          {{ t('currencies.addCurrency') }}
        </button>
      </div>
    </div>

    <!-- Result message -->
    <div
      v-if="resultMessage"
      class="text-sm rounded-xl p-4 mb-5 flex items-center gap-2"
      :class="resultIsError
        ? 'bg-status-red/8 border border-status-red/20 text-status-red'
        : 'bg-primary-500/8 border border-primary-500/20 text-primary-400'"
    >
      <svg v-if="!resultIsError" class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="20 6 9 17 4 12" />
      </svg>
      {{ resultMessage }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading currencies...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border">
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.code') }}</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.numeric') }}</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.format') }}</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.decimals') }}</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.rateToBase') }}</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.base') }}</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.enabled') }}</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">{{ t('currencies.table.actions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-border">
          <tr
            v-for="c in store.currencies"
            :key="c.code"
            class="hover:bg-white/[0.02] transition-colors"
          >
            <td class="px-4 py-3.5 font-semibold text-text-primary">{{ c.code }}</td>
            <td class="px-4 py-3.5 text-text-secondary tabular-nums">{{ c.numeric }}</td>
            <td class="px-4 py-3.5 text-text-secondary">{{ formatSample(c) }}</td>
            <td class="px-4 py-3.5 text-right text-text-secondary tabular-nums">{{ c.decimals }}</td>
            <td class="px-4 py-3.5 text-right text-text-secondary tabular-nums">
              {{ c.isBase ? '—' : c.rateToBase.toFixed(8) }}
            </td>
            <td class="px-4 py-3.5">
              <span
                v-if="c.isBase"
                class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1 text-primary-400 bg-primary-500/10 border border-primary-500/20"
              >
                {{ t('currencies.baseBadge') }}
              </span>
              <button
                v-else
                class="text-[0.72rem] text-text-muted hover:text-text-primary transition-colors underline decoration-dotted"
                @click="makeBaseTarget = c"
              >
                {{ t('currencies.makeBase') }}
              </button>
            </td>
            <td class="px-4 py-3.5">
              <span
                class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                :class="c.isEnabled
                  ? 'text-status-green bg-status-green/10 border border-status-green/20'
                  : 'text-status-red bg-status-red/10 border border-status-red/20'"
              >
                {{ c.isEnabled ? t('currencies.enabledBadge') : t('currencies.disabledBadge') }}
              </span>
            </td>
            <td class="px-4 py-3.5 text-right">
              <button
                class="text-text-muted hover:text-primary-400 transition-colors p-1"
                :title="t('currencies.editTooltip')"
                @click="openEdit(c)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                </svg>
              </button>
            </td>
          </tr>
          <tr v-if="store.currencies.length === 0">
            <td colspan="8" class="px-5 py-8 text-center text-text-muted">
              {{ t('currencies.empty') }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Add/edit currency modal -->
    <CurrencyFormModal
      v-if="showModal"
      :currency="editingCurrency"
      :saving="saving"
      @save="handleSave"
      @close="showModal = false"
    />

    <!-- Make base confirmation -->
    <UiConfirmModal
      v-if="makeBaseTarget"
      :title="t('currencies.makeBaseModal.title')"
      :message="t('currencies.makeBaseModal.message', { code: makeBaseTarget.code })"
      :confirm-label="t('currencies.makeBaseModal.confirmLabel')"
      :loading-label="t('currencies.makeBaseModal.loadingLabel')"
      :loading="makingBase"
      variant="primary"
      @confirm="confirmMakeBase"
      @close="makeBaseTarget = null"
    />

    <!-- Product price recompute: currency picker -->
    <Teleport v-if="showPricesPicker" to="body">
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showPricesPicker = false">
        <div class="bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
          <h2 class="text-text-primary font-semibold text-[1rem]">{{ t('currencies.pricesPicker.title') }}</h2>
          <p class="text-text-secondary text-sm">{{ t('currencies.pricesPicker.description') }}</p>
          <div class="flex flex-col gap-2 max-h-52 overflow-y-auto">
            <label
              v-for="c in nonBaseCurrencies"
              :key="c.code"
              class="flex items-center gap-2 text-sm text-text-secondary"
            >
              <UiCheckbox
                :model-value="selectedPriceCurrencies.includes(c.code)"
                @update:model-value="checked => {
                  selectedPriceCurrencies = checked
                    ? [...selectedPriceCurrencies, c.code]
                    : selectedPriceCurrencies.filter(code => code !== c.code)
                }"
              />
              {{ c.code }}
            </label>
          </div>
          <div class="flex justify-end gap-2">
            <button
              class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary hover:text-text-primary text-sm rounded-lg transition-colors"
              @click="showPricesPicker = false"
            >
              {{ t('currencies.pricesPicker.cancel') }}
            </button>
            <button
              class="px-4 py-2 gradient-brand text-white text-sm rounded-lg transition-opacity disabled:opacity-50"
              :disabled="selectedPriceCurrencies.length === 0"
              @click="confirmPricesPicker"
            >
              {{ t('currencies.pricesPicker.continue') }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Product price recompute: overwrite confirmation -->
    <UiConfirmModal
      v-if="showPricesConfirm"
      :title="t('currencies.pricesConfirm.title')"
      :message="t('currencies.pricesConfirm.message', { codes: selectedPriceCurrencies.join(', ') })"
      :confirm-label="t('currencies.pricesConfirm.confirmLabel')"
      :loading-label="t('currencies.pricesConfirm.loadingLabel')"
      :loading="updatingPrices"
      variant="danger"
      @confirm="handleUpdatePrices"
      @close="showPricesConfirm = false"
    />

  </div>
</template>
