<template>
  <UiCard data-testid="pay-summary">
    <UiHeading :level="2" size="subsection" class="text-center mb-5">{{ $t('invoicePay.invoiceNum', { id: invoice.id }) }}</UiHeading>

    <UiStack gap="sm" class="mb-4">
      <UiStack direction="row" justify="between">
        <UiHeading :level="3" size="label">{{ $t('client.invoices.colDescription') }}</UiHeading>
        <UiHeading :level="3" size="label">{{ $t('client.invoices.colAmount') }}</UiHeading>
      </UiStack>
      <UiKeyValue v-for="item in invoice.items" :key="item.id" :label="item.description" :value="money(item.amount)" />
    </UiStack>

    <UiStack gap="sm" class="border-t border-gray-200 dark:border-white/10 pt-3">
      <UiKeyValue :label="$t('client.invoices.subtotal')" :value="money(invoice.subTotal)" strong />
      <UiKeyValue :label="$t('client.invoices.creditApplied')" :value="money(invoice.credit)" />
      <UiKeyValue :label="$t('client.invoices.total')" :value="money(invoice.total)" strong />
    </UiStack>

    <UiKeyValue class="mt-6" :label="$t('invoicePay.paymentsToDate')" :value="money(received)" />

    <UiAlert variant="success" class="mt-4" data-testid="pay-balance-due">
      <UiKeyValue :label="$t('invoicePay.balanceDue')" :value="money(outstanding)" strong />
    </UiAlert>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PaySummary. The invoice recap beside the payment form: line items, totals, what has been
 * received and the balance still due. Every figure is formatted in the invoice's own currency.
 */
import { computed } from 'vue'
import type { ClientInvoice } from '~/types/clientinvoice'
import { formatCurrency } from '~/utils/formatCurrency'
import { balanceDue, paymentsToDate } from '~/utils/invoice'

/** Props of PaySummary. */
interface Props {
  /** The invoice being paid. */
  invoice: ClientInvoice
}

/** The props of the component. */
const props = defineProps<Props>()

/** How much has actually been received against this invoice: see `utils/invoice.ts`. */
const received = computed(() => paymentsToDate(props.invoice.transactions))

/** What is still owed: the total less everything received. */
const outstanding = computed(() => balanceDue(props.invoice))

/**
 * Formats one amount in the invoice's own billing currency.
 *
 * An invoice's currency is fixed at creation (`MyInvoiceDto.Currency`), not the account's current
 * one, so it is the authority.
 *
 * @param amount - The amount to format.
 * @returns The formatted amount, or an em dash when there is no figure.
 */
const money = (amount: string | number | null | undefined): string => formatCurrency(amount, { code: props.invoice.currency })
</script>
