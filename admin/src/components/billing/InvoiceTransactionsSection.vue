<script setup lang="ts">
/**
 * The two read-only ledgers every invoice tab ends with: the payments received against the
 * invoice, and the full history of payments, refunds and credits. While `invoice` is null (it is still being read) the
 * tables keep their headers and draw placeholder rows.
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
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { InvoiceTransactionType } from '@/types/invoice'
import type { InvoiceDetail, InvoiceTransaction } from '@/types/invoice'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose transactions are listed; null while it is being read. */
  invoice: InvoiceDetail | null
}>()

/** Every transaction of the invoice; the API may omit the list on a brand-new invoice. */
const allTransactions = computed<InvoiceTransaction[]>(() => props.invoice?.transactions ?? [])

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
          :density="UiTableDensity.Compact"
          :loading="invoice === null"
          :empty="invoice !== null && payments.length === 0"
          data-testid="billing-invoice-payments-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.transactionId') }}</UiTableHeader>
              <UiTableHeader numeric>{{ t('shared.amount') }}</UiTableHeader>
              <UiTableHeader numeric>{{ t('shared.transactionFees') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="transaction in payments" :key="transaction.id" hoverable>
              <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
              <UiTableCell>{{ transaction.gateway }}</UiTableCell>
              <UiTableCell muted>{{ transaction.transactionId || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell numeric>
                <UiText tone="success">{{ formatPlainAmount(transaction.amount, (invoice?.currency ?? '')) }}</UiText>
              </UiTableCell>
              <UiTableCell numeric muted>{{ formatPlainAmount(transaction.fees, (invoice?.currency ?? '')) }}</UiTableCell>
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
          :density="UiTableDensity.Compact"
          :loading="invoice === null"
          :empty="invoice !== null && allTransactions.length === 0"
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
              <UiTableCell muted>{{ transaction.transactionId || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="transactionTypeTone(transaction.type)">{{ transaction.type }}</UiBadge>
              </UiTableCell>
              <UiTableCell muted>{{ transaction.notes || EMPTY_VALUE }}</UiTableCell>
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
