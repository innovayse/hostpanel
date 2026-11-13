<script setup lang="ts">
/**
 * Full-page form for creating or editing a TLD pricing configuration.
 *
 * Detects edit mode from the route params, loads the configuration when editing, and carries the
 * pricing tables for registration, transfer and renewal across the listed year periods. Asks
 * before leaving with unsaved edits. A refused load shows the API's sentence with a retry instead of
 * the form; a refused save shows next to the save bar, a saved configuration returns to the list.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import SettingsBreadcrumb from '@/components/settings/SettingsBreadcrumb.vue'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import TldPricingTable from '@/components/settings/TldPricingTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStepper from '@/components/ui/UiStepper.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiTone } from '@/utils/ui/tone'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useUnsavedChangesGuard } from '@/composables/useUnsavedChangesGuard'
import { CURRENCY_CODES, currencyLabel, currencySymbol } from '@/lib/money'
import { CurrencyCode } from '@/types/currency'
import { emptyPriceMap, fromApiMap, toNonZeroMap } from '@/lib/tldPricing'
import { useTldConfigsStore } from '@/stores/tldConfigs'
import { RegistrarModule } from '@/types/registrarmodule'
import type { CreateTldConfigPayload, UpdateTldConfigPayload } from '@/types/tldconfig'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Current route; carries the TLD config id in edit mode. */
const route = useRoute()

/** Router, used to return to the list. */
const router = useRouter()

/** TLD configs store: loads, creates and updates the configuration. */
const store = useTldConfigsStore()

/** Whether we are in edit mode (TLD config ID present in the route). */
const isEditMode = computed<boolean>(() => !!route.params.id)

/** Page title based on create/edit mode. */
const pageTitle = computed<string>(() => (isEditMode.value ? t('settings.tldConfigForm.editTld') : t('settings.tldConfigForm.addTld')))

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('shared.settings'), to: AppRoute.Settings },
  { label: t('settings.tldConfigForm.tldPricing'), to: AppRoute.SettingsTldPricing },
  { label: pageTitle.value },
])

/** Registrar options of the provider dropdown. */
const providerOptions = computed(() => [
  { value: RegistrarModule.NameAm as string, label: t('settings.tldConfigs.nameAm') },
  { value: RegistrarModule.Namecheap as string, label: t('settings.tldConfigs.namecheap') },
])

/** Currency options of the cost and sell currency dropdowns. */
const currencyOptions = CURRENCY_CODES.map((code) => ({ value: code as string, label: currencyLabel(code) }))

/** True while loading existing config data in edit mode. */
const loadingConfig = ref(false)

/** True while saving the form. */
const saving = ref(false)

/** The sentence for the last refused save, null when none. */
const saveError = ref<string | null>(null)

/** Sentence for a failed load of the configuration being edited, null when none. */
const loadError = ref<string | null>(null)

/** Tracks whether the form has been modified since loading. */
const isDirty = ref(false)

/** TLD input value (e.g. "com"). */
const tld = ref('')

/** Selected registrar module. */
const registrarModule = ref<string>(RegistrarModule.NameAm)

/** Cost currency code. */
const currency = ref<string>(CurrencyCode.Amd)

/** Sell currency code. */
const sellCurrency = ref<string>(CurrencyCode.Usd)

/** Whether this TLD is enabled for sale. */
const isEnabled = ref(true)

/** Display sort order. */
const sortOrder = ref(0)

/** Categories as a comma-separated string input. */
const categoriesInput = ref('')

/** Registration cost prices keyed by period. */
const costRegister = ref<Record<string, number>>(emptyPriceMap())

/** Transfer cost prices keyed by period. */
const costTransfer = ref<Record<string, number>>(emptyPriceMap())

/** Renewal cost prices keyed by period. */
const costRenew = ref<Record<string, number>>(emptyPriceMap())

/** Registration sell prices keyed by period. */
const sellRegister = ref<Record<string, number>>(emptyPriceMap())

/** Transfer sell prices keyed by period. */
const sellTransfer = ref<Record<string, number>>(emptyPriceMap())

/** Renewal sell prices keyed by period. */
const sellRenew = ref<Record<string, number>>(emptyPriceMap())

/** Symbol of the cost currency. */
const costSymbol = computed<string>(() => currencySymbol(currency.value))

/** Symbol of the sell currency. */
const sellSymbol = computed<string>(() => currencySymbol(sellCurrency.value))

/** Parks a route change while the reader is asked about unsaved edits. */
const leaveGuard = useUnsavedChangesGuard(() => isDirty.value && !saving.value)

