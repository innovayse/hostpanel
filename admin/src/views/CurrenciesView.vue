<script setup lang="ts">
/**
 * Admin Currencies settings view: table of configured currencies with rate, base and enabled
 * state, plus the bulk exchange-rate and product-price actions.
 *
 * A result banner reports the async actions, a confirmation dialog guards the destructive or global
 * ones, and each row has its own edit button.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import CurrencyFormModal from '@/components/settings/CurrencyFormModal.vue'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { Icon } from '@/lib/icons'
import { formatAffixed } from '@/lib/money'
import { useCurrenciesStore } from '@/stores/currencies'
import type { CurrencyDto, UpsertCurrencyPayload } from '@/types/currency'

const { t } = useI18n()

/** Currencies store: the list, the loading flag and the refusal sentences. */
const store = useCurrenciesStore()

/** The result banner for the bulk actions. */
const banner = useResultBanner()

/** Whether the add/edit currency dialog is visible. */
const showModal = ref(false)

/** Currency being edited, or null for create mode. */
const editingCurrency = ref<CurrencyDto | null>(null)

/** True while a currency save is in flight. */
const saving = ref(false)

/** The currency pending a "make base" confirmation, or null when the dialog is closed. */
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

/** Non-base currencies, candidates for the product-price recompute picker. */
const nonBaseCurrencies = computed<CurrencyDto[]>(() => store.currencies.filter((c) => !c.isBase))

/**
 * Formats a sample amount with a currency's prefix, suffix and decimals.
 *
 * @param currency - The currency to format the sample for.
 * @returns The formatted sample string, e.g. "2.99 → $2.99".
 */
const formatSample = (currency: CurrencyDto): string =>
  `2.99 → ${formatAffixed(2.99, currency)}`

/** Opens the dialog in create mode. */
const openCreate = (): void => {
  editingCurrency.value = null
  showModal.value = true
}

/**
 * Opens the dialog in edit mode for the given currency.
 *
 * @param currency - The currency to edit.
 */
const openEdit = (currency: CurrencyDto): void => {
  editingCurrency.value = currency
  showModal.value = true
}

/**
 * Handles the save event from the currency form dialog.
 *
 * @param code - The currency code being saved.
 * @param payload - The upsert payload.
 */
