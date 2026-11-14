<script setup lang="ts">
/**
 * Billing transactions ledger: the totals of the whole ledger, a net-revenue chart of the page, and the page's
 * transactions, narrowed as the operator types by the search box (description, client, gateway reference) and by
 * a filter dialog (direction, payment method, date range, amount) whose applied filters show as chips. The
 * filters, the search text and the page live in the address (`useFilterQuery`). Rows can be edited or deleted; a
 * delete is confirmed in a danger prompt and its outcome (success, or the API's sentence) shows above the table.
 * A refused load offers a retry.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import TransactionChart from '@/components/billing/TransactionChart.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import UiCard from '@/components/ui/UiCard.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiCardRole } from '@/utils/ui/table'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'
import { formatDate, toIsoDay, EMPTY_VALUE } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useTransactionsStore } from '@/stores/billingTransactions'
import type { Transaction } from '@/types/transaction'
import { AppRoute, transactionEditPath } from '@/lib/routes'
import type { UiFilterChip } from '@/utils/ui/filter'

const { t } = useI18n()

/** Which side of the ledger the list shows; an empty filter value means both. */
const Direction = {
  In: 'in',
  Out: 'out',
} as const

/** The filter fields kept in the address, beside the search text and the page. */
const FilterKey = {
  Direction: 'direction',
  Method: 'method',
  From: 'from',
  To: 'to',
  Amount: 'amount',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

const store = useTransactionsStore()

/** The filters, the search text and the page of the list, as the address holds them. */
const filters = useFilterQuery<FilterKey>([
  FilterKey.Direction,
  FilterKey.Method,
  FilterKey.From,
  FilterKey.To,
  FilterKey.Amount,
])

/** Transaction waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/** Options of the direction select. */
const directionOptions = computed(() => [
  { value: '', label: t('billing.transactionsList.allActivity') },
  { value: Direction.In, label: t('billing.transactionsList.paymentsReceived') },
  { value: Direction.Out, label: t('billing.transactionsList.paymentsSent') },
])

/** Options of the payment-method select: "any" plus the shared methods. */
const methodOptions = computed(() => [{ value: '', label: t('shared.any') }, ...TRANSACTION_METHOD_OPTIONS])

/** The date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(filters.draft, FilterKey.From, FilterKey.To)

/** The amount of the filter draft as a number; 0 means any. The address keeps it as text. */
const draftAmount = computed<number>({
  /**
   * Reads the draft's amount as a number.
   *
   * @returns The amount, 0 when none is set.
   */
  get: () => Number(filters.draft.value.amount) || 0,
  /**
   * Writes the amount back into the draft as text, empty for none.
   *
   * @param amount - The amount typed; 0 or less clears it.
   */
  set: (amount) => {
    filters.draft.value.amount = amount > 0 ? String(amount) : ''
  },
})

/** The applied filters as chips, in the order they are shown. */
const chips = computed<UiFilterChip<FilterKey>[]>(() => {
  const applied = filters.applied.value
  const result: UiFilterChip<FilterKey>[] = []
  if (applied.direction !== '') {
    const label = directionOptions.value.find(option => option.value === applied.direction)?.label ?? applied.direction
    result.push({ key: FilterKey.Direction, label: t('billing.transactionsList.show'), value: label })
  }
  if (applied.method !== '') {
    const label = methodOptions.value.find(option => option.value === applied.method)?.label ?? applied.method
    result.push({ key: FilterKey.Method, label: t('shared.paymentMethod'), value: label })
  }
  if (applied.from !== '') {
    result.push({ key: FilterKey.From, label: t('billing.invoicesList.dateFrom'), value: formatDate(applied.from) })
  }
  if (applied.to !== '') {
    result.push({ key: FilterKey.To, label: t('billing.invoicesList.dateTo'), value: formatDate(applied.to) })
  }
  if (applied.amount !== '') {
    result.push({ key: FilterKey.Amount, label: t('shared.amount'), value: applied.amount })
  }
  return result
})

/**
 * Tells whether a transaction passes the search text and the applied filters.
 *
 * @param transaction - A transaction of the loaded page.
 * @returns True when the transaction matches every filter that is set and contains the search text.
 */
const matches = (transaction: Transaction): boolean => {
  const applied = filters.applied.value
  const term = filters.search.value.trim().toLowerCase()
  const searchable = [transaction.description, transaction.clientName, transaction.transactionId ?? '']
  if (term !== '' && !searchable.some(field => field.toLowerCase().includes(term))) {
    return false
  }
  if (applied.direction === Direction.In && !(transaction.amountIn > 0)) {
    return false
  }
  if (applied.direction === Direction.Out && !(transaction.amountOut > 0)) {
    return false
  }
  if (applied.method !== '' && transaction.paymentMethod !== applied.method) {
    return false
  }
  const day = toIsoDay(new Date(transaction.date))
  if ((applied.from !== '' && day < applied.from) || (applied.to !== '' && day > applied.to)) {
    return false
  }
  const amount = Number(applied.amount)
  return !(amount > 0 && transaction.amountIn !== amount && transaction.amountOut !== amount)
}

/** The loaded page, narrowed by the search text and the filters. */
const shownTransactions = computed<Transaction[]>(() => store.transactions.filter(matches))

/**
 * Formats an amount for a table cell, a dash when there is none.
 *
 * @param value - The amount.
 * @param currency - ISO 4217 code the amount is in.
 * @returns The amount with its currency, or a dash for zero.
 */
const cell = (value: number, currency: string): string => (value > 0 ? formatPlainAmount(value, currency) : EMPTY_VALUE)

/**
 * Loads the page of the ledger named by the address.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchAll(filters.page.value, store.pageSize)

/** The outcome line of the last delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Deletes the transaction that waits for confirmation (the store reloads the page) and shows the outcome. */
const confirmDelete = async (): Promise<void> => {
  const id = pendingDeleteId.value
  if (id === null) {
    return
  }
  feedback.clear()
  const deleted = await store.remove(id)
  pendingDeleteId.value = null
  if (deleted) {
    feedback.show(t('shared.recordDeleted'), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!store.loading) {
    pendingDeleteId.value = null
  }
}

/**
 * The row actions of a transaction: edit as a real link, and delete last (confirmed first).
 *
 * @param id - The transaction id.
 * @returns The action list passed to UiRowActions.
 */
const transactionActions = (id: number): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `billing-transaction-edit-${id}`,
    to: transactionEditPath(id),
  },
  {
    label: t('shared.delete'),
    testid: `billing-transaction-delete-${id}`,
    danger: true,
    /**
     * Asks for confirmation before the transaction is deleted.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      pendingDeleteId.value = id
    },
  },
]

// The address is the source of truth: the first load and every change of the page read the ledger.
watch(filters.page, load, { immediate: true })
</script>

<template>
  <UiStack data-testid="billing-transactions-view">
    <UiPageHeader :title="t('shared.transactions')" :subtitle="t('billing.transactionsList.paymentTransactionsAndRecords')">
      <template #actions>
        <UiLink :to="AppRoute.BillingAddTransaction" variant="primary" data-testid="billing-transactions-add">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('billing.transactionsList.addTransaction') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiGrid :columns="4">
      <UiStatTile :label="t('billing.transactionsList.totalIncome')" :value="formatPlainAmount(store.summary.totalIn, store.summary.currency)" tone="success" data-testid="billing-transactions-total-in" />
      <UiStatTile :label="t('billing.transactionsList.totalExpenditure')" :value="formatPlainAmount(store.summary.totalOut, store.summary.currency)" tone="danger" data-testid="billing-transactions-total-out" />
      <UiStatTile :label="t('billing.transactionsList.totalFees')" :value="formatPlainAmount(store.summary.totalFees, store.summary.currency)" data-testid="billing-transactions-total-fees" />
      <UiStatTile :label="t('shared.balance')" :value="formatPlainAmount(store.summary.balance, store.summary.currency)" tone="primary" data-testid="billing-transactions-balance" />
    </UiGrid>

    <UiListToolbar
      v-model:search="filters.search.value"
      search-testid="billing-transactions-search"
      :active-count="filters.activeCount.value"
      :chips="chips"
      @apply="filters.apply"
      @reset="filters.reset"
      @cancel="filters.cancel"
      @remove="filters.remove"
      @clear="filters.clearAll"
    >
      <UiStack gap="md">
        <UiField :label="t('billing.transactionsList.show')">
          <UiSelect v-model="filters.draft.value.direction" :options="directionOptions" :placeholder="t('billing.transactionsList.show')" />
        </UiField>
        <UiField :label="t('shared.paymentMethod')">
          <UiSelect v-model="filters.draft.value.method" :options="methodOptions" :placeholder="t('shared.paymentMethod')" />
        </UiField>
        <UiField :label="t('shared.dateRange')">
          <UiDateRangePicker v-model="draftRange" :placeholder="t('shared.selectDateRange')" />
        </UiField>
        <UiField :label="t('shared.amount')">
          <UiNumberInput v-model="draftAmount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-transactions-filter-amount" />
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-transactions-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-transactions-error"
      @retry="load"
    />

    <UiCard padding="md">
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">
          {{ t('billing.transactionsList.netRevenueUsd') }}
        </UiHeading>
        <TransactionChart :transactions="shownTransactions" />
      </UiStack>
    </UiCard>

    <UiText tone="muted" size="md" data-testid="billing-transactions-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.transactions.length === 0"
        :empty="!store.loading && shownTransactions.length === 0"
        data-testid="billing-transactions-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader :card-role="UiCardRole.Title">{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amountIn') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.fees') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amountOut') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="transaction in shownTransactions" :key="transaction.id" hoverable>
            <UiTableCell>{{ transaction.clientName }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
            <UiTableCell>{{ transaction.paymentMethod || t('billing.transactionsList.manual') }}</UiTableCell>
            <UiTableCell>{{ transaction.description }}</UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="transaction.amountIn > 0 ? 'success' : 'muted'">{{ cell(transaction.amountIn, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(transaction.fees, transaction.currency) }}</UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="transaction.amountOut > 0 ? 'danger' : 'muted'">{{ cell(transaction.amountOut, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="transactionActions(transaction.id)" :testid="`billing-transaction-actions-${transaction.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('billing.transactionsList.noTransactionsFound')" data-testid="billing-transactions-empty" />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="filters.page.value"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="billing-transactions-pagination"
      @update:page="filters.setPage"
    />

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="pendingDeleteId !== null"
      :title="t('billing.transactionsList.deleteTransaction')"
      :message="t('billing.transactionsList.deleteTransactionMessage', { id: pendingDeleteId ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading="store.loading"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