/** Marks the form as dirty on any change. */
watch(
  [tld, registrarModule, currency, sellCurrency, isEnabled, sortOrder, categoriesInput,
    costRegister, costTransfer, costRenew, sellRegister, sellTransfer, sellRenew],
  () => {
    isDirty.value = true
  },
  { deep: true },
)

/**
 * Loads the configuration being edited into the form; a refusal is kept in `loadError`.
 *
 * @returns Resolves when the load has settled.
 */
const loadConfig = async (): Promise<void> => {
  loadError.value = null
  loadingConfig.value = true
  const id = Number(route.params.id)
  const config = await store.fetchById(id)
  loadingConfig.value = false
  if (!config) {
    loadError.value = store.error ?? t('settings.tldConfigForm.notFound', { id })
    return
  }

  tld.value = config.tld
  registrarModule.value = config.registrarModule
  currency.value = config.currency
  sellCurrency.value = config.sellCurrency
  isEnabled.value = config.isEnabled
  sortOrder.value = config.sortOrder
  categoriesInput.value = config.categories.join(', ')

  costRegister.value = fromApiMap(config.costs.register)
  costTransfer.value = fromApiMap(config.costs.transfer)
  costRenew.value = fromApiMap(config.costs.renew)
  sellRegister.value = fromApiMap(config.sells.register)
  sellTransfer.value = fromApiMap(config.sells.transfer)
  sellRenew.value = fromApiMap(config.sells.renew)

  // Populating the form is not an edit.
  isDirty.value = false
}

onMounted(async () => {
  if (isEditMode.value) {
    await loadConfig()
  }
})

/**
 * Keeps the TLD field to what a domain extension can contain: lower-case letters, digits, hyphens.
 *
 * @param value - The text the operator typed.
 */
const onTldInput = (value: string): void => {
  tld.value = value.toLowerCase().replace(/[^a-z0-9-]/g, '')
}

/**
 * Writes one period of a price map.
 *
 * @param map - The price map to change.
 * @param period - The registration period.
 * @param value - The new price.
 */
const setPrice = (map: Record<string, number>, period: string, value: number): void => {
  map[period] = value
}

/** Collects form data and dispatches create or update via the store; returns to the list on success. */
const handleSave = async (): Promise<void> => {
  if (!tld.value.trim()) {
    saveError.value = t('settings.tldConfigForm.tldIsRequired')
    return
  }

  saving.value = true
  saveError.value = null

  const categories = categoriesInput.value
    .split(',')
    .map((c) => c.trim())
    .filter((c) => c.length > 0)

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

  let saved: boolean
  if (isEditMode.value) {
    const id = Number(route.params.id)
    const update: UpdateTldConfigPayload = { ...payload, id }
    saved = await store.update(id, update)
  } else {
    saved = (await store.create(payload)) !== null
  }
  saving.value = false

  if (saved) {
    isDirty.value = false
    void router.push(AppRoute.SettingsTldPricing)
  } else {
    saveError.value = store.actionError
  }
}

/** Navigates back to the TLD pricing list; the unsaved-changes guard asks first if needed. */
const handleCancel = (): void => {
  void router.push(AppRoute.SettingsTldPricing)
}
</script>

