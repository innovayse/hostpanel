<script setup lang="ts">
/**
 * Client transactions page: displays financial ledger entries with summary cards and allows adding
 * new transactions and deleting existing ones. A delete is confirmed first; adding and deleting
 * report their outcome (success, or the API's sentence) above the ledger, and a refused load shows
 * the sentence with a Retry.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientTransactionsStore } from '@/stores/clientTransactions'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import AddTransactionModal from '@/components/clients/AddTransactionModal.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'

const { t } = useI18n()

/** Icon markup (shape tags) of the delete button. */
const TRASH_ICON = '<polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />'

/** Current route, source of the client id. */
const route = useRoute()

/** Client transactions store, the only caller of the transaction endpoints. */
const store = useClientTransactionsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(LEDGER_PAGE_SIZE)

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
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Fetches transactions for the current client.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchTransactions = async (): Promise<void> => {
  await store.fetchByClient(clientId.value, page.value, pageSize.value)
  loadError.value = store.error
}

/**
 * Navigates to a specific page and re-fetches.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  fetchTransactions()
}

/**
 * Handles successful save from the add transaction modal.
 *
 * @returns Nothing.
 */
const onTransactionSaved = (): void => {
  showAddModal.value = false
  feedback.show(t('clients.clientTransactions.transactionAdded'), false)
  void fetchTransactions()
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
  feedback.clear()
  deleting.value = true
  const ok = await store.remove(deleteTargetId.value)
  deleting.value = false
  showDeleteModal.value = false
  deleteTargetId.value = null
  if (ok) {
    feedback.show(t('clients.clientTransactions.transactionDeleted'), false)
    await fetchTransactions()
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

/** Loads the first page. */
onMounted(() => fetchTransactions())
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="1" variant="card">{{ t('shared.transactions') }}</UiHeading>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Primary"
        data-testid="client-transactions-add"
        @click="showAddModal = true"
      >
        {{ t('clients.clientTransactions.addNewTransaction') }}
      </UiButton>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-transactions-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="client-transactions-error"
      @retry="fetchTransactions()"
    />

    <UiStack v-if="store.loading && store.transactions.length === 0" data-testid="client-transactions-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientTransactions.loadingTransactions') }}</UiText>
    </UiStack>

    <template v-else>
      <UiGrid :columns="4">
        <UiStatTile
          :label="t('clients.clientTransactions.totalIn')"
          :value="formatPlainAmount(store.summary.totalIn, store.summary.currency)"
          :tone="UiTone.Success"
        />
        <UiStatTile
          :label="t('clients.clientTransactions.totalFees')"
          :value="formatPlainAmount(store.summary.totalFees, store.summary.currency)"
          :tone="UiTone.Warning"
        />
        <UiStatTile
          :label="t('clients.clientTransactions.totalOut')"
          :value="formatPlainAmount(store.summary.totalOut, store.summary.currency)"
          :tone="UiTone.Danger"
        />
        <UiStatTile
          :label="t('shared.balance')"
          :value="formatPlainAmount(store.summary.balance, store.summary.currency)"
          :tone="UiTone.Primary"
        />
      </UiGrid>

      <UiCard class="overflow-x-auto">
        <UiTable
          density="comfortable"
          :empty="store.transactions.length === 0"
          :colspan="7"
          data-testid="client-transactions-table"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.amountIn') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.amountOut') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.fees') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="tx in store.transactions" :key="tx.id" hoverable>
              <UiTableCell muted>{{ formatDate(tx.date) }}</UiTableCell>
              <UiTableCell muted>{{ tx.paymentMethod }}</UiTableCell>
              <UiTableCell muted>{{ tx.description }}</UiTableCell>
              <UiTableCell>
                <UiText :tone="tx.amountIn > 0 ? UiTextTone.Success : UiTextTone.Muted">{{ formatPlainAmount(tx.amountIn, tx.currency) }}</UiText>
              </UiTableCell>
              <UiTableCell>
                <UiText :tone="tx.amountOut > 0 ? UiTextTone.Danger : UiTextTone.Muted">{{ formatPlainAmount(tx.amountOut, tx.currency) }}</UiText>
              </UiTableCell>
              <UiTableCell muted>{{ formatPlainAmount(tx.fees, tx.currency) }}</UiTableCell>
              <UiTableCell>
                <UiIconButton
                  :label="t('shared.delete')"
                  :size="UiSize.Sm"
                  :disabled="store.loading"
                  :data-testid="`client-transactions-delete-${tx.id}`"
                  @click="confirmDelete(tx.id)"
                >
                  <UiIcon :markup="TRASH_ICON" />
                </UiIconButton>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('shared.noRecordsFound') }}
          </template>
        </UiTable>
        <UiStack
          v-if="store.totalCount > pageSize"
          direction="row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientTransactions.totalcountTransactionValue', { totalCount: store.totalCount }) }}
          </UiText>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="pageSize"
            testid="client-transactions-pagination"
            @update:page="goToPage"
          />
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      v-if="showDeleteModal"
      variant="confirm"
      :title="t('clients.clientTransactions.deleteTransaction')"
      :message="t('clients.clientTransactions.confirmDeleteTransaction', { id: deleteTargetId })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      tone="danger"
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
