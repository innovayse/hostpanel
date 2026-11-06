<script setup lang="ts">
/**
 * The form that issues a credit note against a paid invoice: the lines credited, where the value goes (the client's credit
 * balance, or back to the payer through the gateway), and the reason the client reads on the note.
 *
 * A credit note is not an invoice credit, and the dialog says so. The tax on the note follows the invoice at the invoice's own
 * rate, so the sub total, the tax and the total are shown as typed and checked against what the invoice can still be credited. The
 * API repeats every check; this only stops the obvious mistakes before the call. Lines can start from the invoice's own lines, so
 * crediting the whole invoice is one click.
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { formatPlainAmount } from '@/lib/money'
import { CreditNoteMethod } from '@/types/creditnote'
import type { IssueCreditNotePayload } from '@/types/creditnote'
import type { InvoiceDetail } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiTextTone } from '@/utils/ui/text'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The props of the dialog. */
const props = defineProps<{
  /** The paid invoice being credited. */
  invoice: InvoiceDetail
  /** True while the issue request is in flight. */
  loading?: boolean
  /** Sentence of the last refused issue, shown in the dialog; null or empty when none. */
  error?: string | null
  /** The API's per-field messages of the last refused issue, keyed by field. */
  fieldErrors?: Record<string, string>
}>()

/** The events of the dialog. */
const emit = defineEmits<{
  /** The form is valid and confirmed; carries the request body. */
  submit: [payload: IssueCreditNotePayload]
  /** The dialog was cancelled or dismissed. */
  close: []
}>()

/** Longest reason the API accepts, mirrored here so the field stops typing at the limit. */
const REASON_MAX_LENGTH = 500

/** Longest line description the API accepts. */
const DESCRIPTION_MAX_LENGTH = 500

/** One editable line of the form: what is typed (a number field is null while it is empty) and a key the list renders by. */
interface FormLine {
  /** Stable key of the row, so removing one does not move the typed values of the others. */
  key: number
  /** What is credited. */
  description: string
  /** Credited price per unit, before tax; null while the field is empty. */
  unitPrice: number | null
  /** Number of units credited; null while the field is empty. */
  quantity: number | null
}

/** The next row key. */
let nextKey = 0

/**
 * Builds an empty line.
 *
 * @returns A row with no description, no price and one unit.
 */
const emptyLine = (): FormLine => ({ key: nextKey++, description: '', unitPrice: 0, quantity: 1 })

/** The lines being typed. */
const lines = reactive<FormLine[]>([emptyLine()])

/** Where the value goes. */
const method = ref<CreditNoteMethod>(CreditNoteMethod.AccountCredit)

/** Why the note is issued. */
const reason = ref('')

/** True once the form was submitted, so missing fields show their sentence. */
const submitted = ref(false)

/** Options of the method select. */
const methodOptions = computed(() => [
  { value: CreditNoteMethod.AccountCredit, label: t('billing.creditNotes.method.AccountCredit') },
  { value: CreditNoteMethod.GatewayRefund, label: t('billing.creditNotes.method.GatewayRefund') },
])

/**
 * Converts an amount to whole minor units (cents), so sums and comparisons are done on integers and a value such as 0.1 + 0.2
 * cannot be told apart from 0.3 by a floating point error.
 *
 * @param amount - The amount in the invoice currency.
 * @returns The amount in minor units, rounded to the nearest one.
 */
const toCents = (amount: number): number => Math.round(amount * 100)

/** Sum of the typed lines before tax, in whole cents (each line is rounded to cents before it is added). */
const subTotalCents = computed<number>(() =>
  lines.reduce((sum, line) => sum + toCents(Number(line.unitPrice || 0) * Number(line.quantity || 0)), 0),
)

/** Sum of the typed lines before tax. */
const subTotal = computed<number>(() => subTotalCents.value / 100)

