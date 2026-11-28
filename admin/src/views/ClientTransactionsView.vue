<script setup lang="ts">
/**
 * Client transactions page: displays financial ledger entries with summary cards, a search box and the filter dialog
 * (direction, payment method, date range, amount) with its removable chips, and allows adding new transactions and
 * deleting existing ones. The search text, the filters and the page live in the address (`useListQuery`), so a reload,
 * a shared link and the back button restore the same ledger, and the API narrows the rows and their summary totals
 * before it pages. A delete is confirmed first; adding and deleting report their outcome (success, or the API's
 * sentence) above the ledger, and a refused load shows the sentence with a Retry.
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientTransactionsStore } from '@/stores/clientTransactions'
import { useFilterDateRange } from '@/composables/useFilterDateRange'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { TransactionFilterKey, useTransactionFilters } from '@/composables/useTransactionFilters'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { paymentMethodLabel } from '@/lib/tones'
import AddTransactionModal from '@/components/clients/AddTransactionModal.vue'
import TransactionFilterFields from '@/components/billing/TransactionFilterFields.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema, { CLIENT_TRANSACTIONS_STATS } from '@/views/ClientTransactionsView.schema'
import type { Transaction } from '@/types/transaction'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiHeadingVariant, UiTextTone } from '@/utils/ui/text'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { UiTableDensity } from '@/utils/ui/injection'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client transactions store, the only caller of the transaction endpoints. */
const store = useClientTransactionsStore()

/** The filters, the search text and the page of the ledger, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, dateOf, applied, chipsOf }
  = useListQuery(Object.values(TransactionFilterKey))

/** The applied direction, payment method, period and amount, and their chips. */
const transactionFilters = useTransactionFilters({ enumOf, dateOf, applied })

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(transactionFilters.chipSources)

/** The date range of the filter draft; the picker edits the pair, the address keeps its two ends. */
const draftRange = useFilterDateRange(draft, TransactionFilterKey.From, TransactionFilterKey.To)

/** True while the first load is in flight and there are no transactions yet; the tiles and the table then draw their placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.transactions.length === 0)

/** The figure and colour of each ledger tile by its key; the captions come from the schema. */
const figures = computed<Record<string, UiStatFigure>>(() => ({
  totalIn: { value: formatPlainAmount(store.summary.totalIn, store.summary.currency), tone: UiTone.Success },
  totalFees: { value: formatPlainAmount(store.summary.totalFees, store.summary.currency), tone: UiTone.Warning },
  totalOut: { value: formatPlainAmount(store.summary.totalOut, store.summary.currency), tone: UiTone.Danger },
  balance: { value: formatPlainAmount(store.summary.balance, store.summary.currency), tone: UiTone.Primary },
}))

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Whether the add transaction modal is visible. */
const showAddModal = ref(false)

/** Whether the delete confirmation modal is visible. */
const showDeleteModal = ref(false)

/** ID of the transaction pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** True while the delete is in flight. */
const deleting = ref(false)

/** The sentence of the last refused ledger load, shown with Retry; null when the ledger loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last add or delete. */
const feedback = useOutcomeToast()

/**
 * Loads the page of the ledger the address names with the filters and search it holds.
 *
 * @returns Promise that resolves when data is loaded.
 */
const load = async (): Promise<void> => {
  await store.fetchByClient(clientId.value, page.value, { search: appliedSearch.value, ...transactionFilters.filters.value })
  loadError.value = store.error
}

// Every change of a filter, the search or the page loads the ledger the address names.
loadOnChange(load)

/**
 * Handles successful save from the add transaction modal.
 *
 * @returns Nothing.
 */
const onTransactionSaved = (): void => {
  showAddModal.value = false
  feedback.show(t('clients.clientTransactions.transactionAdded'), false)
  void load()
}

/**
 * Opens the delete confirmation modal for a transaction.
 *
 * @param id - The transaction ID to delete.
 * @returns Nothing.
 */