const handleSave = async (code: string, payload: UpsertCurrencyPayload): Promise<void> => {
  saving.value = true
  const saved = await store.upsert(code, payload)
  saving.value = false
  if (saved) {
    showModal.value = false
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
}

/** Confirms and applies a "make base" switch for the targeted currency. */
const confirmMakeBase = async (): Promise<void> => {
  if (!makeBaseTarget.value) {
    return
  }
  makingBase.value = true
  const code = makeBaseTarget.value.code
  const switched = await store.makeBase(code)
  if (switched) {
    banner.show(t('currencies.toasts.makeBaseSucceeded', { code }), false)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  makingBase.value = false
  makeBaseTarget.value = null
}

/** Refreshes exchange rates from the source and reports which codes changed. */
const handleUpdateRates = async (): Promise<void> => {
  updatingRates.value = true
  const result = await store.updateRates()
  updatingRates.value = false
  if (!result) {
    if (store.actionError) {
      banner.show(store.actionError, true)
    }
    return
  }
  const updated = result.updated.length > 0 ? result.updated.join(', ') : t('currencies.toasts.none')
  const missing = result.missing.length > 0
    ? t('currencies.toasts.ratesMissing', { codes: result.missing.join(', ') })
    : ''
  banner.show(t('currencies.toasts.ratesUpdated', { updated, missing }), false)
}

/** Opens the currency picker for the product-price recompute. */
const openPricesPicker = (): void => {
  selectedPriceCurrencies.value = []
  showPricesPicker.value = true
}

/** Advances from the picker to the overwrite confirmation. */
const confirmPricesPicker = (): void => {
  if (selectedPriceCurrencies.value.length === 0) {
    return
  }
  showPricesPicker.value = false
  showPricesConfirm.value = true
}

/**
 * Adds or removes a currency from the product-price selection.
 *
 * @param code - The currency code.
 * @param checked - Whether it is now selected.
 */
const togglePriceCurrency = (code: string, checked: boolean): void => {
  selectedPriceCurrencies.value = checked
    ? [...selectedPriceCurrencies.value, code]
    : selectedPriceCurrencies.value.filter((selected) => selected !== code)
}

/** Recomputes product prices in the selected currencies from the base prices. */
const handleUpdatePrices = async (): Promise<void> => {
  updatingPrices.value = true
  const written = await store.updateProductPrices(selectedPriceCurrencies.value)
  if (written !== null) {
    banner.show(t('currencies.toasts.pricesUpdated', { count: written }, written), false)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  updatingPrices.value = false
  showPricesConfirm.value = false
  selectedPriceCurrencies.value = []
}

/**
 * The one row action of a currency: edit it in the dialog.
 *
 * @param currency - The row.
 * @returns The action list passed to UiRowActions.
 */
const currencyActions = (currency: CurrencyDto): UiRowAction[] => [
  {
    label: t('shared.edit'),
    testid: `currency-edit-${currency.code}`,
    /**
     * Opens the edit dialog for this currency.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      openEdit(currency)
    },
  },
]

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack gap="none" data-testid="currencies-view">
    <UiPageHeader :title="t('currencies.title')" :subtitle="t('currencies.description')">
      <template #actions>
        <UiButton
          type="button"
          variant="secondary"
          :disabled="updatingRates"
          data-testid="currencies-update-rates"
          @click="handleUpdateRates"
        >
          <UiIcon :markup="Icon.Refresh" size="sm" />
          {{ updatingRates ? t('currencies.updatingRates') : t('currencies.updateRates') }}
        </UiButton>
        <UiButton
          type="button"
          variant="secondary"
          :disabled="updatingPrices || nonBaseCurrencies.length === 0"
          data-testid="currencies-update-prices"
          @click="openPricesPicker"
        >
          {{ t('currencies.updateProductPrices') }}
        </UiButton>
        <UiButton type="button" variant="primary" data-testid="currencies-add" @click="openCreate">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('currencies.addCurrency') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? 'danger' : 'success'"
      class="mb-5"
      data-testid="currencies-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <SettingsLoading v-if="store.loading" :message="t('settings.currencies.loadingCurrencies')" />

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="currencies-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else class="overflow-hidden">
      <UiTable density="comfortable" :empty="store.currencies.length === 0" :colspan="8">
        <template #empty>
          {{ t('currencies.empty') }}
        </template>
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader align="left">{{ t('currencies.table.code') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('currencies.table.numeric') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('currencies.table.format') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('currencies.table.decimals') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('currencies.table.rateToBase') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('currencies.table.base') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('currencies.table.enabled') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('currencies.table.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="c in store.currencies"
            :key="c.code"
            hoverable
            :data-testid="`currency-row-${c.code}`"
          >
            <UiTableCell class="font-semibold text-text-primary">{{ c.code }}</UiTableCell>
            <UiTableCell muted class="tabular-nums">{{ c.numeric }}</UiTableCell>
            <UiTableCell muted>{{ formatSample(c) }}</UiTableCell>
            <UiTableCell numeric muted>{{ c.decimals }}</UiTableCell>
            <UiTableCell numeric muted>
              {{ c.isBase ? '—' : c.rateToBase.toFixed(8) }}
            </UiTableCell>
            <UiTableCell>
              <UiBadge v-if="c.isBase" tone="primary" small>
                {{ t('currencies.baseBadge') }}
              </UiBadge>
              <UiButton
                v-else
                type="button"
                variant="link"
                size="sm"
                :data-testid="`currency-make-base-${c.code}`"
                @click="makeBaseTarget = c"
              >
                {{ t('currencies.makeBase') }}
              </UiButton>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="c.isEnabled ? 'success' : 'danger'" small>
                {{ c.isEnabled ? t('currencies.enabledBadge') : t('currencies.disabledBadge') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="currencyActions(c)" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <!-- Add/edit currency dialog -->
    <CurrencyFormModal
      v-if="showModal"
      :currency="editingCurrency"
      :saving="saving"
      @save="handleSave"
      @close="showModal = false"
    />

    <!-- Make base confirmation -->
    <UiDialog
      v-if="makeBaseTarget"
      variant="confirm"
      :title="t('currencies.makeBaseModal.title')"
      :message="t('currencies.makeBaseModal.message', { code: makeBaseTarget.code })"
      :confirm-label="t('currencies.makeBaseModal.confirmLabel')"
      :loading-label="t('currencies.makeBaseModal.loadingLabel')"
      :loading="makingBase"
      tone="primary"
      @confirm="confirmMakeBase"
      @close="makeBaseTarget = null"
    />

    <!-- Product price recompute: currency picker -->
    <UiDialog
      v-if="showPricesPicker"
      :title="t('currencies.pricesPicker.title')"
      @close="showPricesPicker = false"
    >
      <UiText tone="secondary" size="md">
        {{ t('currencies.pricesPicker.description') }}
      </UiText>
      <UiStack gap="sm" class="max-h-52 overflow-y-auto">
        <UiLabel v-for="c in nonBaseCurrencies" :key="c.code" variant="inline">
          <UiCheckbox
            :model-value="selectedPriceCurrencies.includes(c.code)"
            :data-testid="`currencies-price-${c.code}`"
            @update:model-value="togglePriceCurrency(c.code, $event)"
          />
          {{ c.code }}
        </UiLabel>
      </UiStack>
      <template #footer>
        <UiButton type="button" variant="secondary" data-testid="currencies-prices-cancel" @click="showPricesPicker = false">
          {{ t('currencies.pricesPicker.cancel') }}
        </UiButton>
        <UiButton
          type="button"
          variant="primary"
          :disabled="selectedPriceCurrencies.length === 0"
          data-testid="currencies-prices-continue"
          @click="confirmPricesPicker"
        >
          {{ t('currencies.pricesPicker.continue') }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- Product price recompute: overwrite confirmation -->
    <UiDialog
      v-if="showPricesConfirm"
      variant="confirm"
      :title="t('currencies.pricesConfirm.title')"
      :message="t('currencies.pricesConfirm.message', { codes: selectedPriceCurrencies.join(', ') })"
      :confirm-label="t('currencies.pricesConfirm.confirmLabel')"
      :loading-label="t('currencies.pricesConfirm.loadingLabel')"
      :loading="updatingPrices"
      tone="danger"
      @confirm="handleUpdatePrices"
      @close="showPricesConfirm = false"
    />
  </UiStack>
</template>
