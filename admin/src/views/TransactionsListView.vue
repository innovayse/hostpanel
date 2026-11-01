<script setup lang="ts">
/**
 * Billing transactions ledger: the totals of the whole ledger, a net-revenue chart of the page,
 * and the page's transactions, narrowed as the operator types by direction, payment method, date
 * range, description, amount and gateway reference. Rows can be edited or deleted; a delete is
 * confirmed in a danger prompt and its outcome (success, or the API's sentence) shows above the
 * table. A refused load offers a retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import TransactionChart from '@/components/billing/TransactionChart.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'
import { formatDate, toIsoDay } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { useTransactionsStore } from '@/stores/billingTransactions'
import type { Transaction } from '@/types/transaction'
import { AppRoute, transactionEditPath } from '@/lib/routes'

const { t } = useI18n()

/** Which side of the ledger the list shows. */
const Direction = {
  All: 'all',
  In: 'in',
  Out: 'out',
} as const

const store = useTransactionsStore()

/** True while the filter card is open. */
const filtersOpen = ref(false)

/** Direction filter. */
const filterDirection = ref<string>(Direction.All)

/** Payment-method filter; empty means any. */
const filterMethod = ref('')

/** Date-range filter. */
const filterDateRange = ref<[string, string] | null>(null)

/** Description filter. */
const filterDescription = ref('')

/** Amount filter; 0 means any. */
const filterAmount = ref(0)

/** Gateway-reference filter. */
const filterReference = ref('')

/** Transaction waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/** Options of the direction select. */
const directionOptions = computed(() => [
  { value: Direction.All, label: t('billing.transactionsList.allActivity') },
  { value: Direction.In, label: t('billing.transactionsList.paymentsReceived') },
  { value: Direction.Out, label: t('billing.transactionsList.paymentsSent') },
])

/** Options of the payment-method select: "any" plus the shared methods. */
const methodOptions = computed(() => [{ value: '', label: t('shared.any') }, ...TRANSACTION_METHOD_OPTIONS])

/**
 * Tells whether a transaction passes the filter card.
 *
 * @param transaction - A transaction of the loaded page.
 * @returns True when the transaction matches every filter that is set.
 */
const matches = (transaction: Transaction): boolean => {
  if (filterDirection.value === Direction.In && !(transaction.amountIn > 0)) {
    return false
  }
  if (filterDirection.value === Direction.Out && !(transaction.amountOut > 0)) {
    return false
  }
  if (filterMethod.value !== '' && transaction.paymentMethod !== filterMethod.value) {
    return false
  }
  if (filterDateRange.value !== null) {
    const day = toIsoDay(new Date(transaction.date))
    if (day < filterDateRange.value[0] || day > filterDateRange.value[1]) {
      return false
    }
  }
  if (
    filterDescription.value !== ''
    && !transaction.description.toLowerCase().includes(filterDescription.value.toLowerCase())
  ) {
    return false
  }
  if (filterAmount.value > 0 && transaction.amountIn !== filterAmount.value && transaction.amountOut !== filterAmount.value) {
    return false
  }
  return filterReference.value === ''
    || (transaction.transactionId ?? '').toLowerCase().includes(filterReference.value.toLowerCase())
}

/** The loaded page, narrowed by the filter card. */
const shownTransactions = computed<Transaction[]>(() => store.transactions.filter(matches))

/**
 * Formats an amount for a table cell, a dash when there is none.
 *
 * @param value - The amount.
 * @param currency - ISO 4217 code the amount is in.
 * @returns The amount with its currency, or a dash for zero.
 */
const cell = (value: number, currency: string): string => (value > 0 ? formatPlainAmount(value, currency) : '—')

/** Clears every filter. */
const clearFilters = (): void => {
  filterDirection.value = Direction.All
  filterMethod.value = ''
  filterDateRange.value = null
  filterDescription.value = ''
  filterAmount.value = 0
  filterReference.value = ''
}

/**
 * Loads a page of the ledger.
 *
 * @param next - Page number (1-based).
 *
 * @returns Resolves when the request has settled.
 */
