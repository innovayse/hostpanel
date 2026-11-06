<template>
  <UiCard data-testid="credit-add-form">
    <UiForm :error="error" @submit="submit">
      <UiStack gap="md">
        <UiText size="sm" tone="muted" data-testid="credit-add-limits">
          {{ $t('client.credit.add.limits', { min: money(minimum), max: money(maximum) }) }}
        </UiText>

        <UiInput
          v-model="text"
          type="text"
          :label="$t('client.credit.add.amount')"
          :placeholder="$t('client.credit.add.amountPlaceholder')"
          :error="amountError"
          autocomplete="off"
          testid="credit-add-amount"
          @blur="touched = true"
        />

        <UiText size="sm" tone="muted">{{ $t('client.credit.add.note') }}</UiText>

        <UiStack direction="row" gap="sm" justify="end">
          <UiButton :to="CLIENT_CREDIT_PATH" variant="subtle" data-testid="credit-add-cancel">{{ $t('client.credit.add.cancel') }}</UiButton>
          <UiButton type="submit" :loading="busy" :disabled="busy" data-testid="credit-add-submit">
            {{ $t('client.credit.add.submit') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * AddFundsForm - the amount a client wants to add to their credit balance.
 *
 * The limits are the admin's and arrive with the credit; the form checks the amount against them before it asks, and the
 * backend checks again, so a stale page cannot add less or more than allowed. Submitting emits the amount; the page raises the
 * top-up invoice and sends the client to pay it.
 */
import { computed, ref } from 'vue'
import { CLIENT_CREDIT_PATH } from '~/constants/clientRoutes'
import { formatCurrency } from '~/utils/formatCurrency'

/** Props of AddFundsForm. */
interface Props {
  /** The least the client may add. */
  minimum: number
  /** The most the client may add. */
  maximum: number
  /** ISO 4217 code of the client's billing currency. */
  currency: string
  /** Whether the invoice is being raised. */
  busy?: boolean
  /** The backend's sentence for a refused top-up; empty when there is none. */
  error?: string
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), { busy: false, error: '' })

/** Events of AddFundsForm. */
const emit = defineEmits<{
  /** The client asked to add this amount. */
  submit: [amount: number]
}>()

/** Translates a key into the active locale. */
const { t } = useI18n()

/** What the client typed. */
const text = ref('')

/** Whether the field has been left once, so an empty field is not an error before the client has touched it. */
const touched = ref(false)

/** What the client typed, as a number; NaN when it is not a plain amount. */
const amount = computed<number>(() => {
  const trimmed = text.value.trim().replace(',', '.')
  return /^\d+(\.\d{1,2})?$/.test(trimmed) ? Number(trimmed) : Number.NaN
})

/** Whether the typed amount is inside the limits. */
const valid = computed<boolean>(() => amount.value >= props.minimum && amount.value <= props.maximum)

/** The sentence under the field; empty while the field is untouched or valid. */
const amountError = computed<string>(() =>
  touched.value && !valid.value
    ? t('client.credit.add.amountInvalid', { min: money(props.minimum), max: money(props.maximum) })
    : '')

/**
 * Formats an amount in the client's billing currency.
 *
 * @param value - The amount.
 * @returns The formatted amount.
 */
const money = (value: number): string => formatCurrency(value, { code: props.currency })

/** Emits the amount when it is inside the limits; otherwise shows why not. */
const submit = (): void => {
  touched.value = true
  if (valid.value) {
    emit('submit', amount.value)
  }
}
</script>
