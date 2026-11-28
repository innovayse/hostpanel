<script setup lang="ts">
/**
 * Billing transactions ledger: the totals of the rows the filters keep, a net-revenue chart of the page, and the
 * page's transactions, narrowed by the search box (description, reference, payment method or id) and by a filter
 * dialog (client, direction, payment method, date range, amount) whose applied filters show as chips. The search
 * text, the filters and the page live in the address (`useListQuery`), so a reload or a shared link shows the same
 * ledger, and the API narrows the rows and their totals before it pages. The client's name is not searched, which only
 * the Clients module can read: the client filter picks the client instead. Rows can be edited or deleted; a delete is
 * confirmed in a danger prompt and its outcome (success, or the API's sentence) shows above the table. A refused load
 * offers a retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import TransactionChart from '@/components/billing/TransactionChart.vue'
import TransactionFilterFields from '@/components/billing/TransactionFilterFields.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { TransactionFilterKey, useTransactionFilters } from '@/composables/useTransactionFilters'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import { UiCardPadding } from '@/utils/ui/card'
import { UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextTone } from '@/utils/ui/text'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import schema from '@/views/TransactionsListView.schema'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { paymentMethodLabel } from '@/lib/tones'
import { useTransactionsStore } from '@/stores/billingTransactions'
import type { TransactionFilters } from '@/types/transaction'
import { AppRoute, transactionEditPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('billing.addTransaction.createTransaction'), variant: UiButtonVariant.Primary, testid: 'billing-transactions-add', to: AppRoute.BillingAddTransaction }),
])

/** The filter fields kept in the address: the transaction filters every transaction list has, and the client this list adds. */
const FilterKey = {
  ...TransactionFilterKey,
  ClientId: 'clientId',
} as const

const store = useTransactionsStore()
const { clients, load: loadClients } = useClientOptions()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, loadOnChange, enumOf, idOf, dateOf, applied, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The applied direction, payment method, period and amount, and their chips. */
const transactionFilters = useTransactionFilters({ enumOf, dateOf, applied })

/** The client in the address as an id, undefined when none is set or the value is not an id. */
const clientIdOfAddress = idOf(FilterKey.ClientId)

/** What the address narrows the ledger by, as the store sends it to the API. */
const filters = computed<TransactionFilters>(() => ({
  search: appliedSearch.value,
  clientId: clientIdOfAddress.value,
  ...transactionFilters.filters.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.ClientId,
    label: t('shared.client'),
    value: clientIdOfAddress.value === undefined
      ? undefined
      : (clients.value.find(client => client.id === clientIdOfAddress.value)?.name ?? String(clientIdOfAddress.value)),
  },
  ...transactionFilters.chipSources(),
])

/** The date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(draft, FilterKey.From, FilterKey.To)

/** Transaction waiting for the delete confirmation, null when none. */
const pendingDeleteId = ref<number | null>(null)

/**
 * Formats an amount for a table cell, a dash when there is none.
 *
 * @param value - The amount.
 * @param currency - ISO 4217 code the amount is in.
 * @returns The amount with its currency, or a dash for zero.
 */
const cell = (value: number, currency: string): string => (value > 0 ? formatPlainAmount(value, currency) : EMPTY_VALUE)

/**
 * Loads the page of the ledger the address names with the filters and search it holds.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchAll(page.value, filters.value, store.pageSize)

// Every change of a filter, the search or the page loads the ledger the address names.
loadOnChange(load)

onMounted(loadClients)

/** The outcome line of the last delete. */
const feedback = useOutcomeToast()

/** Deletes the transaction that waits for confirmation, shows the outcome and reads the list again on success. */
const confirmDelete = async (): Promise<void> => {
  const id = pendingDeleteId.value
  if (id === null) {
    return
  }
  const deleted = await store.remove(id)
  pendingDeleteId.value = null
  if (deleted) {
    feedback.show(t('shared.recordDeleted'), false)
    await load()
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
    icon: Icon.Edit,
    testid: `billing-transaction-edit-${id}`,
    to: transactionEditPath(id),
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
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
</script>

<template>
  <UiStack data-testid="billing-transactions-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiGrid :columns="4">
      <UiStatTile :label="t('billing.transactionsList.totalIncome')" :value="formatPlainAmount(store.summary.totalIn, store.summary.currency)" :tone="UiTone.Success" data-testid="billing-transactions-total-in" />
      <UiStatTile :label="t('billing.transactionsList.totalExpenditure')" :value="formatPlainAmount(store.summary.totalOut, store.summary.currency)" :tone="UiTone.Danger" data-testid="billing-transactions-total-out" />
      <UiStatTile :label="t('billing.transactionsList.totalFees')" :value="formatPlainAmount(store.summary.totalFees, store.summary.currency)" data-testid="billing-transactions-total-fees" />
      <UiStatTile :label="t('shared.balance')" :value="formatPlainAmount(store.summary.balance, store.summary.currency)" :tone="UiTone.Primary" data-testid="billing-transactions-balance" />
    </UiGrid>

    <UiListToolbar
      v-model:search="search"
      search-testid="billing-transactions-search"
      :search-placeholder="t('billing.transactionsList.searchTransactions')"
      filter-testid="billing-transactions-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack :gap="UiGap.Md">
        <UiField :label="t('shared.client')">
          <UiClientSelect
            v-model="draft.clientId"
            :clients="clients"
            :placeholder="t('shared.startTypingToSearchClients')"
          />
        </UiField>
        <TransactionFilterFields
          v-model:direction="draft.direction"
          v-model:payment-method="draft.paymentMethod"
          v-model:period="draftRange"
          v-model:amount="draft.amount"
          testid-prefix="billing-transactions-filter"
        />
      </UiStack>
    </UiListToolbar>


    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-transactions-error"
      @retry="load"
    />

    <UiCard :padding="UiCardPadding.Md">
      <UiStack :gap="UiGap.Sm">
        <UiHeading :level="3" :variant="UiHeadingVariant.Card">
          {{ t('billing.transactionsList.netRevenueUsd') }}
        </UiHeading>
        <TransactionChart :transactions="store.transactions" :loading="store.loading && store.transactions.length === 0" />
      </UiStack>
    </UiCard>

    <UiDataTable
      :loading="store.loading && store.transactions.length === 0"
      :empty="!store.loading && store.transactions.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="billing-transactions-pagination"
      data-testid="billing-transactions-table"
      @update:page="setPage"
    >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow v-for="transaction in store.transactions" :key="transaction.id" hoverable>
            <UiTableCell>{{ transaction.clientName }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(transaction.date) }}</UiTableCell>
            <UiTableCell>{{ transaction.paymentMethod ? paymentMethodLabel(transaction.paymentMethod) : t('billing.transactionsList.manual') }}</UiTableCell>
            <UiTableCell>{{ transaction.description }}</UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="transaction.amountIn > 0 ? UiTextTone.Success : UiTextTone.Muted">{{ cell(transaction.amountIn, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(transaction.fees, transaction.currency) }}</UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="transaction.amountOut > 0 ? UiTextTone.Danger : UiTextTone.Muted">{{ cell(transaction.amountOut, transaction.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="transactionActions(transaction.id)" :testid="`billing-transaction-actions-${transaction.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('billing.transactionsList.noTransactionsFound')" data-testid="billing-transactions-empty" />
        </template>
    </UiDataTable>

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