const confirmDelete = (id: number): void => {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Executes the pending delete, shows the outcome and refreshes the list on success.
 *
 * @returns Promise that resolves when deletion completes.
 */
const executeDelete = async (): Promise<void> => {
  if (!deleteTargetId.value) {
    return
  }
  deleting.value = true
  const ok = await store.remove(deleteTargetId.value)
  deleting.value = false
  showDeleteModal.value = false
  deleteTargetId.value = null
  if (ok) {
    feedback.show(t('clients.clientTransactions.transactionDeleted'), false)
    await load()
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    showDeleteModal.value = false
    deleteTargetId.value = null
  }
}

/**
 * The row actions of a transaction: one, delete it (confirmed first).
 *
 * @param tx - The row.
 * @returns The action list passed to UiRowActions.
 */
const transactionActions = (tx: Transaction): UiMenuAction[] => [
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    testid: `client-transactions-delete-${tx.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      confirmDelete(tx.id)
    },
    danger: true,
    disabled: store.loading,
  },
]
</script>

<template>
  <UiStack>
    <UiSectionHeader :level="1" :variant="UiHeadingVariant.Card">
      {{ t('shared.transactions') }}
      <template #actions>
        <UiButton :action="UiActionKind.Add"
          type="button"
          :variant="UiButtonVariant.Primary"
          data-testid="client-transactions-add"
          @click="showAddModal = true"
        >
          {{ t('clients.clientTransactions.addTransaction') }}
        </UiButton>
      </template>
    </UiSectionHeader>


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-transactions-error"
      @retry="load()"
    />

    <template v-else>
      <UiStatRow :stats="CLIENT_TRANSACTIONS_STATS" :figures="figures" :loading="initialLoading" testid-prefix="client-transactions-stat" />

      <UiListToolbar
        v-model:search="search"
        search-testid="client-transactions-search"
        :search-placeholder="t('billing.transactionsList.searchTransactions')"
        filter-testid="client-transactions-filter"
        :active-count="activeCount"
        :chips="chips"
        :loading="initialLoading"
        @apply="apply"
        @reset="reset"
        @cancel="cancel"
        @remove="remove"
        @clear="clearAll"
      >
        <TransactionFilterFields
          v-model:direction="draft.direction"
          v-model:payment-method="draft.paymentMethod"
          v-model:period="draftRange"
          v-model:amount="draft.amount"
          testid-prefix="client-transactions-filter"
        />
      </UiListToolbar>

      <UiDataTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        :empty="!initialLoading && store.transactions.length === 0"
        :page="page"
        :total="store.totalCount"
        :page-size="LEDGER_PAGE_SIZE"
        pagination-testid="client-transactions-pagination"
        data-testid="client-transactions-table"
        @update:page="setPage"
      >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody>
          <UiTableRow v-for="tx in store.transactions" :key="tx.id" hoverable>
            <UiTableCell muted>{{ formatDate(tx.date) }}</UiTableCell>
            <UiTableCell muted>{{ paymentMethodLabel(tx.paymentMethod) }}</UiTableCell>
            <UiTableCell muted>{{ tx.description }}</UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="tx.amountIn > 0 ? UiTextTone.Success : UiTextTone.Muted">{{ formatPlainAmount(tx.amountIn, tx.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="tx.amountOut > 0 ? UiTextTone.Danger : UiTextTone.Muted">{{ formatPlainAmount(tx.amountOut, tx.currency) }}</UiText>
            </UiTableCell>
            <UiTableCell muted numeric>{{ formatPlainAmount(tx.fees, tx.currency) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="transactionActions(tx)" :testid="`client-transactions-actions-${tx.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="narrowed ? t('clients.clientTransactions.noTransactionsMatchYourFilters') : t('shared.noRecordsFound')"
            data-testid="client-transactions-empty"
          >
            <template v-if="activeCount > 0" #actions>
              <UiButton :variant="UiButtonVariant.Link" data-testid="client-transactions-clear-empty" @click="clearAll">
                {{ t('shared.clearFilters') }}
              </UiButton>
            </template>
          </UiEmptyState>
        </template>
      </UiDataTable>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      :variant="UiDialogVariant.Confirm"
      :title="t('clients.clientTransactions.deleteTransaction')"
      :message="t('clients.clientTransactions.confirmDeleteTransaction', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="executeDelete"
      @close="dismissDelete"
    />

    <AddTransactionModal
      v-if="showAddModal"
      :client-id="clientId"
      :saving="false"
      @saved="onTransactionSaved"
      @close="showAddModal = false"
    />
  </UiStack>
</template>