/** Tax on the sub total at the invoice's own rate, in whole cents. */
const taxCents = computed<number>(() => Math.round((subTotalCents.value * props.invoice.amounts.taxRate) / 100))

/** Tax on the sub total at the invoice's own rate. */
const tax = computed<number>(() => taxCents.value / 100)

/** What the note would credit in all, in whole cents. */
const totalCents = computed<number>(() => subTotalCents.value + taxCents.value)

/** What the note would credit in all. */
const total = computed<number>(() => totalCents.value / 100)

/** What the invoice can still be credited. */
const creditable = computed<number>(() => props.invoice.creditableAmount)

/** True when the typed lines credit more than the invoice can still take; compared in whole cents, never as floats. */
const overCredit = computed<boolean>(() => totalCents.value > toCents(creditable.value))

/** True when every line has a description, a price above zero and at least one unit. */
const linesValid = computed<boolean>(() =>
  lines.every((line) => line.description.trim() !== '' && Number(line.unitPrice) > 0 && Number(line.quantity) >= 1),
)

/** True when the form can be sent. */
const canSubmit = computed<boolean>(() => linesValid.value && reason.value.trim() !== '' && total.value > 0 && !overCredit.value && !props.loading)

/** Sentence under the reason field once the form was submitted without one. */
const reasonError = computed<string>(() => {
  if (props.fieldErrors?.reason) {
    return props.fieldErrors.reason
  }
  return submitted.value && reason.value.trim() === '' ? t('billing.creditNotes.reasonRequired') : ''
})

/** Sentence above the lines when the typed lines are incomplete after a submit. */
const linesError = computed<string>(() => (submitted.value && !linesValid.value ? t('billing.creditNotes.linesInvalid') : ''))

/** Adds an empty line. */
const addLine = (): void => {
  lines.push(emptyLine())
}

/**
 * Removes a line; the last one cannot be removed, a note needs a line.
 *
 * @param key - Key of the row to remove.
 */
const removeLine = (key: number): void => {
  if (lines.length === 1) {
    return
  }
  const index = lines.findIndex((line) => line.key === key)
  if (index >= 0) {
    lines.splice(index, 1)
  }
}

/** Replaces the typed lines with the invoice's own lines, so the whole invoice is credited. */
const useInvoiceLines = (): void => {
  lines.splice(
    0,
    lines.length,
    ...props.invoice.items.map((item) => ({
      key: nextKey++,
      description: item.description,
      unitPrice: item.unitPrice,
      quantity: item.quantity,
    })),
  )
}

/** Sends the form when it is valid; otherwise shows what is missing. */
const submit = (): void => {
  submitted.value = true
  if (!canSubmit.value) {
    return
  }
  emit('submit', {
    items: lines.map((line) => ({ description: line.description.trim(), unitPrice: Number(line.unitPrice), quantity: Number(line.quantity) })),
    method: method.value,
    reason: reason.value.trim(),
  })
}
</script>

