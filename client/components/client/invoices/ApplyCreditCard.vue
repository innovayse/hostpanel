<template>
  <UiCard v-if="visible" data-testid="apply-credit-card">
    <UiForm :error="store.applyError" @submit="submit">
      <UiStack gap="md">
        <UiStack gap="xs">
          <UiHeading :level="2" size="subsection">{{ $t('client.credit.apply.title') }}</UiHeading>
          <UiText size="sm" tone="muted" data-testid="apply-credit-available">
            {{ $t('client.credit.apply.available', { balance: money(store.balance) }) }}
          </UiText>
        </UiStack>

        <UiInput
          v-model="text"
          type="text"
          :label="$t('client.credit.apply.amount')"
          :placeholder="$t('client.credit.apply.amountPlaceholder')"
          :error="amountError"
          autocomplete="off"
          testid="apply-credit-amount"
          @blur="touched = true"
        />

        <UiStack direction="row" gap="sm" justify="end">
          <UiButton variant="subtle" type="button" data-testid="apply-credit-max" @click="useMost">
            {{ $t('client.credit.apply.useMost') }}
          </UiButton>
          <UiButton type="submit" :loading="store.applyBusy" :disabled="store.applyBusy" data-testid="apply-credit-submit">
            {{ $t('client.credit.apply.submit') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ApplyCreditCard: pays part or all of an invoice from the client's credit balance.
 *
 * Shown only while there is something to pay with and something to pay: the invoice owes money, is not itself a credit top-up (credit
 * cannot pay for credit), and the balance is above zero. The most that can be applied is the smaller of the balance and what the
 * invoice still owes; the backend repeats every check. After a successful application the invoice is read again by the page
 * through the `applied` event, because its total and its status have changed.
 */
import { computed, onMounted, ref } from 'vue'
import { useCreditStore } from '~/stores/credit'
import type { ClientInvoice } from '~/types/clientinvoice'
import { formatCurrency } from '~/utils/formatCurrency'
import { balanceDue, isInvoiceOutstanding } from '~/utils/invoice'

/** Props of ApplyCreditCard. */
interface Props {
  /** The invoice credit would be applied to. */
  invoice: ClientInvoice
}

/** The props of the component. */
const props = defineProps<Props>()

/** Events of ApplyCreditCard. */
const emit = defineEmits<{
  /** Credit was applied; the invoice on screen is out of date. */
  applied: []
}>()

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The credit store: the balance and the application of credit. */
const store = useCreditStore()

/** What the client typed. */
const text = ref('')

/** Whether the field has been left once. */
const touched = ref(false)

// The balance is read when the card appears, so a page that never shows the card never asks for it.
onMounted(async () => {
  if (!store.credit) {
    await store.load(1)
  }
})

/** The most credit that can be applied: the smaller of the balance and what the invoice still owes. */
const most = computed<number>(() => Math.min(store.balance, balanceDue(props.invoice)))

/** Whether the card is shown: the invoice owes money, is no top-up, and there is credit to spend. */
const visible = computed<boolean>(() =>
  !props.invoice.isCreditTopUp && isInvoiceOutstanding(props.invoice) && store.hasCredit)

/** What the client typed, as a number; NaN when it is not a plain amount. */
const amount = computed<number>(() => {
  const trimmed = text.value.trim().replace(',', '.')
  return /^\d+(\.\d{1,2})?$/.test(trimmed) ? Number(trimmed) : Number.NaN
})

/** Whether the typed amount can be applied. */
const valid = computed<boolean>(() => amount.value > 0 && amount.value <= most.value)

/** The sentence under the field; empty while the field is untouched or valid. */
const amountError = computed<string>(() =>
  touched.value && !valid.value ? t('client.credit.apply.amountInvalid', { max: money(most.value) }) : '')

/**
 * Formats an amount in the invoice's currency.
 *
 * @param value - The amount.
 * @returns The formatted amount.
 */
const money = (value: number): string => formatCurrency(value, { code: props.invoice.currency })

/** Fills the field with the most that can be applied. */
const useMost = (): void => {
  text.value = String(most.value)
}

/** Applies the typed amount when it is allowed; otherwise shows why not. */
const submit = async (): Promise<void> => {
  touched.value = true
  if (!valid.value) {
    return
  }
  if (await store.applyToInvoice(props.invoice.id, amount.value)) {
    text.value = ''
    touched.value = false
    emit('applied')
  }
}
</script>
