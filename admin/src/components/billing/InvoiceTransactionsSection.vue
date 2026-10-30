<script setup lang="ts">
/**
 * The two read-only ledgers every invoice tab ends with: the payments received against the
 * invoice, and the full history of payments, refunds and credits.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { transactionTypeTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { InvoiceTransactionType } from '@/types/invoice'
import type { InvoiceDetail, InvoiceTransaction } from '@/types/invoice'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose transactions are listed. */
  invoice: InvoiceDetail
}>()

/** Every transaction of the invoice; the API may omit the list on a brand-new invoice. */
const allTransactions = computed<InvoiceTransaction[]>(() => props.invoice.transactions ?? [])

/** Only the payments, for the "Transactions" ledger. */
const payments = computed<InvoiceTransaction[]>(() =>
  allTransactions.value.filter((transaction) => transaction.type === InvoiceTransactionType.Payment),
)

</script>

<template>
  <UiStack data-testid="billing-invoice-transactions">
    <UiCard padding="md">
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">
          {{ t('shared.transactions') }}
        </UiHeading>
        <UiTable
          density="compact"
          :empty="payments.length === 0"
          :colspan="5"
          data-testid="billing-invoice-payments-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.transactionId') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.amount') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.transactionFees') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="transaction in payments" :key="transaction.id" hoverable>
              <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
              <UiTableCell>{{ transaction.gateway }}</UiTableCell>
              <UiTableCell muted>{{ transaction.transactionId || '—' }}</UiTableCell>
              <UiTableCell>
                <UiText tone="success">{{ formatPlainAmount(transaction.amount, props.invoice.currency) }}</UiText>
              </UiTableCell>
              <UiTableCell muted>{{ formatPlainAmount(transaction.fees, props.invoice.currency) }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('shared.noRecordsFound') }}
          </template>
        </UiTable>
      </UiStack>
    </UiCard>

    <UiCard padding="md">
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">
          {{ t('shared.transactionHistory') }}
        </UiHeading>
        <UiTable
          density="compact"
          :empty="allTransactions.length === 0"
          :colspan="5"
          data-testid="billing-invoice-history-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.transactionId') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="transaction in allTransactions" :key="transaction.id" hoverable>
              <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
              <UiTableCell>{{ transaction.gateway }}</UiTableCell>
              <UiTableCell muted>{{ transaction.transactionId || '—' }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="transactionTypeTone(transaction.type)">{{ transaction.type }}</UiBadge>
              </UiTableCell>
              <UiTableCell muted>{{ transaction.notes || '—' }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('shared.noRecordsFound') }}
          </template>
        </UiTable>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
