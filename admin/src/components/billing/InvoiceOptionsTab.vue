<script setup lang="ts">
/**
 * Options tab of the invoice screen: invoice date, due date, payment method and the tax rate,
 * saved together. The outcome of a save (success, or the API's sentence) shows under the form.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { GATEWAY_OPTIONS } from '@/lib/constants'
import { toDateInputValue } from '@/lib/format'
import { InvoiceStatus } from '@/types/invoice'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail } from '@/types/invoice'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose options are edited. */
  invoice: InvoiceDetail
}>()

const store = useBillingStore()

/** Editable invoice date (ISO day). */
const invoiceDate = ref<string | null>(null)

/** Editable due date (ISO day). */
const dueDate = ref<string | null>(null)

/** Editable payment method. */
const paymentMethod = ref('')

/** Tax rate, percent. */
const taxRate = ref(0)

/** True while the options are being saved. */
const saving = ref(false)

/** The outcome line of the last save, shown under the form. */
const feedback = useOutcomeToast()

/** True when the invoice is a draft the client cannot see yet. */
const isDraft = computed<boolean>(() => props.invoice.status === InvoiceStatus.Draft)

/** Fills the form from the invoice. */
const populate = (): void => {
  invoiceDate.value = toDateInputValue(props.invoice.dates.invoiceDate)
  dueDate.value = toDateInputValue(props.invoice.dates.dueDate)
  paymentMethod.value = props.invoice.payment.method ?? GATEWAY_OPTIONS[0]?.value ?? ''
  taxRate.value = props.invoice.amounts.taxRate ?? 0
}

/**
 * Saves the options and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const save = async (): Promise<void> => {
  saving.value = true
  const ok = await store.updateOptions(props.invoice.id, {
    invoiceDate: invoiceDate.value ?? '',
    dueDate: dueDate.value ?? '',
    paymentMethod: paymentMethod.value,
    taxRate: taxRate.value,
  })
  saving.value = false
  if (ok) {
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

onMounted(populate)
</script>

<template>
  <UiStack data-testid="billing-invoice-options">
    <UiAlert v-if="isDraft" :variant="UiTone.Info" data-testid="billing-invoice-options-draft">
      {{ t('billing.invoiceOptionsTab.thisIsADraftInvoice') }}
    </UiAlert>

    <UiAlert v-if="invoice.reverseCharge" :variant="UiTone.Info" data-testid="billing-invoice-options-reverse-charge">
      {{ t('billing.invoiceOptionsTab.reverseCharge', { vatNumber: invoice.clientVatNumber ?? '' }) }}
    </UiAlert>

    <UiCard :padding="UiCardPadding.Md" container>
      <UiForm stacked data-testid="billing-invoice-options-form" @submit="save">
        <UiGrid :columns="2" container>
          <UiField :label="t('shared.invoiceDate')" :error="store.fieldErrors.invoiceDate">
            <UiDatePicker v-model="invoiceDate" :placeholder="t('shared.selectDate')" />
          </UiField>
          <UiField :label="t('shared.dueDate')" :error="store.fieldErrors.dueDate">
            <UiDatePicker v-model="dueDate" :direction="UiDatePickerDirection.Future" :placeholder="t('shared.selectDate')" />
          </UiField>
          <UiField :label="t('shared.paymentMethod')" :error="store.fieldErrors.paymentMethod">
            <UiSelect v-model="paymentMethod" :options="GATEWAY_OPTIONS" :placeholder="t('shared.paymentMethod')" />
          </UiField>
          <UiField :label="t('billing.invoiceOptionsTab.taxRate')" :error="store.fieldErrors.taxRate">
            <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center">
              <UiNumberInput v-model="taxRate" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-invoice-tax" />
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">%</UiText>
            </UiStack>
          </UiField>
        </UiGrid>
        <UiFormActions>
          <UiButton :action="UiActionKind.Save"
            :type="UiButtonType.Submit"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            data-testid="billing-invoice-options-save"
          >
            {{ t('shared.save') }}
          </UiButton>
        </UiFormActions>
      </UiForm>
    </UiCard>
  </UiStack>
</template>
