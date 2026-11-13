<script setup lang="ts">
/**
 * Modal for adding one billable item to a client: a one time or recurring charge with its
 * description, amount, currency and the day it is invoiced automatically (the invoice day of a one
 * time item, the next due date of a recurring one). A recurring item also takes how often it repeats.
 * The form sends exactly the API's create command; a refusal shows the API's sentence above the form
 * and each field's message under its field. Every opening starts without the last refusal's messages.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useClientBillableItemsStore } from '@/stores/clientBillableItems'
import { useClientsStore } from '@/stores/clients'
import { BILLABLE_ITEM_TYPE_OPTIONS, RECURRING_PERIOD_OPTIONS } from '@/lib/constants'
import type { SelectOption } from '@/lib/constants'
import { toIsoDay } from '@/lib/format'
import { BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH } from '@/lib/limits'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import type { CreateClientBillableItemPayload } from '@/types/billableitem'
import UiButton from '@/components/ui/UiButton.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'

/** Translator for the modal's labels. */
const { t } = useI18n()

/** Props for AddBillableItemModal. */
const props = defineProps<{
  /** Id of the client the item is billed to. */
  clientId: string
}>()

/** Events of AddBillableItemModal. */
const emit = defineEmits<{
  /** Emitted after the API accepted the item. */
  saved: []
  /** Emitted when the operator closes the modal. */
  close: []
}>()

/** Billable items store, the only caller of the billable item endpoints. */
const store = useClientBillableItemsStore()

/** Clients store: the open client's billing currency and the currencies the operator enabled. */
const clientsStore = useClientsStore()

/** True while the item is being sent. */
const saving = ref(false)

/** What the charge is for. */
const description = ref('')

/** Charge amount. */
const amount = ref(0)

/** ISO 4217 code of the charge currency; empty until the currencies are known. */
const currency = ref('')

/** One time or recurring. */
const type = ref<BillableItemType>(BillableItemType.OneTime)

/** How often a recurring item repeats. */
const recurringPeriod = ref<RecurringPeriod>(RecurringPeriod.Monthly)

/**
 * Day (ISO `YYYY-MM-DD`) the item is invoiced automatically: once for a one time item, next for a
 * recurring one; null for none, which leaves the item to be invoiced by hand.
 */
const nextDueDate = ref<string | null>(toIsoDay(new Date()))

/** True when the item recurs, so its period is shown and sent. */
const isRecurring = computed<boolean>(() => type.value === BillableItemType.Recurring)

/** Label of the due date field: the invoice day of a one time item, the next due date of a recurring one. */
const dueDateLabel = computed<string>(() =>
  isRecurring.value ? t('billing.addBillableItem.nextDueDate') : t('billing.addBillableItem.invoiceOn'),
)

/** The currencies the operator enabled, as select options. */
const currencyOptions = computed<SelectOption[]>(() =>
  clientsStore.currencies.map(option => ({ value: option.code, label: option.code })),
)

/** Billing currency of this client when the open client is this one and has one set, else null. */
const clientCurrency = computed<string | null>(() => {
  const client = clientsStore.current
  if (client === null || String(client.id) !== props.clientId) {
    return null
  }
  return client.preferences.currency ?? null
})

/**
 * Builds the create command from the form: the due date for every item, the period only for a
 * recurring one.
 *
 * @returns The body the API's create command expects.
 */
const toPayload = (): CreateClientBillableItemPayload => {
  const payload: CreateClientBillableItemPayload = {
    description: description.value,
    amount: amount.value,
    currency: currency.value,
    type: type.value,
    nextDueDate: nextDueDate.value ?? undefined,
  }
  if (!isRecurring.value) {
    return payload
  }
  return { ...payload, recurringPeriod: recurringPeriod.value }
}

/**
 * Sends the item and tells the parent once the API accepted it.
 *
 * @returns Promise that resolves when the request is over.
 */
