<script setup lang="ts">
/**
 * Add billable item: a one-time or recurring charge for a client, with the day it is invoiced
 * automatically (the invoice day of a one-time charge, the next due date of a recurring one).
 * Recurring charges also take how often they recur (weekly, monthly, quarterly or annually). The
 * screen returns to the list once the API accepts the item.
 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { BILLABLE_ITEM_TYPE_OPTIONS, CURRENCY_OPTIONS, RECURRING_PERIOD_OPTIONS } from '@/lib/constants'
import { toIsoDay } from '@/lib/format'
import { useBillableItemsStore } from '@/stores/billableItems'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { CurrencyCode } from '@/types/currency'
import { AppRoute } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { ITEM_SECTION_KEY, RECURRING_ITEM_SECTION } from '@/views/AddBillableItemView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'
import { UiMaxWidth } from '@/utils/ui/layout'

/** Translator for the screen's labels. */
const { t } = useI18n()

/** Router, to return to the list after a save. */
const router = useRouter()

/** Billable items store, the only caller of the billable item endpoints. */
const store = useBillableItemsStore()

/** The clients the picker offers and the loader that fetches them. */
const { clients, load: loadClients } = useClientOptions()

/** Id of the client to bill, as the picker holds it. */
const clientId = ref('')

/** What the charge is for. */
const description = ref('')

/** Charge amount. */
const amount = ref(0)

/** Charge currency. */
const currency = ref<string>(CurrencyCode.Usd)

/** One-time or recurring. */
const type = ref<BillableItemType>(BillableItemType.OneTime)

/** Day (ISO `YYYY-MM-DD`) the charge is invoiced automatically, null for none (invoiced by hand). */
const nextDueDate = ref<string | null>(toIsoDay(new Date()))

/** How often a recurring charge repeats. */
const recurringPeriod = ref<RecurringPeriod>(RecurringPeriod.Monthly)

/** True when the charge recurs, so the recurrence fields show. */
const isRecurring = computed<boolean>(() => type.value === BillableItemType.Recurring)

/** The columns of the form: the schema's, with the card of a recurring charge while the charge recurs. */
const columns = computed(() =>
  schema.columns.map(column =>
    column.map(section => (section.key === ITEM_SECTION_KEY && isRecurring.value ? RECURRING_ITEM_SECTION : section)),
  ),
)

/** Creates the item and returns to the list when it was accepted. */
const submit = async (): Promise<void> => {
  const ok = await store.create({
    clientId: Number.parseInt(clientId.value, 10),
    description: description.value,
    amount: amount.value,
    currency: currency.value,
    type: type.value,
    recurringPeriod: isRecurring.value ? recurringPeriod.value : undefined,
    nextDueDate: nextDueDate.value || undefined,
  })
  if (ok) {
    await router.push(AppRoute.BillingBillableItems)
  }
}

/** Loads the clients the picker offers. */
onMounted(loadClients)
</script>

<template>
  <UiStack data-testid="billing-add-billable-item-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiAlert v-if="store.error" :variant="UiTone.Danger" data-testid="billing-add-billable-item-error">
      {{ store.error }}
    </UiAlert>

    <UiSchemaForm :columns="columns" :errors="store.fieldErrors" :max-width="UiMaxWidth.Form" @submit="submit">
      <template #field-clientId>
        <UiClientSelect
          v-model="clientId"
          :clients="clients"
          :placeholder="t('shared.selectAClient')"
        />
      </template>
      <template #field-description="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="description"
          type="text"
          :invalid="invalid"
          :placeholder="t('billing.addBillableItem.whatIsThisChargeFor')"
          data-testid="billing-add-billable-item-description"
        />
      </template>
      <template #field-amount>
        <UiNumberInput v-model="amount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-add-billable-item-amount" />
      </template>
      <template #field-currency>
        <UiSelect v-model="currency" :options="CURRENCY_OPTIONS" :placeholder="t('shared.currency')" />
      </template>
      <template #field-type>
        <UiSelect v-model="type" :options="BILLABLE_ITEM_TYPE_OPTIONS" :placeholder="t('shared.type')" data-testid="billing-add-billable-item-type" />
      </template>
      <template #field-nextDueDate>
        <UiDatePicker
          v-model="nextDueDate"
          :direction="UiDatePickerDirection.Future"
          :placeholder="t('shared.selectDate')"
          data-testid="billing-add-billable-item-next-due-date"
        />
      </template>
      <template #field-recurringPeriod>
        <UiSelect
          v-model="recurringPeriod"
          :options="RECURRING_PERIOD_OPTIONS"
          :placeholder="t('billing.addBillableItem.recurringPeriod')"
          data-testid="billing-add-billable-item-recurring-period"
        />
      </template>

      <UiFormActions>
        <UiLink :action="UiActionKind.Cancel" :to="AppRoute.BillingBillableItems" :variant="UiButtonVariant.Ghost" data-testid="billing-add-billable-item-cancel">
          {{ t('shared.cancel') }}
        </UiLink>
        <UiButton :action="UiActionKind.Add" type="submit" :variant="UiButtonVariant.Primary" :loading="store.loading" data-testid="billing-add-billable-item-submit">
          {{ t('shared.create') }}
        </UiButton>
      </UiFormActions>
    </UiSchemaForm>
  </UiStack>
</template>