<template>
  <UiStack gap="none" data-testid="tld-config-form-view">
    <SettingsBreadcrumb :items="breadcrumb" />

    <UiErrorState v-if="loadError" :message="loadError" testid="tld-config-form-load-error" @retry="loadConfig" />

    <template v-else>

      <UiGrid :columns="2" class="items-start">
        <!-- Left: general settings -->
        <SettingsSection :title="t('settings.tldConfigForm.generalSettings')" :loading="loadingConfig" data-testid="tld-config-form-general">
          <UiField :label="t('settings.tldConfigForm.tld')" :hint="t('settings.tldConfigForm.domainExtensionWithoutTheDot')">
            <template #default="{ id, describedby }">
              <UiStack direction="row" align="center" gap="sm">
                <UiText tone="muted" size="lg">.</UiText>
                <UiInput
                  :id="id"
                  :model-value="tld"
                  type="text"
                  :placeholder="t('shared.examples.tld')"
                  :disabled="isEditMode"
                  :aria-describedby="describedby"
                  data-testid="tld-config-form-tld"
                  class="flex-1 lowercase"
                  @update:model-value="onTldInput"
                />
              </UiStack>
            </template>
          </UiField>

          <UiField :label="t('settings.tldConfigForm.registrarProvider')">
            <UiSelect
              v-model="registrarModule"
              :options="providerOptions"
              :placeholder="t('settings.tldConfigForm.registrarProvider')"
              data-testid="tld-config-form-provider"
            />
          </UiField>

          <UiField :label="t('settings.tldConfigForm.costCurrency')" :hint="t('settings.tldConfigForm.currencyUsedByTheRegistrar')">
            <UiSelect
              v-model="currency"
              :options="currencyOptions"
              :placeholder="t('settings.tldConfigForm.costCurrency')"
              data-testid="tld-config-form-cost-currency"
            />
          </UiField>

          <UiField :label="t('settings.tldConfigForm.sellCurrency')" :hint="t('settings.tldConfigForm.currencyShownToClientsFor')">
            <UiSelect
              v-model="sellCurrency"
              :options="currencyOptions"
              :placeholder="t('settings.tldConfigForm.sellCurrency')"
              data-testid="tld-config-form-sell-currency"
            />
          </UiField>

          <UiStack direction="row" align="center" justify="between">
            <UiStack gap="xs">
              <UiText tone="primary" size="md">{{ t('shared.enabled') }}</UiText>
              <UiText tone="muted" size="sm">{{ t('settings.tldConfigForm.makeThisTldAvailableFor') }}</UiText>
            </UiStack>
            <UiToggle v-model="isEnabled" data-testid="tld-config-form-enabled" />
          </UiStack>

          <UiField :label="t('settings.tldConfigForm.sortOrder')">
            <UiStepper
              v-model="sortOrder"
              :min="0"
              :step="1"
              placeholder="0"
              data-testid="tld-config-form-sort-order"
            />
          </UiField>

          <UiField :label="t('shared.categories')" :hint="t('settings.tldConfigForm.commaSeparatedCategoryTagsFor')">
            <template #default="{ id, describedby }">
              <UiInput
                :id="id"
                v-model="categoriesInput"
                type="text"
                :aria-describedby="describedby"
                :placeholder="t('settings.tldConfigForm.popularBusinessTech')"
                data-testid="tld-config-form-categories"
              />
            </template>
          </UiField>
        </SettingsSection>

        <!-- Right: pricing -->
        <UiStack gap="none">
          <TldPricingTable
            :title="t('settings.tldConfigForm.registrationPricing')"
            testid="tld-register"
            :cost-symbol="costSymbol"
            :sell-symbol="sellSymbol"
            :loading="loadingConfig"
            :cost="costRegister"
            :sell="sellRegister"
            @update:cost="(period, value) => setPrice(costRegister, period, value)"
            @update:sell="(period, value) => setPrice(sellRegister, period, value)"
          />
          <TldPricingTable
            :title="t('settings.tldConfigForm.transferPricing')"
            testid="tld-transfer"
            :cost-symbol="costSymbol"
            :sell-symbol="sellSymbol"
            :loading="loadingConfig"
            :cost="costTransfer"
            :sell="sellTransfer"
            @update:cost="(period, value) => setPrice(costTransfer, period, value)"
            @update:sell="(period, value) => setPrice(sellTransfer, period, value)"
          />
          <TldPricingTable
            :title="t('settings.tldConfigForm.renewalPricing')"
            testid="tld-renew"
            :cost-symbol="costSymbol"
            :sell-symbol="sellSymbol"
            :loading="loadingConfig"
            :cost="costRenew"
            :sell="sellRenew"
            @update:cost="(period, value) => setPrice(costRenew, period, value)"
            @update:sell="(period, value) => setPrice(sellRenew, period, value)"
          />
        </UiStack>
      </UiGrid>

      <UiStatusMessage :message="saveError" :tone="UiTone.Danger" class="mb-5" data-testid="tld-config-form-error" />

      <SettingsFormFooter
        :save-label="isEditMode ? t('shared.saveChanges') : t('settings.tldConfigForm.createTld')"
        :saving="saving"
        :dirty="!loadingConfig"
        @cancel="handleCancel"
        @save="handleSave"
      />
    </template>

    <!-- Unsaved-changes question, asked while the route change waits -->
    <UiDialog
      v-if="leaveGuard.pending.value"
      variant="confirm"
      :title="t('settings.tldConfigForm.unsavedChanges')"
      :message="t('settings.tldConfigForm.youHaveUnsavedChangesAre')"
      :confirm-label="t('settings.tldConfigForm.leave')"
      tone="warning"
      @confirm="leaveGuard.confirmLeave"
      @close="leaveGuard.cancelLeave"
    />
  </UiStack>
</template>