const load = (next: number): Promise<void> => store.fetchAll(next, store.pageSize)

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

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-transactions-view">
    <UiPageHeader :title="t('shared.transactions')" :subtitle="t('billing.transactionsList.paymentTransactionsAndRecords')">
      <template #actions>
        <UiButton variant="secondary" data-testid="billing-transactions-filter-toggle" @click="filtersOpen = !filtersOpen">
          {{ filtersOpen ? t('shared.hideFilters') : t('shared.searchFilter') }}
        </UiButton>
        <UiLink :to="AppRoute.BillingAddTransaction" variant="primary" data-testid="billing-transactions-add">
          <UiIcon :markup="Icon.Add" size="sm" />
          {{ t('billing.transactionsList.addTransaction') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiFilterCard v-if="filtersOpen">
      <UiGrid :columns="3" container class="items-end">
        <UiField :label="t('billing.transactionsList.show')">
          <UiSelect v-model="filterDirection" :options="directionOptions" :placeholder="t('billing.transactionsList.show')" />
        </UiField>
        <UiField :label="t('shared.paymentMethod')">
          <UiSelect v-model="filterMethod" :options="methodOptions" :placeholder="t('shared.paymentMethod')" />
        </UiField>
        <UiField :label="t('shared.dateRange')">
          <UiDateRangePicker v-model="filterDateRange" :placeholder="t('shared.selectDateRange')" />
        </UiField>
        <UiField :label="t('shared.description')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="filterDescription"
              type="text"
              :placeholder="t('shared.search')"
              data-testid="billing-transactions-filter-description"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.amount')">
          <UiNumberInput v-model="filterAmount" :step="0.01" :min="0" placeholder="0.00" />
        </UiField>
        <UiField :label="t('shared.transactionId')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="filterReference"
              type="text"
              :placeholder="t('shared.search')"
            />
          </template>
        </UiField>
      </UiGrid>
      <template #actions>
        <UiStack direction="row" justify="center">
          <UiButton variant="ghost" data-testid="billing-transactions-clear" @click="clearFilters">
            {{ t('shared.clear') }}
          </UiButton>
        </UiStack>
      </template>
    </UiFilterCard>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-transactions-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-transactions-error"
      @retry="load(store.page)"
    />

    <UiGrid :columns="4">
      <UiStatTile :label="t('billing.transactionsList.totalIncome')" :value="formatPlainAmount(store.summary.totalIn, store.summary.currency)" tone="success" data-testid="billing-transactions-total-in" />
      <UiStatTile :label="t('billing.transactionsList.totalExpenditure')" :value="formatPlainAmount(store.summary.totalOut, store.summary.currency)" tone="danger" data-testid="billing-transactions-total-out" />
      <UiStatTile :label="t('billing.transactionsList.totalFees')" :value="formatPlainAmount(store.summary.totalFees, store.summary.currency)" data-testid="billing-transactions-total-fees" />
      <UiStatTile :label="t('shared.balance')" :value="formatPlainAmount(store.summary.balance, store.summary.currency)" tone="primary" data-testid="billing-transactions-balance" />
    </UiGrid>

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
        :colspan="8"
        data-testid="billing-transactions-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.amountIn') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.fees') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.amountOut') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="transaction in shownTransactions" :key="transaction.id" hoverable>
            <UiTableCell>{{ transaction.clientName }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
            <UiTableCell>{{ transaction.paymentMethod || t('billing.transactionsList.manual') }}</UiTableCell>
            <UiTableCell>{{ transaction.description }}</UiTableCell>
            <UiTableCell>
              <UiText :tone="transaction.amountIn > 0 ? 'success' : 'muted'">{{ cell(transaction.amountIn, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell>{{ formatPlainAmount(transaction.fees, transaction.currency) }}</UiTableCell>
            <UiTableCell>
              <UiText :tone="transaction.amountOut > 0 ? 'danger' : 'muted'">{{ cell(transaction.amountOut, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell align="right">
              <UiStack direction="row" gap="xs" justify="end">
                <UiLink
                  variant="ghost"
                  size="sm"
                  :to="transactionEditPath(transaction.id)"
                  :aria-label="t('shared.edit')"
                  :data-testid="`billing-transaction-edit-${transaction.id}`"
                >
                  <UiIcon :markup="Icon.Edit" size="sm" />
                </UiLink>
                <UiIconButton
                  :label="t('shared.delete')"
                  size="sm"
                  :data-testid="`billing-transaction-delete-${transaction.id}`"
                  @click="pendingDeleteId = transaction.id"
                >
                  <UiIcon :markup="Icon.TrashBin" size="sm" />
                </UiIconButton>
              </UiStack>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.transactionsList.noTransactionsFound') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="store.page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="billing-transactions-pagination"
      @update:page="load"
    />

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="pendingDeleteId !== null"
      :title="t('billing.transactionsList.deleteTransaction')"
      :message="t('billing.transactionsList.deleteTransactionMessage', { id: pendingDeleteId ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="store.loading"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
