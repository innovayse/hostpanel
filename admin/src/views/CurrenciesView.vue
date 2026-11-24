<script setup lang="ts">
/**
 * Admin Currencies settings view: the exchange rate source choice and the table of configured
 * currencies with rate, base and enabled state, plus the bulk exchange-rate and product-price actions.
 *
 * A result banner reports the async actions, a confirmation dialog guards the destructive or global
 * ones, and each row has its own edit button.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import CurrencyFormModal from '@/components/settings/CurrencyFormModal.vue'
import ExchangeRateProviderCard from '@/components/settings/ExchangeRateProviderCard.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { Icon } from '@/lib/icons'
import { formatAffixed } from '@/lib/money'
import { useCurrenciesStore } from '@/stores/currencies'
import { useSettingsStore } from '@/stores/settings'
import type { CurrencyDto, UpsertCurrencyPayload } from '@/types/currency'
import { UiTableDensity } from '@/utils/ui/injection'
import schema, { CURRENCIES_TABLE, CURRENCY_PROVIDER_PART } from '@/views/CurrenciesView.schema'
import { EMPTY_VALUE } from '@/lib/format'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import type { UiHeaderAction } from '@/utils/ui/actionBar'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiGap } from '@/utils/ui/layout'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('currencies.updateRates'), icon: Icon.Refresh, variant: UiButtonVariant.Secondary, testid: 'currencies-update-rates', loading: updatingRates.value, onSelect: handleUpdateRates },
  { label: t('currencies.updateProductPrices'), icon: actionIcons[UiActionKind.Refresh], variant: UiButtonVariant.Secondary, testid: 'currencies-update-prices', disabled: updatingPrices.value || nonBaseCurrencies.value.length === 0, onSelect: openPricesPicker },
  { label: t('currencies.addCurrency'), icon: Icon.Add, variant: UiButtonVariant.Primary, testid: 'currencies-add', onSelect: openCreate },
])

/** Currencies store: the list, the loading flag and the refusal sentences. */
const store = useCurrenciesStore()

/** Settings store: the rows the rate source card reads and writes. */
const settingsStore = useSettingsStore()

/** The result banner for the bulk actions. */
const banner = useOutcomeToast()

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

/**
 * Whether the first settings load has finished, with rows or with a refusal. The settings store starts with no rows and no
 * request in flight, so without this the rate source card would claim its row is not seeded for the moment before the load starts.
 */
const settingsLoaded = ref(false)

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
 * The row actions of a currency: edit it in the dialog, and, unless it already is the base, make it the base
 * (confirmed in a dialog first).
 *
 * @param currency - The row.
 * @returns The action list passed to UiRowActions.
 */
const currencyActions = (currency: CurrencyDto): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
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
  ...(currency.isBase
    ? []
    : [{
      label: t('currencies.makeBase'),
      icon: Icon.Star,
      testid: `currency-make-base-${currency.code}`,
      /**
       * Opens the make base confirmation for this currency.
       *
       * @returns Nothing.
       */
      onSelect: (): void => {
        makeBaseTarget.value = currency
      },
    }]),
]

/** Loads the settings the rate source card reads and marks the first load finished. */
const loadSettings = async (): Promise<void> => {
  await settingsStore.fetchSettings()
  settingsLoaded.value = true
}

onMounted(() => Promise.all([store.fetchAll(), loadSettings()]))
</script>

<template>
  <UiStack data-testid="currencies-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>


    <!-- A refused settings load is said, never hidden: the card would otherwise claim the row is not seeded. -->
    <UiErrorState
      v-if="settingsStore.error"
      :message="settingsStore.error"
      :loading="settingsStore.loading"
      testid="currencies-settings-error"
      @retry="settingsStore.fetchSettings()"
    />
    <UiDetailCards v-else-if="!settingsLoaded" :columns="CURRENCY_PROVIDER_PART.columns" loading />
    <ExchangeRateProviderCard
      v-else
      :settings="settingsStore.settings"
      :save="settingsStore.saveSetting"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="currencies-error"
      @retry="store.fetchAll()"
    />

    <UiDataTable v-else :empty-text="t('currencies.empty')" :density="UiTableDensity.Comfortable" :loading="store.loading" :empty="!store.loading && store.currencies.length === 0">
      <UiSchemaTableHead :columns="CURRENCIES_TABLE.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="c in store.currencies"
          :key="c.code"
          hoverable
          :data-testid="`currency-row-${c.code}`"
        >
          <UiTableCell strong>{{ c.code }}</UiTableCell>
          <UiTableCell muted>{{ c.numeric }}</UiTableCell>
          <UiTableCell muted>{{ formatSample(c) }}</UiTableCell>
          <UiTableCell numeric muted>{{ c.decimals }}</UiTableCell>
          <UiTableCell numeric muted>
            {{ c.isBase ? EMPTY_VALUE : c.rateToBase.toFixed(8) }}
          </UiTableCell>
          <UiTableCell>
            <UiBadge v-if="c.isBase" :tone="UiTone.Primary" small>
              {{ t('currencies.baseBadge') }}
            </UiBadge>
          </UiTableCell>
          <UiTableCell>
            <UiStatusIcon
              :state="c.isEnabled"
              :on-icon="Icon.CheckCircle"
              :off-icon="Icon.Close"
              :on-label="t('currencies.enabledBadge')"
              :off-label="t('currencies.disabledBadge')"
            />
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="currencyActions(c)" :testid="`currency-actions-${c.code}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>

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
      :variant="UiDialogVariant.Confirm"
      :title="t('currencies.makeBaseModal.title')"
      :message="t('currencies.makeBaseModal.message', { code: makeBaseTarget.code })"
      :confirm-label="t('currencies.makeBase')"
      :loading="makingBase"
      :tone="UiTone.Primary"
      @confirm="confirmMakeBase"
      @close="makeBaseTarget = null"
    />

    <!-- Product price recompute: currency picker -->
    <UiDialog
      v-if="showPricesPicker"
      :title="t('currencies.pricesPicker.title')"
      @close="showPricesPicker = false"
    >
      <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">
        {{ t('currencies.pricesPicker.description') }}
      </UiText>
      <UiStack :gap="UiGap.Sm" class="max-h-52 overflow-y-auto">
        <UiLabel v-for="c in nonBaseCurrencies" :key="c.code" :variant="UiLabelVariant.Inline">
          <UiCheckbox
            :model-value="selectedPriceCurrencies.includes(c.code)"
            :data-testid="`currencies-price-${c.code}`"
            @update:model-value="togglePriceCurrency(c.code, $event)"
          />
          {{ c.code }}
        </UiLabel>
      </UiStack>
      <template #footer>
        <UiButton :action="UiActionKind.Cancel" :type="UiButtonType.Button" :variant="UiButtonVariant.Secondary" data-testid="currencies-prices-cancel" @click="showPricesPicker = false">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Next"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Primary"
          :disabled="selectedPriceCurrencies.length === 0"
          data-testid="currencies-prices-continue"
          @click="confirmPricesPicker"
        >
          {{ t('shared.continue') }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- Product price recompute: overwrite confirmation -->
    <UiDialog
      v-if="showPricesConfirm"
      :variant="UiDialogVariant.Confirm"
      :title="t('currencies.pricesConfirm.title')"
      :message="t('currencies.pricesConfirm.message', { codes: selectedPriceCurrencies.join(', ') })"
      :confirm-label="t('currencies.pricesConfirm.confirmLabel')"
      :loading="updatingPrices"
      :tone="UiTone.Danger"
      @confirm="handleUpdatePrices"
      @close="showPricesConfirm = false"
    />
  </UiStack>
</template>
