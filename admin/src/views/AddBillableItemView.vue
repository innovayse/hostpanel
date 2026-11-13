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
import UiCard from '@/components/ui/UiCard.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { BILLABLE_ITEM_TYPE_OPTIONS, CURRENCY_OPTIONS, RECURRING_PERIOD_OPTIONS } from '@/lib/constants'
import { toIsoDay } from '@/lib/format'
import { useBillableItemsStore } from '@/stores/billableItems'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { CurrencyCode } from '@/types/currency'
import { AppRoute } from '@/lib/routes'

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

/** Label of the due date field: the invoice day of a one-time charge, the next due date of a recurring one. */
const dueDateLabel = computed<string>(() =>
  isRecurring.value ? t('billing.addBillableItem.nextDueDate') : t('billing.addBillableItem.invoiceOn'),
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
    <UiPageHeader :title="t('shared.addBillableItem')" :subtitle="t('billing.addBillableItem.createANewBillableItem')" />

    <UiCard padding="lg" class="max-w-3xl">
      <UiForm stacked @submit="submit">
        <UiAlert v-if="store.error" variant="error" data-testid="billing-add-billable-item-error">
          {{ store.error }}
        </UiAlert>

        <UiField :label="t('shared.client')" :error="store.fieldErrors.clientId">
          <UiClientSelect
            v-model="clientId"
            :clients="clients"
            :placeholder="t('shared.selectAClient')"
          />
        </UiField>

        <UiField :label="t('shared.description')" :error="store.fieldErrors.description">
          <template #default="{ id, invalid }">
            <UiInput
              :id="id"
              v-model="description"
              type="text"
              :invalid="invalid"
              :placeholder="t('billing.addBillableItem.whatIsThisChargeFor')"
              data-testid="billing-add-billable-item-description"
            />
          </template>
        </UiField>

        <UiGrid :columns="2">
          <UiField :label="t('shared.amount')" :error="store.fieldErrors.amount">
            <UiNumberInput v-model="amount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-add-billable-item-amount" />
          </UiField>
          <UiField :label="t('shared.currency')" :error="store.fieldErrors.currency">
            <UiSelect v-model="currency" :options="CURRENCY_OPTIONS" :placeholder="t('shared.currency')" />
          </UiField>
          <UiField :label="t('shared.type')" :error="store.fieldErrors.type">
            <UiSelect v-model="type" :options="BILLABLE_ITEM_TYPE_OPTIONS" :placeholder="t('shared.type')" data-testid="billing-add-billable-item-type" />
          </UiField>
          <UiField :label="dueDateLabel" :error="store.fieldErrors.nextDueDate" data-testid="billing-add-billable-item-next-due-date-field">
            <UiDatePicker
              v-model="nextDueDate"
              :direction="UiDatePickerDirection.Future"
              :placeholder="t('shared.selectDate')"
              data-testid="billing-add-billable-item-next-due-date"
            />
          </UiField>
        </UiGrid>

        <UiGrid v-if="isRecurring" :columns="2">
          <UiField :label="t('billing.addBillableItem.recurringPeriod')" :error="store.fieldErrors.recurringPeriod">
            <UiSelect
              v-model="recurringPeriod"
              :options="RECURRING_PERIOD_OPTIONS"
              :placeholder="t('billing.addBillableItem.recurringPeriod')"
              data-testid="billing-add-billable-item-recurring-period"
            />
          </UiField>
        </UiGrid>

        <UiFormActions>
          <UiLink :to="AppRoute.BillingBillableItems" variant="ghost" data-testid="billing-add-billable-item-cancel">
            {{ t('shared.cancel') }}
          </UiLink>
          <UiButton type="submit" variant="primary" :loading="store.loading" data-testid="billing-add-billable-item-submit">
            {{ t('shared.addBillableItem') }}
          </UiButton>
        </UiFormActions>
      </UiForm>
    </UiCard>
  </UiStack>
</template>