<template>
  <UiDialog
    :title="t('billing.creditNotes.issueTitle')"
    :size="UiDialogSize.Lg"
    data-testid="billing-credit-note-dialog"
    @close="emit('close')"
  >
    <UiStack>
      <UiAlert variant="info" data-testid="billing-credit-note-dialog-explain">
        {{ t('billing.creditNotes.explain') }}
      </UiAlert>

      <UiAlert v-if="error" variant="danger" data-testid="billing-credit-note-dialog-error">
        {{ error }}
      </UiAlert>

      <UiStack gap="sm">
        <UiStack direction="row" justify="between">
          <UiText :tone="UiTextTone.Primary">{{ t('billing.creditNotes.lines') }}</UiText>
          <UiButton size="sm" :variant="UiButtonVariant.Secondary" data-testid="billing-credit-note-use-invoice-lines" @click="useInvoiceLines">
            {{ t('billing.creditNotes.useInvoiceLines') }}
          </UiButton>
        </UiStack>
        <UiText v-if="linesError" :tone="UiTextTone.Danger" size="sm" data-testid="billing-credit-note-lines-error">{{ linesError }}</UiText>
        <UiGrid v-for="(line, index) in lines" :key="line.key" :columns="4" :data-testid="`billing-credit-note-line-${index}`">
          <UiInput
            v-model="line.description"
            :placeholder="t('billing.creditNotes.descriptionPlaceholder')"
            :maxlength="DESCRIPTION_MAX_LENGTH"
            :data-testid="`billing-credit-note-line-${index}-description`"
          />
          <UiNumberInput
            v-model="line.unitPrice"
            :min="0"
            :step="0.01"
            :placeholder="t('billing.creditNotes.pricePlaceholder')"
            :data-testid="`billing-credit-note-line-${index}-price`"
          />
          <UiNumberInput
            v-model="line.quantity"
            :min="1"
            :step="1"
            :placeholder="t('billing.creditNotes.quantityPlaceholder')"
            :data-testid="`billing-credit-note-line-${index}-quantity`"
          />
          <UiButton
            size="sm"
            :variant="UiButtonVariant.Secondary"
            :disabled="lines.length === 1"
            :data-testid="`billing-credit-note-line-${index}-remove`"
            @click="removeLine(line.key)"
          >
            {{ t('billing.creditNotes.removeLine') }}
          </UiButton>
        </UiGrid>
        <UiButton size="sm" :variant="UiButtonVariant.Secondary" data-testid="billing-credit-note-add-line" @click="addLine">
          {{ t('billing.creditNotes.addLine') }}
        </UiButton>
      </UiStack>

      <UiField :label="t('billing.creditNotes.methodLabel')" :hint="t('billing.creditNotes.methodHint')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="method"
            :options="methodOptions"
            :placeholder="t('billing.creditNotes.methodPlaceholder')"
            data-testid="billing-credit-note-method"
          />
        </template>
      </UiField>

      <UiField
        :label="t('billing.creditNotes.reasonLabel')"
        :hint="t('billing.creditNotes.reasonHint')"
        :error="reasonError"
        required
        error-testid="billing-credit-note-reason-error"
      >
        <template #default="{ id, invalid, describedby }">
          <UiTextarea
            :id="id"
            v-model="reason"
            rows="3"
            :maxlength="REASON_MAX_LENGTH"
            :invalid="invalid"
            :aria-describedby="describedby"
            :placeholder="t('billing.creditNotes.reasonPlaceholder')"
            data-testid="billing-credit-note-reason"
          />
        </template>
      </UiField>

      <UiStack gap="xs" data-testid="billing-credit-note-totals">
        <UiText>{{ t('billing.creditNotes.subTotal') }}: {{ formatPlainAmount(subTotal, invoice.currency) }}</UiText>
        <UiText>{{ t('billing.creditNotes.taxAt', { rate: invoice.amounts.taxRate }) }}: {{ formatPlainAmount(tax, invoice.currency) }}</UiText>
        <UiText :tone="UiTextTone.Primary" data-testid="billing-credit-note-total">
          {{ t('billing.creditNotes.total') }}: {{ formatPlainAmount(total, invoice.currency) }}
        </UiText>
        <UiText :tone="UiTextTone.Muted" size="sm" data-testid="billing-credit-note-creditable">
          {{ t('billing.creditNotes.creditable', { amount: formatPlainAmount(creditable, invoice.currency) }) }}
        </UiText>
        <UiText v-if="overCredit" :tone="UiTextTone.Danger" size="sm" data-testid="billing-credit-note-over">
          {{ t('billing.creditNotes.overCredit') }}
        </UiText>
      </UiStack>
    </UiStack>

    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="billing-credit-note-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton :loading="loading" :disabled="loading" data-testid="billing-credit-note-submit" @click="submit">
        {{ t('billing.creditNotes.issue') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
