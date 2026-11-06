<template>
  <UiCard data-testid="bank-transfer-card">
    <UiStack gap="md">
      <UiStack direction="row" justify="between" align="center" wrap>
        <UiHeading :level="2" size="subsection">{{ $t('invoicePay.bankTransfer.heading') }}</UiHeading>
        <UiBadge variant="warning" rounded data-testid="bank-transfer-status">{{ $t('invoicePay.bankTransfer.awaiting') }}</UiBadge>
      </UiStack>

      <UiText size="sm" tone="muted">{{ $t('invoicePay.bankTransfer.intro') }}</UiText>

      <!-- The reference is the one thing the payer must not get wrong, so it stands apart from the other fields. -->
      <UiAlert variant="info" data-testid="bank-transfer-reference-box">
        <UiStack direction="row" justify="between" align="center" gap="sm">
          <UiStack gap="xs">
            <UiText size="sm" tone="muted">{{ $t('invoicePay.bankTransfer.reference') }}</UiText>
            <UiText as="span" weight="bold" data-testid="bank-transfer-reference">{{ details.reference }}</UiText>
          </UiStack>
          <UiCopyButton
            :value="details.reference"
            :label="$t('invoicePay.bankTransfer.copy', { field: $t('invoicePay.bankTransfer.reference') })"
            :copied-label="$t('invoicePay.bankTransfer.copied')"
            testid="bank-transfer-copy-reference"
          />
        </UiStack>
        <UiText size="sm" tone="muted" class="mt-2">{{ $t('invoicePay.bankTransfer.referenceHint') }}</UiText>
      </UiAlert>

      <UiStack gap="sm">
        <UiStack v-for="row in rows" :key="row.key" direction="row" justify="between" align="center" gap="sm" :data-testid="`bank-transfer-row-${row.key}`">
          <UiStack gap="none">
            <UiText size="sm" tone="muted">{{ row.label }}</UiText>
            <UiText as="span" weight="medium" :data-testid="`bank-transfer-${row.key}`">{{ row.value }}</UiText>
          </UiStack>
          <UiCopyButton
            :value="row.copyValue"
            :label="$t('invoicePay.bankTransfer.copy', { field: row.label })"
            :copied-label="$t('invoicePay.bankTransfer.copied')"
            :testid="`bank-transfer-copy-${row.key}`"
          />
        </UiStack>
      </UiStack>

      <!-- Nothing to send the money to: say so, so the payer asks the provider instead of guessing. -->
      <UiAlert v-if="hasNoAccountDetails" variant="warning" data-testid="bank-transfer-no-details">
        <UiText as="span" size="sm">{{ $t('invoicePay.bankTransfer.noDetails') }}</UiText>
      </UiAlert>

      <UiAlert v-if="details.instructions" variant="info" data-testid="bank-transfer-instructions">
        <UiText as="span" size="sm" class="whitespace-pre-line">{{ details.instructions }}</UiText>
      </UiAlert>

      <UiText size="sm" tone="muted">{{ $t('invoicePay.bankTransfer.afterwards') }}</UiText>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * BankTransferCard - the bank details of a manual bank transfer, each with a copy button, the reference to write on the
 * transfer shown prominently, and the "awaiting payment confirmation" status.
 *
 * Shown on the payment page and the invoice page while the transfer waits for staff to confirm it. The reference is the invoice
 * number, never an internal id. A detail the operator left empty is not shown; the amount is always shown, as digits only in
 * its copy button so a bank form accepts it. When the operator left every detail empty the card says to contact the provider
 * for the account to pay into, since the amount and reference alone cannot be paid.
 */
import { computed } from 'vue'
import type { BankTransferDetails } from '~/types/banktransferdetails'
import { formatCurrency } from '~/utils/formatCurrency'

/** One line of the details: what it is, what is shown and what the copy button copies. */
interface DetailRow {
  /** Stable key, used in the test ids. */
  key: string
  /** Caption of the line. */
  label: string
  /** The value as shown. */
  value: string
  /** The value as copied. */
  copyValue: string
}

/** Props of BankTransferCard. */
interface Props {
  /** The details the API sent for this invoice. */
  details: BankTransferDetails
}

/** The props of the component. */
const props = defineProps<Props>()

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The lines to show: the amount, then each bank detail the operator filled in. */
const rows = computed<DetailRow[]>(() => {
  const { details } = props
  const lines: DetailRow[] = [
    {
      key: 'amount',
      label: t('invoicePay.bankTransfer.amount'),
      value: formatCurrency(details.amountDue, { code: details.currency }),
      copyValue: details.amountDue.toFixed(2)
    }
  ]
  const optional: Array<[string, string, string | null]> = [
    ['accountName', t('invoicePay.bankTransfer.accountName'), details.accountName],
    ['bankName', t('invoicePay.bankTransfer.bankName'), details.bankName],
    ['iban', t('invoicePay.bankTransfer.iban'), details.iban],
    ['swift', t('invoicePay.bankTransfer.swift'), details.swift]
  ]
  for (const [key, label, value] of optional) {
    if (value) {
      lines.push({ key, label, value, copyValue: value })
    }
  }
  return lines
})

/** Whether the operator gave no account detail and no instructions, so the card holds only the amount and the reference. */
const hasNoAccountDetails = computed<boolean>(() => {
  const { accountName, bankName, iban, swift, instructions } = props.details
  return !accountName && !bankName && !iban && !swift && !instructions
})
</script>