const handleSave = async (): Promise<void> => {
  saving.value = true
  const saved = await store.addItem(props.clientId, toPayload())
  saving.value = false
  if (saved) {
    emit('saved')
  }
}

/**
 * The modal is mounted each time it opens: drops the messages of an earlier refused add, loads the
 * enabled currencies and starts on the client's billing currency, else the first one.
 */
onMounted(async () => {
  store.clearSaveErrors()
  await clientsStore.fetchCurrencies()
  currency.value = clientCurrency.value ?? currencyOptions.value[0]?.value ?? ''
})
</script>

<template>
  <UiDialog :title="t('shared.addBillableItem')" :size="UiDialogSize.Lg" @close="emit('close')">
    <UiStack>
      <UiStatusMessage :message="store.saveError" :tone="UiTone.Danger" data-testid="clients-add-billable-item-error" />

      <UiForm stacked @submit="handleSave">
        <UiField
          :label="t('shared.description')"
          :error="store.fieldErrors.description"
          error-testid="clients-add-billable-item-description-error"
        >
          <template #default="{ id, invalid }">
            <UiTextarea
              :id="id"
              v-model="description"
              rows="3"
              :maxlength="BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH"
              :invalid="invalid"
              :placeholder="t('clients.addBillableItem.describeTheCharge')"
              data-testid="clients-add-billable-item-description"
            />
          </template>
        </UiField>

        <UiGrid :columns="2">
          <UiField
            :label="t('shared.amount')"
            :error="store.fieldErrors.amount"
            error-testid="clients-add-billable-item-amount-error"
          >
            <UiNumberInput v-model="amount" :step="0.01" :min="0" placeholder="0.00" data-testid="clients-add-billable-item-amount" />
          </UiField>
          <UiField
            :label="t('shared.currency')"
            :error="store.fieldErrors.currency"
            error-testid="clients-add-billable-item-currency-error"
          >
            <UiSelect
              v-model="currency"
              :options="currencyOptions"
              :placeholder="t('shared.selectCurrency')"
              data-testid="clients-add-billable-item-currency"
            />
          </UiField>
        </UiGrid>

        <UiGrid :columns="2">
          <UiField :label="t('shared.type')" :error="store.fieldErrors.type" error-testid="clients-add-billable-item-type-error">
            <UiSelect
              v-model="type"
              :options="BILLABLE_ITEM_TYPE_OPTIONS"
              :placeholder="t('shared.type')"
              data-testid="clients-add-billable-item-type"
            />
          </UiField>
          <UiField
            :label="dueDateLabel"
            :error="store.fieldErrors.nextDueDate"
            error-testid="clients-add-billable-item-next-due-date-error"
            data-testid="clients-add-billable-item-next-due-date-field"
          >
            <UiDatePicker
              v-model="nextDueDate"
              :direction="UiDatePickerDirection.Future"
              :placeholder="t('shared.selectDate')"
              data-testid="clients-add-billable-item-next-due-date"
            />
          </UiField>
        </UiGrid>

        <UiField
          v-if="isRecurring"
          :label="t('billing.addBillableItem.recurringPeriod')"
          :error="store.fieldErrors.recurringPeriod"
          error-testid="clients-add-billable-item-recurring-period-error"
          data-testid="clients-add-billable-item-recurrence"
        >
          <UiSelect
            v-model="recurringPeriod"
            :options="RECURRING_PERIOD_OPTIONS"
            :placeholder="t('billing.addBillableItem.recurringPeriod')"
            data-testid="clients-add-billable-item-recurring-period"
          />
        </UiField>

        <UiStack :direction="UiDirection.Row" :justify="UiJustify.End" :align="UiAlignItems.Center" :gap="UiGap.Sm">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="clients-add-billable-item-cancel"
            @click="emit('close')"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            type="submit"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            data-testid="clients-add-billable-item-save"
          >
            {{ t('shared.save') }}
          </UiButton>
        </UiStack>
      </UiForm>
    </UiStack>
  </UiDialog>
</template>
